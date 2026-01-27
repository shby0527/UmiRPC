using System.IO.Pipelines;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Client;

namespace Umi.Rpc.Server.Executors;

public interface IServerExecutor
{
    ValueTask<ExecuteResult> ExecuteCommandAsync(RpcBasic basic, PipeReader reader);
}

public readonly struct ExecuteResult
{
    public RpcPackageBase? Package { get; init; }

    public required uint ResultCommand { get; init; }

    public required bool CloseConnection { get; init; }

    public required ClientState NextState { get; init; }
}