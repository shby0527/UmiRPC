using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;

namespace Umi.Rpc.Protocol;

/// <summary>
/// Call 方法调用消息
/// </summary>
public sealed unsafe class RpcCallMessage : RpcPackageBase
{
    /************************************************
     * 我们考虑 Call 消息应该如何内存布局?
     * 首先明确 Call 目标
     * 由于我们已经在 Metadata 上交换过实现了
     * 但是泛型并没有
     * 实际上我们应该需要知道
     * Implement 在metadata 约定了，所以 client 可以直接从 Interface 中获得服务端的实现，
     * 所以实际上，客户端会直接发送 Implement 名字，如果存在泛型，应该有泛型信息
     * 以及调用的方法，如果存在泛型，也要有泛型信息
     * 实际上 服务端 在客户端 发起方法调用后，已经可以定位具体方法了
     * 所以方法类型和返回值类型不需要客户端传递
     * 那么 泛型类型信息如何传递？ 类型在服务端不存在应该 Method Not Found?
     * 依据 Metadata 的 序列化约定反序列化，以及和 type mapping 约定的特殊反序列化
     * 查找方法（需要一个机制来缓存所有可以进行call 的 封闭 method)
     * 调用后，序列化结果进行传递（除非发生异常）
     * 所以 包格式可以这样
     * ServiceName
     * GenericType[]
     * MethodName
     * MethodGenericType[]
     * Arguments[]
     ***********************************************/
    /**********************************************
     * PackageLength                               int                             4 bytes                   整个包长度
     * StringPoolOffset                            int                             4 bytes                   字符串池在包中的位置
     * ObjectPoolOffset                            int                             4 bytes                   对象数据池在包中的位置
     * ServiceName                                 long                            8 bytes                   服务名
     *     - ServiceNameOffset                     int                             4 bytes                   服务名Offset(相对StringPool)
     *     - ServiceNameLength                     int                             4 bytes                   服务名Length
     * ServiceGenericTypeCount                     int                             4 bytes                   服务泛型信息数量
     * ServiceGenericType                          Array                   8*ServiceGenericTypeCount bytes   服务泛型数组信息
     *     - TypeName                              long                            8 bytes                   服务泛型类型
     *         - TypeNameOffset                    int                             4 bytes
     *         - TypeNameLength                    int                             4 bytes
     * MethodName                                  long                            8 bytes                   服务方法名
     *     - Offset                                int                             4 bytes
     *     - Length                                int                             4 bytes
     * MethodGenericTypeCount                      int                             4 bytes                   方法泛型信息
     * MethodGenericType                           Array                   8*MethodGenericTypeCount bytes    方法泛型数组
     *     - TypeName                              long                            8 bytes
     *         - TypeNameOffset                    int                             4 bytes
     *         - TypeNameLength                    int                             4 bytes
     * ArgumentCount                               int                             4 bytes                   参数数量
     * ArgumentArray                               Array                   8*ArgumentCount bytes             参数数组
     *     - Argument                              long                            8 bytes                   参数引用（在ObjectPool)
     *         - ArgumentOffset                    int                             4 bytes
     *         - ArgumentLength                    int                             4 bytes
     * StringPool                                  variable
     * ObjectPool                                  variable
     ********************************************/
    private RpcCallMessage(void* data, int size)
        : base(data, size)
    {
    }

