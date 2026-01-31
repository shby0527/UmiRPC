using System.IO.Pipelines;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Client;
using Umi.Rpc.Server.Services;

namespace Umi.Rpc.Server.Executors;

public sealed class SessionCloseExecutor(ISessionService service) : IServerExecutor
{
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

        if (service.InvalidateSession(basic.Session))
        {
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.SESSION_CLOSE_ACK,
                CloseConnection = true,
                NextState = ClientState.Closed
            };
        }

        return new ExecuteResult
        {
            ResultCommand = UmiRpcConstants.COMMON_ERROR,
            CloseConnection = false,
            NextState = ClientState.Idle,
            Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.SESSION_ERROR, "Unknown Session")
        };
    }
}