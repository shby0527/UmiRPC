namespace Umi.Rpc.Server.Exceptions;

public sealed class ProtocolCommandConflictException : SystemException
{
    public ProtocolCommandConflictException()
    {
    }

    public ProtocolCommandConflictException(string message)
        : base(message)
    {
    }
}