    public int PackageLength
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)Data;
        }
    }

    public int StringPoolOffset
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 4);
        }
    }

    public int ObjectPoolOffset
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 8);
        }
    }

    public ref RpcOffsetLength ServiceName
    {
        get
        {
            ThrowIfDisposed();
            return ref *(RpcOffsetLength*)((byte*)Data + 12);
        }
    }

    public int ServiceGenericTypeCount
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 12 + sizeof(RpcOffsetLength));
        }
    }

    public ReadOnlySpan<RpcOffsetLength> ServiceGenericType =>
        new((byte*)Data + 16 + sizeof(RpcOffsetLength), ServiceGenericTypeCount);


    public ref RpcOffsetLength MethodName =>
        ref *(RpcOffsetLength*)((byte*)Data + 16 + (1 + ServiceGenericTypeCount) * sizeof(RpcOffsetLength));


    public int MethodGenericTypeCount =>
        *(int*)((byte*)Data + 16 + (ServiceGenericTypeCount + 2) * sizeof(RpcOffsetLength));

    public ReadOnlySpan<RpcOffsetLength> MethodGenericType =>
        new((byte*)Data + 20 + (ServiceGenericTypeCount + 2) * sizeof(RpcOffsetLength), MethodGenericTypeCount);

    public int ArgumentCount => *(int*)((byte*)Data + 20 +
                                        (ServiceGenericTypeCount + MethodGenericTypeCount + 2) *
                                        sizeof(RpcOffsetLength));

    public ReadOnlySpan<RpcOffsetLength> Arguments => new((byte*)Data + 24 +
                                                          (ServiceGenericTypeCount + MethodGenericTypeCount + 2) *
                                                          sizeof(RpcOffsetLength), ArgumentCount);

    public string GetString(scoped in RpcOffsetLength offset)
    {
        ThrowIfDisposed();
        if (offset.Offset + StringPoolOffset > PackageLength ||
            offset.Offset + offset.Length + StringPoolOffset > PackageLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var pool = (byte*)Data + StringPoolOffset;
        return Encoding.ASCII.GetString(pool + offset.Offset, offset.Length);
    }

    public ReadOnlySpan<byte> GetObject(scoped in RpcOffsetLength offset)
    {
        ThrowIfDisposed();
        if (offset.Offset + ObjectPoolOffset > PackageLength ||
            offset.Offset + offset.Length + ObjectPoolOffset > PackageLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var pool = (byte*)Data + ObjectPoolOffset;
        return new ReadOnlySpan<byte>(pool + offset.Offset, offset.Length);
    }

    public static RpcCallMessage CreateFromMemory(scoped in ReadOnlySpan<byte> memory)
    {
        if (memory.Length < 12)
        {
            throw new ArgumentOutOfRangeException(nameof(memory));
        }

        fixed (byte* ptr = memory)
        {
            if (memory.Length < *(int*)ptr)
            {
                throw new ArgumentOutOfRangeException(nameof(memory));
            }

            if (*(int*)ptr < *(int*)(ptr + 4))
            {
                throw new OutOfMemoryException("String Pool out of memory");
            }

            if (*(int*)ptr < *(int*)(ptr + 8))
            {
                throw new OutOfMemoryException("Object Pool out of memory");
            }

            var buffer = NativeMemory.Alloc((nuint)(*(int*)ptr));
            NativeMemory.Copy(ptr, buffer, (nuint)(*(int*)ptr));
            return new RpcCallMessage(buffer, *(int*)ptr);
        }
    }

    public static RpcCallMessage CreateFromMemory(scoped in ReadOnlySequence<byte> memory)
    {
        if (memory.Length < 12)
        {
            throw new ArgumentOutOfRangeException(nameof(memory));
        }

        Span<byte> header = stackalloc byte[12];
        memory.Slice(0, 12).CopyTo(header);
        fixed (byte* ptr = header)
        {
            if (memory.Length < *(int*)ptr)
            {
                throw new ArgumentOutOfRangeException(nameof(memory));
            }

            if (*(int*)ptr < *(int*)(ptr + 4))
            {
                throw new OutOfMemoryException("String Pool out of memory");
            }

            if (*(int*)ptr < *(int*)(ptr + 8))
            {
                throw new OutOfMemoryException("Object Pool out of memory");
            }

            var buffer = NativeMemory.Alloc((nuint)(*(int*)ptr));
            Span<byte> span = new(buffer, *(int*)ptr);
            memory.Slice(0, *(int*)ptr).CopyTo(span);
            return new RpcCallMessage(buffer, *(int*)ptr);
        }
    }

    public static RpcCallMessage CreateFromMessage(scoped in RpcCallMessageWrap wrap)
    {
        // 让我们来重新映射计算所有字符串和参数数组
        if (wrap is not { ServiceName: { Length: > 0 }, MethodName: { Length: > 0 } })
        {
            throw new ArgumentException("ServiceName and MethodName MUST not empty", nameof(wrap));
        }

        var stringPoolData = wrap.ServiceGenericType
            .Concat([wrap.ServiceName, wrap.MethodName, ..wrap.MethodGenericType])
            .Distinct()
            .ToImmutableDictionary(k => k, v => Encoding.ASCII.GetBytes(v));
        var stringPoolSize = stringPoolData.Sum(p => p.Value.Length);
        // 为了效率。。我们放弃对象去重
        var objectPoolSize = wrap.Arguments.Sum(p => p.Length);
        var totalSize = 40 + 8 * (wrap.ServiceGenericType.Length + wrap.MethodGenericType.Length +
                                  wrap.Arguments.Length) + stringPoolSize + objectPoolSize;
        var stringPoolOffset = totalSize - stringPoolSize - objectPoolSize;
        var objectPoolOffset = totalSize - objectPoolSize;
        var buffer = (byte*)NativeMemory.Alloc((UIntPtr)totalSize);
        // 先存放 字符串池，并记录 Offset 和 Length
        Dictionary<string, (int Offset, int Length)> str = new();
        var pool = buffer + stringPoolOffset;
        var offset = 0;
        foreach (var item in stringPoolData)
        {
            fixed (byte* ptr = item.Value)
            {
                NativeMemory.Copy(ptr, pool + offset, (UIntPtr)item.Value.Length);
            }

            str.Add(item.Key, (offset, item.Value.Length));
            offset += item.Value.Length;
        }

        // 这里按数组顺序保存对象在对象池的位置
        var obj = new (int Offset, int Length)[wrap.Arguments.Length];
        offset = 0;
        pool = buffer + objectPoolOffset;
        for (var i = 0; i < wrap.Arguments.Length; i++)
        {
            fixed (byte* ptr = wrap.Arguments[i])
            {
                NativeMemory.Copy(ptr, pool + offset, (UIntPtr)wrap.Arguments[i].Length);
            }

            obj[i] = (offset, wrap.Arguments[i].Length);
            offset += wrap.Arguments[i].Length;
        }

        // 好的，开始写入每个字节
        *(int*)buffer = totalSize;
        *(int*)(buffer + 4) = stringPoolOffset;
        *(int*)(buffer + 8) = objectPoolOffset;
        (*(int*)(buffer + 12), *(int*)(buffer + 16)) = str[wrap.ServiceName];
        *(int*)(buffer + 20) = wrap.ServiceGenericType.Length;
        WriteString(buffer + 24, wrap.ServiceGenericType);
        // method name
        var tmp = buffer + 24 + 8 * wrap.ServiceGenericType.Length;
        (*(int*)tmp, *(int*)(tmp + 4)) = str[wrap.MethodName];
        *(int*)(tmp + 8) = wrap.MethodGenericType.Length;
        WriteString(tmp + 12, wrap.MethodGenericType);
        // Argument
        tmp = tmp + 12 + 8 * wrap.MethodGenericType.Length;
        *(int*)tmp = wrap.Arguments.Length;
        for (var i = 0; i < obj.Length; i++)
        {
            (*(int*)(tmp + 4 + i * 8), *(int*)(tmp + 8 + i * 8)) = obj[i];
        }

        return new RpcCallMessage(buffer, totalSize);

        void WriteString(byte* begin, string[] s)
        {
            for (var i = 0; i < s.Length; i++)
            {
                (*(int*)(begin + i * 8), *(int*)(begin + i * 8 + 4)) = str[s[i]];
            }
        }
    }
}

public readonly record struct RpcCallMessageWrap(
    string ServiceName,
    string[] ServiceGenericType,
    string MethodName,
    string[] MethodGenericType,
    byte[][] Arguments);