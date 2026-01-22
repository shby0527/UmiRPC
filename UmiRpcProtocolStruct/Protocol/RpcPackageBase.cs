using System.Runtime.InteropServices;

namespace UmiRpcProtocolStruct.Protocol;

public abstract unsafe class RpcPackageBase : IDisposable
{
    protected readonly void* Data;

    private readonly int _size;

    private bool _disposed;

    protected RpcPackageBase(void* data, int size)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        Data = data;
        _size = size;
    }

    protected RpcPackageBase(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        _size = size;
        Data = NativeMemory.Alloc((UIntPtr)_size);
        NativeMemory.Fill(Data, (UIntPtr)_size, 0);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // 释放托管资源
            // 这里没有，就不管了
        }

        NativeMemory.Free(Data);

        _disposed = true;
    }

    public ReadOnlySpan<byte> Memory
    {
        get
        {
            ThrowIfDisposed();
            return new ReadOnlySpan<byte>(Data, _size);
        }
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~RpcPackageBase()
    {
        Dispose(false);
    }
}