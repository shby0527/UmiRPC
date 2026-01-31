using System.IO.Pipelines;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Client;
using Umi.Rpc.Server.Services;

namespace Umi.Rpc.Server.Executors;

public class PingExecutor(ISessionService service) : IServerExecutor
{
    public event EventHandler? Ping;

    public async ValueTask<ExecuteResult> ExecuteCommandAsync(RpcBasic basic, PipeReader reader)
    {
        // ping 包理论没有payload
        if (basic.Length > 0)
        {
            var result = await reader.ReadAtLeastAsync(basic.Length);
            if (result.IsCanceled || result.IsCompleted)
            {
                return new ExecuteResult
                {
                    ResultCommand = UmiRpcConstants.COMMON_ERROR,
                    CloseConnection = true,
                    NextState = ClientState.Closed,
                    Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.CONNECTION_CLOSED, "Connection Closed")
                };
            }

            var position = result.Buffer.GetPosition(basic.Length);
            reader.AdvanceTo(position);
        }

        // 是一个ping 包，先校验一下 session 
        if (!service.CheckSession(basic.Session))
        {
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.COMMON_ERROR,
                CloseConnection = true,
                NextState = ClientState.Closed,
                Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.SESSION_EXPIRED, "Session Expired")
            };
        }

        Ping?.Invoke(this, EventArgs.Empty);
        return new ExecuteResult
        {
            ResultCommand = UmiRpcConstants.PONG,
            CloseConnection = false,
            NextState = ClientState.Idle
        };
    }
}