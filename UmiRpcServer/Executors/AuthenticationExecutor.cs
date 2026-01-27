using System.IO.Pipelines;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Services;
using Umi.Rpc.Server.Client;

namespace Umi.Rpc.Server.Executors;

public sealed class AuthenticationExecutor(IAuthenticationService service) : IServerExecutor
{
    public async ValueTask<ExecuteResult> ExecuteCommandAsync(RpcBasic basic, PipeReader reader)
    {
        // 这里应该要有一个 Payload ，如果没有，就是错误包，应该返回通用错误并保持原样
        if (basic.Length <= 0)
        {
            return new ExecuteResult()
            {
                CloseConnection = false,
                ResultCommand = UmiRpcConstants.AUTHENTICATION_RESULT,
                NextState = ClientState.Authentication,
                Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.AUTHENTICATION_REQUIRED, "payload required")
            };
        }

        // 开始认证流程
        var result = await reader.ReadAtLeastAsync(basic.Length);
        if (result.IsCanceled || result.IsCompleted)
        {
            return new ExecuteResult()
            {
                ResultCommand = UmiRpcConstants.COMMON_ERROR,
                CloseConnection = true,
                NextState = ClientState.Closed,
                Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.CONNECTION_CLOSED, "Connection Closed")
            };
        }

        using var auth = RpcAuthenticationMessage.CreateFromMemory(result.Buffer);
        var position = result.Buffer.GetPosition(basic.Length);
        reader.AdvanceTo(position);
        if ((auth.LoginType & RpcAuthenticationMessage.LOGIN_PASSWORD) == RpcAuthenticationMessage.LOGIN_PASSWORD
            && service.PasswordAuthenticationEnabled)
        {
            // 密码登陆
            return service.Authenticate(auth.UserName, auth.Password, basic.Session)
                ? new ExecuteResult
                {
                    ResultCommand = UmiRpcConstants.AUTHENTICATION_RESULT,
                    CloseConnection = false,
                    NextState = ClientState.MetadataConsent,
                    Package = RpcCommonError.CreateFromMessage(0, "Success")
                }
                : new ExecuteResult
                {
                    ResultCommand = UmiRpcConstants.AUTHENTICATION_RESULT,
                    CloseConnection = false,
                    NextState = ClientState.Authentication,
                    Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.AUTHENTICATION_FAILURE,
                        "Username or password is incorrect")
                };
        }

        if ((auth.LoginType & RpcAuthenticationMessage.LOGIN_KEY_SIGNED) == RpcAuthenticationMessage.LOGIN_KEY_SIGNED
            && service.KeyAuthenticationEnabled)
        {
            return service.Authenticate(auth.UserName, auth.KeySignedData, basic.Session)
                ? new ExecuteResult
                {
                    ResultCommand = UmiRpcConstants.AUTHENTICATION_RESULT,
                    CloseConnection = false,
                    NextState = ClientState.MetadataConsent,
                    Package = RpcCommonError.CreateFromMessage(0, "Success")
                }
                : new ExecuteResult
                {
                    ResultCommand = UmiRpcConstants.AUTHENTICATION_RESULT,
                    CloseConnection = false,
                    NextState = ClientState.Authentication,
                    Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.AUTHENTICATION_FAILURE,
                        "Username or signed data is incorrect")
                };
        }

        return new ExecuteResult
        {
            ResultCommand = UmiRpcConstants.AUTHENTICATION_RESULT,
            CloseConnection = false,
            NextState = ClientState.Authentication,
            Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.AUTHENTICATION_FAILURE,
                "Unknown authentication type")
        };
    }
}