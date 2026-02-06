namespace Umi.Rpc.Server.Services;

public interface IMethodExecService
{
    long GenerateTransactionId(scoped ReadOnlySpan<byte> session);

    Task<object> InvokeMethodAsync(scoped ReadOnlySpan<byte> session,
        scoped in MethodResolveParameters resolveParameters,
        object[] parameters);

    Func<ReadOnlySpan<byte>, object> ResolveParameter(scoped ReadOnlySpan<byte> session);

    ReadOnlySpan<byte> SerializeResult(scoped ReadOnlySpan<byte> session, object result);
}

public readonly record struct MethodResolveParameters(
    string ServiceName,
    string[] ServiceGenericArguments,
    string MethodName,
    string[] MethodGenericArguments);