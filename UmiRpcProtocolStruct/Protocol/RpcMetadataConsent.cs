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

    /// <summary>
    /// 那个字符串池的直接大小
    /// </summary>
    public int StringPoolLength
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 8);
        }
    }

    /// <summary>
    /// 这是那个数组
    /// </summary>
    public ReadOnlySpan<RpcMetadataService> RpcMetadataServices => new((byte*)Data + 12, ServiceArrayLength);

    /// <summary>
    /// 字符串池
    /// </summary>
    public ReadOnlySpan<byte> StringPool =>
        new((byte*)Data + 12 + ServiceArrayLength * sizeof(RpcMetadataService), StringPoolLength);


    public string GetString(int offset, int length)
    {
        ThrowIfDisposed();
        if (offset > StringPoolLength || offset + length > StringPoolLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var pool = (byte*)Data + 12 + ServiceArrayLength * sizeof(RpcMetadataService);
        return Encoding.ASCII.GetString(pool + offset, length);
    }


    public static RpcMetadataConsent CreateFromMemory(scoped in ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            throw new ArgumentOutOfRangeException(nameof(data));
        }

        fixed (byte* ptr = data)
        {
            var totalLength = *(int*)(ptr + 4) * sizeof(RpcMetadataService) + *(int*)(ptr + 8) + 12;
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
        if (data.Length < 12)
        {
            throw new ArgumentOutOfRangeException(nameof(data));
        }

        Span<byte> header = stackalloc byte[12];
        data.Slice(0, 12).CopyTo(header);
        fixed (byte* ptr = header)
        {
            var totalLength = *(int*)(ptr + 4) * sizeof(RpcMetadataService) + *(int*)(ptr + 8) + 12;
            if (data.Length < totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(data));
            }

            var buffer = NativeMemory.Alloc((UIntPtr)totalLength);
            data.CopyTo(new Span<byte>(buffer, totalLength));
            return new RpcMetadataConsent(buffer, totalLength);
        }
    }

    public static RpcMetadataConsent CreateFromMessage(uint serialization, RpcMetadataWrap[] metadata)
    {
        if (metadata.Length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata));
        }

        var nameData = metadata
            .Select(p => p.ServiceName)
            .Distinct()
            .ToImmutableDictionary(p => p,
                p => Encoding.ASCII.GetBytes(p));
        // ?? 长度？ 12 + metadata.Length * sizeof(RpcMetadataService) + sum(nameData)
        var stringPoolLen = nameData.Sum(p => p.Value.Length);
        var totalLength = 12 + metadata.Length * sizeof(RpcMetadataService) + stringPoolLen;
        var buffer = (byte*)NativeMemory.Alloc((UIntPtr)totalLength);
        *(uint*)buffer = serialization;
        *(int*)(buffer + 4) = metadata.Length;
        *(int*)(buffer + 8) = stringPoolLen;
        var offset = 0;
        var stringPool = buffer + 12 + metadata.Length * sizeof(RpcMetadataService);
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
            *(int*)(buffer + 12 + i * sizeof(RpcMetadataService)) = metadata[i].Version;
            *(int*)(buffer + 12 + i * sizeof(RpcMetadataService) + 4) = ol.Length;
            *(int*)(buffer + 12 + i * sizeof(RpcMetadataService) + 8) = ol.Offset;
        }

        return new RpcMetadataConsent(buffer, totalLength);
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct RpcMetadataService(int version)
{
    /// <summary>
    /// 服务版本
    /// </summary>
    public readonly int Version = version;

    /// <summary>
    /// 服务名长度
    /// </summary>
    public readonly int NameLength;

    /// <summary>
    /// 服务在字符串池中的相对offset
    /// </summary>
    public readonly int NameOffset;
}

public readonly record struct RpcMetadataWrap(int Version, string ServiceName);