using System.IO.Pipelines;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Services;
using Umi.Rpc.Server.Client;

namespace Umi.Rpc.Server.Executors;

internal sealed class HandshakeContinueExecutor(IAuthenticationService authenticationService) : IServerExecutor
{
    public async ValueTask<ExecuteResult> ExecuteCommandAsync(RpcBasic basic, PipeReader reader)
    {
        // 理论上来说，应该没有payload, 如果有payload ，说明包有问题,直接丢弃多余内容
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

        if (!authenticationService.NeedsAuthentication)
        {
            // 不需要认证，我们就跳过认证步骤 直接  idle
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.HANDSHAKE_CONTINUE_ACK,
                CloseConnection = false,
                NextState = ClientState.Idle,
                Package = null
            };
        }

        if (authenticationService.SessionCheck(basic.Session))
        {
            // 对于这个命令，这意味着需要复用session
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.HANDSHAKE_CONTINUE_ACK,
                CloseConnection = false,
                NextState = ClientState.Idle,
                Package = null
            };
        }

        return new ExecuteResult
        {
            ResultCommand = UmiRpcConstants.COMMON_ERROR,
            CloseConnection = true,
            NextState = ClientState.Closed,
            Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.SESSION_ERROR, "Session Error")
        };
    }
}