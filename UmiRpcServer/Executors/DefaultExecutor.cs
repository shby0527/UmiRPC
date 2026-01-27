using System.IO.Pipelines;
using Umi.Rpc.Protocol;

namespace Umi.Rpc.Server.Executors;

/// <summary>
/// 只是消耗协议规定的数据包，没有额外处理
/// </summary>
public sealed class DefaultExecutor : IServerExecutor
{
    public async ValueTask<ExecuteResult> ExecuteCommandAsync(RpcBasic basic, PipeReader reader)
    {
        if (basic.Length < 0) return default;
        var result = await reader.ReadAtLeastAsync(basic.Length);
        if (result.IsCompleted || result.IsCanceled) return default;
        var position = result.Buffer.GetPosition(basic.Length);
        reader.AdvanceTo(position);
        return default;
    }
}