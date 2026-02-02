using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;

namespace Umi.Rpc.Protocol;

/// <summary>
/// 这个payload 将会拥有大量数据, 如大量字符串(ASCII)，
/// 这里将会约定双方的序列化方法，(需要具体实现注册序列化方法）
/// 交换双方的提供/需要的服务(具体规则需要由具体实现定义）
/// </summary>
public sealed unsafe class RpcMetadataConsent : RpcPackageBase
{
    private RpcMetadataConsent(void* data, int size)
        : base(data, size)
    {
    }

    /*********************************************************
     * 这里详细说明一下包结构
     * Serialization:        unsigned int,  4             bytes  约定双方的序列化类型(Msgpack? protobuf? JSON?)
     * PackageLength:        int,           4             bytes  整个数据包的总长度（含固定头）
     * StringPoolOffset:     int,           4             bytes  字符串池偏移（相对包头
     * ContentHeaderCount:   short          2             bytes  内容数据数组长度
     * Content Array         Content        9*count       bytes  内容数组
     * -   Flag              byte           1             byte   内容数组标识
     * -   Offset            int            4             bytes  内容数组指向的内容数据偏移
     * -   Count             int            4             bytes  内容数据长度/数量
     * Service Array         Service        20*count      bytes  服务数组
     * -   Version           int            4             bytes  服务版本
     * -   Offset            int            4             bytes  服务数组服务名字符串相对String Pool 偏移
     * -   Length            int            4             bytes  服务数组服务名字符串字节长度
     * -   ImplementOffset   int            4             bytes  服务实现偏移
     * -   ImplementLength   int            4             bytes  服务实现长度
     * TypeMapping Array     TypeMapping    16*count      bytes  类型映射数组
     * -   Source Type       long           8             bytes  源类型（Offset, Length)组合， 字符串池
     * -   Target Type       long           8             bytes  目标类型（Offset, Length)组合， 字符串池
     * EventHandle Array     EventHandle    48*count      bytes  事件句柄数组
     * -   Type UUID         GUID           16            bytes  类型 UUID
     * -   Object UUID       GUID           16            bytes  对象 UUID
     * -   Type Name         long           8             bytes  对象类型名（Offset, Length) 组合， 字符串池
     * -   Event Name        long           8             bytes  事件名（Offset，Length）组合，字符串池
     * String Pool           String Pool    variable      bytes  字符串池
     ********************************************************/

    /// <summary>
    /// 序列化协议
    /// </summary>
    public uint Serialization
    {
        get
        {
            ThrowIfDisposed();
            return *(uint*)Data;
        }
    }

