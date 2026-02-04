using System.Runtime.InteropServices;

namespace Umi.Rpc.Protocol;

/// <summary>
/// Exception 的Message 包
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly ref struct RpcExceptionMessage
{
    /***********************************
     * ExceptionId                  long                        8 bytes                        异常ID
     * StringPoolOffset             int                         4 bytes                        字符串池偏移
     * ExceptionName                OffsetLength                8 bytes                        异常名（Offset + Length)
     * ExceptionMessage             OffsetLength                8 bytes                        异常消息
     * StringPool                   byte                        variable
     **********************************/
    public readonly long ExceptionId;

    public readonly int StringPoolOffset;

    public readonly RpcOffsetLength ExceptionName;

    public readonly RpcOffsetLength ExceptionMessage;
}