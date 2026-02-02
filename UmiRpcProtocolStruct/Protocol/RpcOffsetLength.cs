using System.Runtime.InteropServices;

namespace Umi.Rpc.Protocol;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct RpcOffsetLength
{
    public readonly int Offset;

    public readonly int Length;
}