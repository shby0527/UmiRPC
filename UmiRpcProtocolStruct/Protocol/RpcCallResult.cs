using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace Umi.Rpc.Protocol;

/// <summary>
/// Rpc Call Result
/// 这里不需要提供类型，只需要提供数据
/// </summary>
public sealed unsafe class RpcCallResult : RpcPackageBase
{
    /******************************************
     * 当 为了兼容异步方法，我们可以这样
     * 这里可以设计一个全局唯一的ID(可以是一个类似的递增ID或者别的）
     * 如果方法同步，那么将会同返回值一同返回
     *****************************************/

    /****************************************
     * 内存布局
     * TransactionId                                 long                          8 bytes                    事务ID
     * CallStatus                                    byte                          1 byte                     状态
     * ResultLength                                  int                           4 bytes                    Result 的长度
     * Result                                        Data                          variable                   结果
     * 依据 CallStatus 不同，Result 不同
     * Padding: Result 为 0 byte
     * Completed: Result 为 方法返回值的序列化（如果为 void 那么也是 0 byte)
     * Failure 时 为 Exception 的序列化(参见 RpcExceptionMessage)
     * Canceled 时 为 0 byte
     ***************************************/
    private RpcCallResult(void* data, int size)
        : base(data, size)
    {
    }

    public long TransactionId
    {
        get
        {
            ThrowIfDisposed();
            return *(long*)Data;
        }
    }

    public RpcCallResultStatus Status
    {
        get
        {
            ThrowIfDisposed();
            return *(RpcCallResultStatus*)((byte*)Data + 8);
        }
    }

    public int ResultLength
    {
        get
        {
            ThrowIfDisposed();
            return *(int*)((byte*)Data + 8 + sizeof(RpcCallResultStatus));
        }
    }

    public ReadOnlySpan<byte> Result => new((byte*)Data + 12 + sizeof(RpcCallResultStatus), ResultLength);

    public ref RpcExceptionMessage GetExceptionMessage()
    {
        ThrowIfDisposed();
        if (Status != RpcCallResultStatus.Failure)
        {
            throw new InvalidOperationException("status not failure, no error message");
        }

        if (ResultLength < sizeof(RpcExceptionMessage))
        {
            throw new ArgumentOutOfRangeException(nameof(ResultLength), "length is less than Exception Message");
        }

        return ref *(RpcExceptionMessage*)((byte*)Data + 12 + sizeof(RpcCallResultStatus));
    }

    public string GetExceptionString(scoped in RpcOffsetLength ol)
    {
        ThrowIfDisposed();
        if (Status != RpcCallResultStatus.Failure)
        {
            throw new InvalidOperationException("status not failure, no error message");
        }

        if (ResultLength < sizeof(RpcExceptionMessage) + ol.Offset + ol.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(ResultLength), "length is less than Exception Message");
        }

        var stringPool = (byte*)Data + 12 + sizeof(RpcCallResultStatus) + sizeof(RpcExceptionMessage);
        return Encoding.UTF8.GetString(stringPool + ol.Offset, ol.Length);
    }

    public static RpcCallResult CreateFromMemory(scoped in ReadOnlySpan<byte> memory)
    {
        const int header = 12 + sizeof(RpcCallResultStatus);
        if (memory.Length < header)
        {
            throw new ArgumentOutOfRangeException(nameof(memory), "memory length is less than header");
        }

        fixed (byte* ptr = memory)
        {
            var resultLength = *(int*)(ptr + 8 + sizeof(RpcCallResultStatus));
            var totalLength = header + resultLength;
            if (memory.Length < totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "memory length is less than header");
            }

            var buffer = NativeMemory.Alloc((nuint)totalLength);
            NativeMemory.Copy(ptr, buffer, (nuint)totalLength);
            return new RpcCallResult(buffer, totalLength);
        }
    }

    public static RpcCallResult CreateFromMemory(scoped in ReadOnlySequence<byte> memory)
    {
        if (memory.Length < 12 + sizeof(RpcCallResultStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(memory), "memory length is less than header");
        }

        Span<byte> header = stackalloc byte[12 + sizeof(RpcCallResultStatus)];
        memory.Slice(0, 12 + sizeof(RpcCallResultStatus)).CopyTo(header);
        fixed (byte* ptr = header)
        {
            var resultLength = *(int*)(ptr + 8 + sizeof(RpcCallResultStatus));
            var totalLength = 12 + sizeof(RpcCallResultStatus) + resultLength;
            if (memory.Length < totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(memory), "memory length is less than header");
            }

            var buffer = NativeMemory.Alloc((nuint)totalLength);
            memory.Slice(0, totalLength).CopyTo(new Span<byte>(buffer, totalLength));
            return new RpcCallResult(buffer, totalLength);
        }
    }

    public static RpcCallResult CreateFromMessage(long transactionId,
        RpcCallResultStatus status,
        scoped in ReadOnlySpan<byte> result)
    {
        var totalLength = 12 + sizeof(RpcCallResultStatus) + result.Length;
        var buffer = (byte*)NativeMemory.Alloc((nuint)totalLength);
        *(long*)buffer = transactionId;
        *(RpcCallResultStatus*)(buffer + 8) = status;
        *(int*)(buffer + 8 + sizeof(RpcCallResultStatus)) = result.Length;
        result.CopyTo(new Span<byte>(buffer + 12 + sizeof(RpcCallResultStatus), result.Length));
        return new RpcCallResult(buffer, totalLength);
    }

    public static RpcCallResult CreateFromExceptionMessage(long transactionId, long exceptionId,
        string exName, string exMessage)
    {
        ReadOnlySpan<byte> nameData = Encoding.UTF8.GetBytes(exName);
        ReadOnlySpan<byte> messageData = Encoding.UTF8.GetBytes(exMessage);
        using var memory = MemoryPool<byte>.Shared.Rent(sizeof(RpcExceptionMessage)
                                                        + nameData.Length
                                                        + messageData.Length);
        fixed (byte* ptr = memory.Memory.Span)
        {
            *(long*)ptr = exceptionId;
            *(int*)(ptr + 8) = sizeof(RpcExceptionMessage);
            (*(int*)(ptr + 12), *(int*)(ptr + 16)) = (0, nameData.Length);
            (*(int*)(ptr + 20), *(int*)(ptr + 24)) = (nameData.Length, messageData.Length);
        }

        var strPool = memory.Memory.Span[sizeof(RpcExceptionMessage)..];
        nameData.CopyTo(strPool);
        messageData.CopyTo(strPool[nameData.Length..]);
        return CreateFromMessage(transactionId, RpcCallResultStatus.Failure, memory.Memory.Span);
    }
}

public enum RpcCallResultStatus : byte
{
    /// <summary>
    /// 处理中
    /// </summary>
    Padding = 1,

    /// <summary>
    /// 完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 失败
    /// </summary>
    Failure = 3,

    /// <summary>
    /// 取消
    /// </summary>
    Canceled = 4,
}