    /// <summary>
    /// 包长度
    /// </summary>
    public int PackageLength
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 4);
        }
    }

    /// <summary>
    /// 字符串池的偏移
    /// </summary>
    public int StringPoolOffset
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 8);
        }
    }

    /// <summary>
    /// 内容数组数量
    /// </summary>
    public short ContentHeaderCount
    {
        get
        {
            ThrowIfDisposed();
            return *(short*)((byte*)Data + 12);
        }
    }

    /// <summary>
    /// 内容数组头部
    /// </summary>
    public ReadOnlySpan<RpcMetadataContent> ContentHeaders => new((byte*)Data + 14, ContentHeaderCount);

    /// <summary>
    /// 获取内容头的结构
    /// </summary>
    /// <param name="offset">头偏移</param>
    /// <param name="count">数量</param>
    /// <typeparam name="T">内容头结构</typeparam>
    /// <returns>返回内容头结构</returns>
    /// <exception cref="ArgumentOutOfRangeException">offset 超范围</exception>
    public ReadOnlySpan<T> GetContentHeader<T>(int offset, int count) where T : unmanaged
    {
        ThrowIfDisposed();
        // 保证内容在包长度内
        if (offset + 14 > PackageLength || offset + count * sizeof(T) + 14 > PackageLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        return new ReadOnlySpan<T>((byte*)Data + offset, count);
    }

    public string GetString(int offset, int length)
    {
        ThrowIfDisposed();
        if (offset + 14 > PackageLength || offset + length + 14 > PackageLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var pool = (byte*)Data + StringPoolOffset;
        return Encoding.ASCII.GetString(pool + offset, length);
    }


    public static RpcMetadataConsent CreateFromMemory(scoped in ReadOnlySpan<byte> data)
    {
        if (data.Length < 14)
        {
            throw new ArgumentOutOfRangeException(nameof(data));
        }

        fixed (byte* ptr = data)
        {
            var totalLength = *(int*)(ptr + 4);
            if (data.Length < totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(data));
            }

            if (*(int*)(ptr + 8) > totalLength)
            {
                throw new OutOfMemoryException("String Pool Offset out of memory");
            }

            var buffer = NativeMemory.Alloc((UIntPtr)totalLength);
            NativeMemory.Copy(ptr, buffer, (UIntPtr)totalLength);
            return new RpcMetadataConsent(buffer, totalLength);
        }
    }

    public static RpcMetadataConsent CreateFromMemory(scoped in ReadOnlySequence<byte> data)
    {
        if (data.Length < 14)
        {
            throw new ArgumentOutOfRangeException(nameof(data));
        }

        Span<byte> header = stackalloc byte[14];
        data.Slice(0, 14).CopyTo(header);
        fixed (byte* ptr = header)
        {
            var totalLength = *(int*)(ptr + 4);
            if (data.Length < totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(data));
            }

            if (*(int*)(ptr + 8) > totalLength)
            {
                throw new OutOfMemoryException("String Pool Offset out of memory");
            }

            var buffer = NativeMemory.Alloc((UIntPtr)totalLength);
            data.Slice(0, totalLength).CopyTo(new Span<byte>(buffer, totalLength));
            return new RpcMetadataConsent(buffer, totalLength);
        }
    }

    public static RpcMetadataConsent CreateFromMessage(uint serialization, RpcMetadataContentWrap content)
    {
        if (content.Service is null
            || content.Service.Length <= 0
            || content.TypeMapping is null
            || content.TypeMapping.Length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(content));
        }

        short contentHeaderCount = 2;
        var nameDataStr = content.Service
            .Select(p => p.ServiceName)
            .Concat(content.Service.Select(p => p.ImplementName))
            .Concat(content.TypeMapping.Select(p => p.Source))
            .Concat(content.TypeMapping.Select(p => p.Target));
        var eventContentCount = 0;
        if (content.Event is { Length: > 0 })
        {
            nameDataStr = nameDataStr.Concat(content.Event.Select(p => p.TypeName))
                .Concat(content.Event.Select(p => p.EventName));
            contentHeaderCount++;
            eventContentCount = content.Event.Length;
        }

        var nameData = nameDataStr.Distinct()
            .ToImmutableDictionary(p => p,
                p => Encoding.ASCII.GetBytes(p));

        var stringPoolLen = nameData.Sum(p => p.Value.Length);
        var packageLength = 14 + contentHeaderCount * sizeof(RpcMetadataContent)
                               + content.Service.Length * sizeof(RpcMetadataService)
                               + content.TypeMapping.Length * sizeof(RpcMetadataTypeMapping)
                               + eventContentCount * sizeof(RpcMetadataEventHandle)
                               + stringPoolLen;
        var buffer = (byte*)NativeMemory.Alloc((UIntPtr)packageLength);
        var stringPoolOffset = packageLength - stringPoolLen;
        *(uint*)buffer = serialization;
        *(int*)(buffer + 4) = packageLength;
        *(int*)(buffer + 8) = stringPoolOffset; // string pool offset 
        *(short*)(buffer + 12) = contentHeaderCount;
        // 先写入字符串池
        var stringPool = buffer + stringPoolOffset;
        Dictionary<string, (int Offset, int Length)> dic = new();
        var poolStrOffset = 0;
        foreach (var item in nameData)
        {
            ReadOnlySpan<byte> data = item.Value;
            var target = new Span<byte>(stringPool + poolStrOffset, item.Value.Length);
            data.CopyTo(target);
            dic.Add(item.Key, (poolStrOffset, item.Value.Length));
            poolStrOffset += item.Value.Length;
        }

        // 这是 service 的写入
        var serviceOffset = 14 + contentHeaderCount * sizeof(RpcMetadataContent);
        MetadataContentHeaderWrite(buffer + 14, RpcMetadataContent.FLAG_SERVICE, serviceOffset, content.Service.Length);
        var serviceArray = buffer + serviceOffset;
        for (var i = 0; i < content.Service.Length; i++)
        {
            *(int*)(serviceArray + i * sizeof(RpcMetadataService)) = content.Service[i].Version;
            var (offset, length) = dic[content.Service[i].ServiceName];
            *(int*)(serviceArray + i * sizeof(RpcMetadataService) + 4) = offset;
            *(int*)(serviceArray + i * sizeof(RpcMetadataService) + 8) = length;
            (offset, length) = dic[content.Service[i].ImplementName];
            *(int*)(serviceArray + i * sizeof(RpcMetadataService) + 12) = offset;
            *(int*)(serviceArray + i * sizeof(RpcMetadataService) + 16) = length;
        }

        // 这是 typeMapping 的写入
        var typeMappingOffset = serviceOffset + content.Service.Length * sizeof(RpcMetadataService);
        MetadataContentHeaderWrite(buffer + 14 + sizeof(RpcMetadataContent),
            RpcMetadataContent.FLAG_TYPE_MAPPING,
            typeMappingOffset,
            content.TypeMapping.Length);
        var typeMappingArray = buffer + typeMappingOffset;
        for (var i = 0; i < content.TypeMapping.Length; i++)
        {
            var (offset, length) = dic[content.TypeMapping[i].Source];
            *(int*)(typeMappingArray + i * sizeof(RpcMetadataTypeMapping)) = offset;
            *(int*)(typeMappingArray + i * sizeof(RpcMetadataTypeMapping) + 4) = length;
            var (targetOffset, targetLength) = dic[content.TypeMapping[i].Target];
            *(int*)(typeMappingArray + i * sizeof(RpcMetadataTypeMapping) + 8) = targetOffset;
            *(int*)(typeMappingArray + i * sizeof(RpcMetadataTypeMapping) + 12) = targetLength;
        }

        // event handle 的写入
        if (content.Event is not { Length: > 0 }) return new RpcMetadataConsent(buffer, packageLength);
        var eventHandleOffset = typeMappingOffset + content.TypeMapping.Length * sizeof(RpcMetadataTypeMapping);
        MetadataContentHeaderWrite(buffer + 14 + 2 * sizeof(RpcMetadataContent),
            RpcMetadataContent.FLAG_EVENT_HANDLE,
            eventHandleOffset,
            content.Event.Length);
        var eventHandleArray = buffer + eventHandleOffset;
        for (var i = 0; i < content.Event.Length; i++)
        {
            Span<byte> typeGuid = new(eventHandleArray + i * sizeof(RpcMetadataEventHandle), 16);
            content.Event[i].TypeGuid.TryWriteBytes(typeGuid);
            Span<byte> objectGuid = new(eventHandleArray + i * sizeof(RpcMetadataEventHandle) + 16, 16);
            content.Event[i].ObjectGuid.TryWriteBytes(objectGuid);
            var (toffset, tlength) = dic[content.Event[i].TypeName];
            *(int*)(eventHandleArray + i * sizeof(RpcMetadataEventHandle) + 32) = toffset;
            *(int*)(eventHandleArray + i * sizeof(RpcMetadataEventHandle) + 36) = tlength;

            var (eoffset, elength) = dic[content.Event[i].EventName];
            *(int*)(eventHandleArray + i * sizeof(RpcMetadataEventHandle) + 40) = eoffset;
            *(int*)(eventHandleArray + i * sizeof(RpcMetadataEventHandle) + 44) = elength;
        }

        return new RpcMetadataConsent(buffer, packageLength);
    }

    private static void MetadataContentHeaderWrite(byte* address, byte flag, int offset, int count)
    {
        *address = flag;
        *(int*)(address + 1) = offset;
        *(int*)(address + 5) = count;
    }
}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
public readonly struct RpcMetadataContent
{
    // ReSharper disable InconsistentNaming
    public const byte FLAG_SERVICE = 0x1;
    public const byte FLAG_TYPE_MAPPING = 0x2;
    public const byte FLAG_EVENT_HANDLE = 0x3;
    // ReSharper restore InconsistentNaming

    /// <summary>
    /// Metadata 标志 (见本类定义）
    /// </summary>
    [FieldOffset(0)] public readonly byte Flag;

    /// <summary>
    /// 内容数组指向
    /// </summary>
    [FieldOffset(1)] public readonly long ContentArray;

    [FieldOffset(1)] public readonly int Offset;
    [FieldOffset(5)] public readonly int Count;
}

