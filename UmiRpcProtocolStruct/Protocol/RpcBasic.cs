using System.Buffers;
using System.Runtime.InteropServices;
using Umi.Rpc.Base;

namespace Umi.Rpc.Protocol;

public sealed unsafe class RpcBasic : RpcPackageBase
{
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once InconsistentNaming
    public const int SIZE_OF_PACKAGE = 33;

    // 4 bytes    Magic: uint
    // 1 byte     Version: byte
    // 4 bytes    Command: uint
    // 20 bytes   Session: byte[]
    // 4 bytes    Length: int  这是 Payload 数据长度
    // total = 33 bytes

    private RpcBasic(void* data)
        : base(data, SIZE_OF_PACKAGE)
    {
    }

    private RpcBasic()
        : base(SIZE_OF_PACKAGE)
    {
    }

    public uint Magic
    {
        get
        {
            ThrowIfDisposed();
            return *(uint*)Data;
        }
        private set
        {
            ThrowIfDisposed();
            (*(uint*)Data) = value;
        }
    }

    public byte Version
    {
        get
        {
            ThrowIfDisposed();
            return *((byte*)Data + 4);
        }
        private set
        {
            ThrowIfDisposed();
            *((byte*)Data + 4) = value;
        }
    }

    public uint Command
    {
        get
        {
            ThrowIfDisposed();
            return *(uint*)((byte*)Data + 5);
        }
        private set
        {
            ThrowIfDisposed();
            (*(uint*)((byte*)Data + 5)) = value;
        }
    }

    /// <summary>
    /// 服务端回复的包里，这个值不重要，它保持0或者任意值
    /// </summary>
    public Span<byte> Session
    {
        get
        {
            ThrowIfDisposed();
            return new Span<byte>((byte*)Data + 9, 20);
        }
    }

    public int Length
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 29);
        }
        set
        {
            ThrowIfDisposed();
            (*(int*)((byte*)Data + 29)) = value;
        }
    }

    public static RpcBasic CreateFromMemory(scoped in ReadOnlySequence<byte> data)
    {
        if (data.Length < SIZE_OF_PACKAGE) throw new ArgumentOutOfRangeException(nameof(data));
        var buffer = NativeMemory.Alloc(SIZE_OF_PACKAGE);
        Span<byte> span = new(buffer, SIZE_OF_PACKAGE);
        data.CopyTo(span);
        return new RpcBasic(buffer);
    }

    public static RpcBasic CreateFromMemory(scoped in ReadOnlySpan<byte> data)
    {
        if (data.Length < SIZE_OF_PACKAGE) throw new IndexOutOfRangeException(nameof(data));
        var buffer = NativeMemory.Alloc(SIZE_OF_PACKAGE);
        Span<byte> span = new(buffer, SIZE_OF_PACKAGE);
        data.CopyTo(span);
        return new RpcBasic(buffer);
    }

    public static RpcBasic CreateFromMessage(uint command)
    {
        return new RpcBasic()
        {
            Magic = UmiRpcConstants.MAGIC,
            Version = UmiRpcConstants.VERSION,
            Command = command
        };
    }
}