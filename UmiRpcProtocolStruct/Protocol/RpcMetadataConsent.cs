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
    /// 这是数组长度
    /// </summary>
    public int ServiceArrayLength
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 4);
        }
    }

    public int TypeMappingLength
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 8);
        }
    }

    /// <summary>
    /// 那个字符串池的直接大小
    /// </summary>
    public int StringPoolLength
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 12);
        }
    }

    /// <summary>
    /// 这是那个数组
    /// </summary>
    public ReadOnlySpan<RpcMetadataService> RpcMetadataServices => new((byte*)Data + 16, ServiceArrayLength);


    public ReadOnlySpan<RpcMetadataTypeMapping> RpcMetadataTypeMappings =>
        new((byte*)Data + 16 + sizeof(RpcMetadataService) * ServiceArrayLength, TypeMappingLength);

    /// <summary>
    /// 字符串池
    /// </summary>
    public ReadOnlySpan<byte> StringPool =>
        new(
            (byte*)Data + 16 + ServiceArrayLength * sizeof(RpcMetadataService) +
            sizeof(RpcMetadataTypeMapping) * TypeMappingLength, StringPoolLength);


    public string GetString(int offset, int length)
    {
        ThrowIfDisposed();
        if (offset > StringPoolLength || offset + length > StringPoolLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var pool = (byte*)Data + 16 + ServiceArrayLength * sizeof(RpcMetadataService) +
                   sizeof(RpcMetadataTypeMapping) * TypeMappingLength;
        return Encoding.ASCII.GetString(pool + offset, length);
    }


    public static RpcMetadataConsent CreateFromMemory(scoped in ReadOnlySpan<byte> data)
    {
        if (data.Length < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(data));
        }

        fixed (byte* ptr = data)
        {
            var totalLength = 16 + *(int*)(ptr + 4) * sizeof(RpcMetadataService) +
                              *(int*)(ptr + 8) * sizeof(RpcMetadataTypeMapping) + *(int*)(ptr + 12);
            if (data.Length < totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(data));
            }

            var buffer = NativeMemory.Alloc((UIntPtr)totalLength);
            NativeMemory.Copy(ptr, buffer, (UIntPtr)totalLength);
            return new RpcMetadataConsent(buffer, totalLength);
        }
    }

    public static RpcMetadataConsent CreateFromMemory(scoped in ReadOnlySequence<byte> data)
    {
        if (data.Length < 16)
        {
            throw new ArgumentOutOfRangeException(nameof(data));
        }

        Span<byte> header = stackalloc byte[16];
        data.Slice(0, 16).CopyTo(header);
        fixed (byte* ptr = header)
        {
            var totalLength = 16 + *(int*)(ptr + 4) * sizeof(RpcMetadataService) +
                              *(int*)(ptr + 8) * sizeof(RpcMetadataTypeMapping) + *(int*)(ptr + 12);
            if (data.Length < totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(data));
            }

            var buffer = NativeMemory.Alloc((UIntPtr)totalLength);
            data.CopyTo(new Span<byte>(buffer, totalLength));
            return new RpcMetadataConsent(buffer, totalLength);
        }
    }

    public static RpcMetadataConsent CreateFromMessage(uint serialization, RpcMetadataWrap[] metadata,
        RpcMetadataTypeMappingWrap[] typeMappings)
    {
        if (metadata is null || metadata.Length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata));
        }

        if (typeMappings is null)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata));
        }

        var nameData = metadata
            .Select(p => p.ServiceName)
            .Concat(typeMappings.Select(p => p.Source))
            .Concat(typeMappings.Select(p => p.Target))
            .Distinct()
            .ToImmutableDictionary(p => p,
                p => Encoding.ASCII.GetBytes(p));
        // ?? 长度？ 12 + metadata.Length * sizeof(RpcMetadataService) + sum(nameData)
        var stringPoolLen = nameData.Sum(p => p.Value.Length);
        var totalLength = 16 + metadata.Length * sizeof(RpcMetadataService) +
                          typeMappings.Length * sizeof(RpcMetadataTypeMapping) + stringPoolLen;
        var buffer = (byte*)NativeMemory.Alloc((UIntPtr)totalLength);
        *(uint*)buffer = serialization;
        *(int*)(buffer + 4) = metadata.Length;
        *(int*)(buffer + 8) = typeMappings.Length;
        *(int*)(buffer + 12) = stringPoolLen;
        var offset = 0;
        var stringPool = buffer + 16 + metadata.Length * sizeof(RpcMetadataService) +
                         typeMappings.Length * sizeof(RpcMetadataTypeMapping);
        Dictionary<string, (int Offset, int Length)> dic = new();
        // 先存储字符串池
        foreach (var item in nameData)
        {
            ReadOnlySpan<byte> source = item.Value;
            source.CopyTo(new Span<byte>(stringPool + offset, item.Value.Length));
            dic.Add(item.Key, (offset, item.Value.Length));
            offset += item.Value.Length;
        }

        // 还要修改一下内存
        for (var i = 0; i < metadata.Length; i++)
        {
            var ol = dic[metadata[i].ServiceName];
            *(int*)(buffer + 16 + i * sizeof(RpcMetadataService)) = metadata[i].Version;
            *(int*)(buffer + 16 + i * sizeof(RpcMetadataService) + 4) = ol.Length;
            *(int*)(buffer + 16 + i * sizeof(RpcMetadataService) + 8) = ol.Offset;
            *(long*)(buffer + 16 + i * sizeof(RpcMetadataService) + 12) = metadata[i].TransportType;
        }

        var beginOfTypeMapping = buffer + 16 + metadata.Length * sizeof(RpcMetadataService);
        for (var i = 0; i < typeMappings.Length; i++)
        {
            var sourceOl = dic[typeMappings[i].Source];
            var targetOl = dic[typeMappings[i].Target];
            *(int*)(beginOfTypeMapping + i * sizeof(RpcMetadataTypeMapping)) = sourceOl.Offset;
            *(int*)(beginOfTypeMapping + i * sizeof(RpcMetadataTypeMapping) + 4) = sourceOl.Length;
            *(int*)(beginOfTypeMapping + i * sizeof(RpcMetadataTypeMapping) + 8) = targetOl.Offset;
            *(int*)(beginOfTypeMapping + i * sizeof(RpcMetadataTypeMapping) + 12) = targetOl.Length;
        }

        return new RpcMetadataConsent(buffer, totalLength);
    }
}

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