/// <summary>
/// 客户端->服务端 （询问具体服务/接口的实现）
/// 服务端->客户端 （回复具体的实现/服务名）
/// </summary>
[StructLayout(LayoutKind.Explicit, Pack = 1)]
public readonly struct RpcMetadataService
{
    /// <summary>
    /// 服务版本
    /// </summary>
    [FieldOffset(0)] public readonly int Version;

    /// <summary>
    /// 服务名
    /// </summary>
    [FieldOffset(4)] public readonly long Name;

    /// <summary>
    /// 服务在字符串池中的相对offset
    /// </summary>
    [FieldOffset(4)] public readonly int NameOffset;

    /// <summary>
    /// 服务名长度
    /// </summary>
    [FieldOffset(8)] public readonly int NameLength;

    /// <summary>
    ///  实现输出
    /// </summary>
    [FieldOffset(12)] public readonly long Implement;

    /// <summary>
    /// 实现offset
    /// </summary>
    [FieldOffset(12)] public readonly int ImplementOffset;

    /// <summary>
    /// 实现长度
    /// </summary>
    [FieldOffset(16)] public readonly int ImplementLength;
}

/// <summary>
/// 特殊基础类型的特殊序列化映射
/// 客户端->服务端 发送客户端存在的
/// 服务端->客户端 约定共同支持的
/// 如果出现缺少的，子类自行协商是否回落到通用序列化
/// SourceType 可约定 特殊类型
/// TargetType 可约定 特殊序列化方式
/// </summary>
[StructLayout(LayoutKind.Explicit, Pack = 1)]
public readonly struct RpcMetadataTypeMapping
{
    [FieldOffset(0)] public readonly long SourceType;
    [FieldOffset(8)] public readonly long TargetType;


    [FieldOffset(0)] public readonly int SourceTypeOffset;
    [FieldOffset(4)] public readonly int SourceTypeLength;


    [FieldOffset(8)] public readonly int TargetTypeOffset;
    [FieldOffset(12)] public readonly int TargetTypeLength;
}

