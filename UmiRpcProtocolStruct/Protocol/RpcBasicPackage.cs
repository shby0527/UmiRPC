using System.Buffers;
using System.Runtime.InteropServices;

namespace UmiRpcProtocolStruct.Protocol;

public sealed unsafe class RpcBasicPackage : RpcPackageBase
{
    public const int SIZE_OF_PACKAGE = 33;

    // 4 bytes   uint Magic
    // 1 byte    byte Version
    // 4 bytes   uint Command
    // 20 bytes  byte[] Session
    // 4 bytes   int Length
    // total = 33 bytes

    private RpcBasicPackage(void* data)
        : base(data, SIZE_OF_PACKAGE)
    {
    }

    public RpcBasicPackage()
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
        set
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
        set
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
        set
        {
            ThrowIfDisposed();
            (*(uint*)((byte*)Data + 5)) = value;
        }
    }

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

    public static RpcBasicPackage CreateFromMemory(scoped in ReadOnlySequence<byte> data)
    {
        if (data.Length < SIZE_OF_PACKAGE) throw new ArgumentOutOfRangeException(nameof(data));
        var buffer = NativeMemory.Alloc(SIZE_OF_PACKAGE);
        Span<byte> span = new(buffer, SIZE_OF_PACKAGE);
        data.CopyTo(span);
        return new RpcBasicPackage(buffer);
    }

    public static RpcBasicPackage CreateFromMemory(scoped in ReadOnlySpan<byte> data)
    {
        if (data.Length < SIZE_OF_PACKAGE) throw new IndexOutOfRangeException(nameof(data));
        var buffer = NativeMemory.Alloc(SIZE_OF_PACKAGE);
        Span<byte> span = new(buffer, SIZE_OF_PACKAGE);
        data.CopyTo(span);
        return new RpcBasicPackage(buffer);
    }
}