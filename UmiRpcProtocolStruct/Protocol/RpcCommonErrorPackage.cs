using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace UmiRpcProtocolStruct.Protocol;

public sealed unsafe class RpcCommonErrorPackage : RpcPackageBase
{
    // 内存布局
    // 4 bytes ErrorCode:int
    // 4 bytes MessageLength:int
    // MessageLength bytes  Message:string
    private RpcCommonErrorPackage(void* data, int size)
        : base(data, size)
    {
    }

    public int ErrorCode
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)Data;
        }
    }

    public int MessageLength
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 4);
        }
    }

    public bool IsSuccess => (ErrorCode & 0x80_00_00_00) == 0;

    public int Code => ErrorCode & 0x7F_FF_FF_FF;

    public string Message => Encoding.UTF8.GetString(new ReadOnlySpan<byte>((byte*)Data + 8, MessageLength));

    public static RpcCommonErrorPackage CreateFromMemory(scoped in ReadOnlySpan<byte> data)
    {
        if (data.Length < 8) throw new ArgumentOutOfRangeException(nameof(data));
        fixed (byte* pd = data)
        {
            var l = *(int*)(pd + 4);
            if (data.Length < l + 8) throw new ArgumentOutOfRangeException(nameof(data));
            var buffer = NativeMemory.Alloc((UIntPtr)(l + 8));
            data.CopyTo(new Span<byte>(buffer, l + 8));
            return new RpcCommonErrorPackage(buffer, l + 8);
        }
    }

    public static RpcCommonErrorPackage CreateFromMemory(scoped in ReadOnlySequence<byte> data)
    {
        if (data.Length < 8) throw new ArgumentOutOfRangeException(nameof(data));
        Span<byte> tmp = stackalloc byte[4];
        data.Slice(4, 4).CopyTo(tmp);
        fixed (byte* pl = tmp)
        {
            var l = *(int*)pl;
            if (data.Length < l + 8) throw new ArgumentOutOfRangeException(nameof(data));
            var buffer = NativeMemory.Alloc((UIntPtr)(l + 8));
            data.CopyTo(new Span<byte>(buffer, l + 8));
            return new RpcCommonErrorPackage(buffer, l + 8);
        }
    }

    public static RpcCommonErrorPackage CreateFromMessage(int errorCode, string message)
    {
        ReadOnlySpan<byte> bytes = Encoding.UTF8.GetBytes(message);
        var buffer = NativeMemory.Alloc((UIntPtr)(8 + bytes.Length));
        *(int*)buffer = errorCode;
        *(int*)((byte*)buffer + 4) = bytes.Length;
        bytes.CopyTo(new Span<byte>((byte*)buffer + 8, bytes.Length));
        return new RpcCommonErrorPackage(buffer, 8 + bytes.Length);
    }
}