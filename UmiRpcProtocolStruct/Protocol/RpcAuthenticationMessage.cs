using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace Umi.Rpc.Protocol;

public sealed unsafe class RpcAuthenticationMessage : RpcPackageBase
{
    // 内存布局 
    // 1 byte LoginType: byte
    // 2 bytes UserNameLength: short
    // 2 bytes PasswordLength: short
    // 4 bytes KeyLength: int
    // UserNameLength bytes UserName: string
    // PasswordLength bytes Password: string
    // KeyLength bytes KeySignedData: byte[]
    // 说明： 其中 LoginType 对应 登陆方式， 0x1 表示 密码登陆， 0x2 表示 密钥登陆
    // 密码登陆时，KeyLength 应该为 0 (不提供Key 对象) 需要 UserName 和 Password
    // 密钥登陆时，PasswordLength: 应该为0 （不提供密码），需要 UserName 和 KeySignedData
    // 但是为了兼容，我们会按字段规定长度读取
    // 密钥登陆时，我们将 {"username": username, "code": {CommonError.Code 低 2 字节作为挑战码(1分钟内有效)} } 进行签名
    // 握手后返回的 RpcCommonError 的 Code 的高2字节，表示服务器支持的登陆类型（0x1 or 0x2 or (0x1|0x2)

    private RpcAuthenticationMessage(void* data, int size)
        : base(data, size)
    {
    }

    public byte LoginType
    {
        get
        {
            ThrowIfDisposed();
            return *(byte*)Data;
        }
    }

    public short UserNameLength
    {
        get
        {
            ThrowIfDisposed();
            return *(short*)((byte*)Data + 1);
        }
    }

    public short PasswordLength
    {
        get
        {
            ThrowIfDisposed();
            return *(short*)((byte*)Data + 3);
        }
    }

    public int KeySignedDataLength
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 5);
        }
    }

    public string UserName
    {
        get
        {
            var userNameLength = UserNameLength;
            if (userNameLength <= 0) return string.Empty;
            ReadOnlySpan<byte> usernameByte = new((byte*)Data + 9, userNameLength);
            return Encoding.UTF8.GetString(usernameByte);
        }
    }

    public string Password
    {
        get
        {
            var length = PasswordLength;
            if (length <= 0) return string.Empty;
            ReadOnlySpan<byte> span = new((byte*)Data + 9 + UserNameLength, length);
            return Encoding.UTF8.GetString(span);
        }
    }

    public ReadOnlySpan<byte> KeySignedData
    {
        get
        {
            var length = KeySignedDataLength;
            if (length <= 0) return ReadOnlySpan<byte>.Empty;
            return new ReadOnlySpan<byte>((byte*)Data + 9 + UserNameLength + PasswordLength, length);
        }
    }

    public static RpcAuthenticationMessage CreateFromMemory(scoped in ReadOnlySpan<byte> data)
    {
        if (data.Length < 9) throw new ArgumentOutOfRangeException(nameof(data));
        fixed (byte* pd = data)
        {
            var ul = *(short*)(pd + 1);
            var pl = *(short*)(pd + 3);
            var kl = *(int*)(pd + 5);
            if (ul < 0 || pl < 0 || kl < 0 || data.Length < 9 + ul + pl + kl)
                throw new ArgumentOutOfRangeException(nameof(data));

            var buffer = NativeMemory.Alloc((UIntPtr)(9 + ul + pl + kl));
            data.CopyTo(new Span<byte>(buffer, 9 + ul + pl + kl));
            return new RpcAuthenticationMessage(buffer, 9 + ul + pl + kl);
        }
    }

    public static RpcAuthenticationMessage CreateFromMemory(scoped in ReadOnlySequence<byte> data)
    {
        if (data.Length < 9) throw new ArgumentOutOfRangeException(nameof(data));
        Span<byte> span = stackalloc byte[8];
        data.Slice(1, 8).CopyTo(span);
        fixed (byte* pd = span)
        {
            var ul = *(short*)(pd);
            var pl = *(short*)(pd + 2);
            var kl = *(int*)(pd + 4);
            if (ul < 0 || pl < 0 || kl < 0 || data.Length < 9 + ul + pl + kl)
                throw new ArgumentOutOfRangeException(nameof(data));

            var buffer = NativeMemory.Alloc((UIntPtr)(9 + ul + pl + kl));
            data.CopyTo(new Span<byte>(buffer, 9 + ul + pl + kl));
            return new RpcAuthenticationMessage(buffer, 9 + ul + pl + kl);
        }
    }

    public static RpcAuthenticationMessage CreateFromMessage(
        byte loginType,
        string username, string? password,
        scoped in ReadOnlySpan<byte> keySignedData)
    {
        var length = 9; // 基础长度
        if (string.IsNullOrEmpty(username))
            throw new ArgumentException($"{username} MUST not be empty or null", nameof(username));
        ReadOnlySpan<byte> ud = Encoding.UTF8.GetBytes(username);
        var pd = string.IsNullOrEmpty(password)
            ? ReadOnlySpan<byte>.Empty
            : Encoding.UTF8.GetBytes(password);
        length += ud.Length + pd.Length + keySignedData.Length;
        var buffer = NativeMemory.Alloc((UIntPtr)length);
        *(byte*)buffer = loginType;
        *(short*)((byte*)buffer + 1) = (short)ud.Length;
        *(short*)((byte*)buffer + 3) = (short)pd.Length;
        *(int*)((byte*)buffer + 5) = keySignedData.Length;
        var section = new Span<byte>((byte*)buffer + 9, ud.Length + pd.Length + keySignedData.Length);
        ud.CopyTo(section);
        pd.CopyTo(section[ud.Length..]);
        keySignedData.CopyTo(section[(ud.Length + pd.Length)..]);
        return new RpcAuthenticationMessage(buffer, 9 + ud.Length + pd.Length + keySignedData.Length);
    }
}