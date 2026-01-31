using System.Buffers;
using System.IO.Pipelines;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Client;
using Umi.Rpc.Server.Services;

namespace Umi.Rpc.Server.Executors;

public sealed class SessionRefreshExecutor(ISessionService service) : IServerExecutor
{
    public async ValueTask<ExecuteResult> ExecuteCommandAsync(RpcBasic basic, PipeReader reader)
    {
        // Refresh 包必须要有payload ，没有就是异常包
        if (basic.Length <= 0)
        {
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.COMMON_ERROR,
                CloseConnection = false,
                NextState = ClientState.Idle,
                Package = RpcCommonError.CreateFromMessage(
                    UmiRpcConstants.SESSION_NEW_SESSION_REQUIRED,
                    "New Session Required")
            };
        }

        var result = await reader.ReadAtLeastAsync(basic.Length);
        if (basic.Length < 20)
        {
            // 异常包
            var position = result.Buffer.GetPosition(basic.Length);
            reader.AdvanceTo(position);
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.COMMON_ERROR,
                CloseConnection = false,
                NextState = ClientState.Idle,
                Package = RpcCommonError.CreateFromMessage(
                    UmiRpcConstants.SESSION_NEW_SESSION_REQUIRED,
                    "New Session Format Error")
            };
        }

        Span<byte> newSession = stackalloc byte[20];
        result.Buffer.Slice(0, 20).CopyTo(newSession);
        reader.AdvanceTo(result.Buffer.GetPosition(basic.Length));
        if (service.Refresh(basic.Session, newSession))
        {
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.SESSION_REFRESH_ACK,
                CloseConnection = false,
                NextState = ClientState.Idle
            };
        }

        return new ExecuteResult
        {
            ResultCommand = UmiRpcConstants.COMMON_ERROR,
            CloseConnection = false,
            NextState = ClientState.Idle,
            Package = RpcCommonError.CreateFromMessage(
                UmiRpcConstants.SESSION_NEW_SESSION_REQUIRED,
                "New Session Format Error")
        };
    }
}