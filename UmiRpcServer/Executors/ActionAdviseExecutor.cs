using System.IO.Pipelines;
using Umi.Rpc.Protocol;

namespace Umi.Rpc.Server.Executors;

public sealed class ActionAdviseExecutor(IServerExecutor executor) : IServerExecutor
{
    public event EventHandler? BeforeExecute;

    public event EventHandler? AfterExecute;

    public async ValueTask<ExecuteResult> ExecuteCommandAsync(RpcBasic basic, PipeReader reader)
    {
        BeforeExecute?.Invoke(this, EventArgs.Empty);
        var result = await executor.ExecuteCommandAsync(basic, reader);
        AfterExecute?.Invoke(this, EventArgs.Empty);
        return result;
    }
}