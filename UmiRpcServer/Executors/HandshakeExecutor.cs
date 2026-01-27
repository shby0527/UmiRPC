using System.IO.Pipelines;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Authentications;
using Umi.Rpc.Server.Client;

namespace Umi.Rpc.Server.Executors;

internal sealed class HandshakeExecutor(IAuthenticationService authenticationService) : IServerExecutor
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
            // 不需要认证，我们就跳过认证步骤 直接  协商元数据
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.HANDSHAKE_RESULT,
                CloseConnection = false,
                NextState = ClientState.MetadataConsent,
                Package = RpcCommonError.CreateFromMessage(0, "Success")
            };
        }

        if (authenticationService.SessionCheck(basic.Session))
        {
            // session 错误，要求重新握手
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.HANDSHAKE_RESULT,
                CloseConnection = false,
                NextState = ClientState.Handshake,
                Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.SESSION_CONFLICT, "Session Conflicted")
            };
        }

        // 需要认证
        var code = UmiRpcConstants.NEED_AUTHENTICATION;
        if (authenticationService.PasswordAuthenticationEnabled)
            code |= RpcAuthenticationMessage.LOGIN_PASSWORD << 16;
        if (authenticationService.KeyAuthenticationEnabled)
            code |= (RpcAuthenticationMessage.LOGIN_KEY_SIGNED << 16) | authenticationService.GenerateChallengeCode();
        return new ExecuteResult
        {
            ResultCommand = UmiRpcConstants.HANDSHAKE_RESULT,
            CloseConnection = false,
            NextState = ClientState.Authentication,
            Package = RpcCommonError.CreateFromMessage(code, "Need Authentication")
        };
    }
}