[StructLayout(LayoutKind.Explicit, Pack = 1)]
public readonly struct RpcMetadataService
{
    /// <summary>
    /// 服务版本
    /// </summary>
    [FieldOffset(0)] public readonly int Version;

    /// <summary>
    /// 服务名长度
    /// </summary>
    [FieldOffset(4)] public readonly int NameLength;

    /// <summary>
    /// 服务在字符串池中的相对offset
    /// </summary>
    [FieldOffset(8)] public readonly int NameOffset;

    /// <summary>
    ///  传输类型， 8 bytes, 8 个小组合
    /// </summary>
    [FieldOffset(12)] public readonly long TransportType;

    /// <summary>
    /// 压缩类型
    /// </summary>
    [FieldOffset(12)] public readonly byte CompressionType;

    /// <summary>
    /// 超时时间 0 是永远等待不超时，后续是秒数
    /// </summary>
    [FieldOffset(13)] public readonly byte Timeout;

    [FieldOffset(14)] public readonly byte Reserve1;
    [FieldOffset(15)] public readonly byte Reserve2;
    [FieldOffset(16)] public readonly byte Reserve3;
    [FieldOffset(17)] public readonly byte Reserve4;
    [FieldOffset(18)] public readonly byte Reserve5;
    [FieldOffset(19)] public readonly byte Reserve6;
}

public readonly record struct RpcMetadataWrap(int Version, string ServiceName, long TransportType);

public readonly record struct RpcMetadataTypeMappingWrap(string Source, string Target);