/// <summary>
/// 这里的ObjectGuid 通常可以全填0,如果是单例的可以直接确认
/// 这里是 客户端->服务端的通知，服务端不存在这一项
/// </summary>
[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]
public readonly unsafe struct RpcMetadataEventHandle
{
    [FieldOffset(0)] public readonly byte TypeGUID;

    public Guid TypeGuid
    {
        get
        {
            fixed (byte* ptr = &TypeGUID)
            {
                ReadOnlySpan<byte> span = new(ptr, 16);
                return new Guid(span);
            }
        }
    }

    [FieldOffset(16)] public readonly byte ObjectGUID;

    public Guid ObjectGuid
    {
        get
        {
            fixed (byte* ptr = &ObjectGUID)
            {
                ReadOnlySpan<byte> span = new(ptr, 16);
                return new Guid(span);
            }
        }
    }

    [FieldOffset(32)] public readonly long TypeName;
    [FieldOffset(32)] public readonly int TypeNameOffset;
    [FieldOffset(36)] public readonly int TypeNameLength;

    [FieldOffset(40)] public readonly long EventName;
    [FieldOffset(40)] public readonly int EventNameOffset;
    [FieldOffset(44)] public readonly int EventNameLength;
}

public readonly record struct RpcMetadataServiceWrap(int Version, string ServiceName, string ImplementName);

public readonly record struct RpcMetadataTypeMappingWrap(string Source, string Target);

public readonly record struct RpcMetadataEventWrap(Guid TypeGuid, Guid ObjectGuid, string TypeName, string EventName);

public readonly record struct RpcMetadataContentWrap(
    RpcMetadataServiceWrap[] Service,
    RpcMetadataTypeMappingWrap[] TypeMapping,
    RpcMetadataEventWrap[]? Event);