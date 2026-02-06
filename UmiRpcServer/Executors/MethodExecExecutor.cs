using System.IO.Pipelines;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Client;
using Umi.Rpc.Server.Services;

namespace Umi.Rpc.Server.Executors;

public sealed class MethodExecExecutor(IMethodExecService service) : IServerExecutor
{
    public event EventHandler<ExecuteResultEventArgs>? Executed;

    public async ValueTask<ExecuteResult> ExecuteCommandAsync(RpcBasic basic, PipeReader reader)
    {
        // 这个包应该存在payload
        if (basic.Length <= 0)
        {
            return new ExecuteResult
            {
                CloseConnection = false,
                ResultCommand = UmiRpcConstants.COMMON_ERROR,
                NextState = ClientState.UnChanged,
                Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.METADATA_CONSENT_FAILURE, "payload required")
            };
        }

        var result = await reader.ReadAtLeastAsync(basic.Length);
        if (result is { IsCanceled: true } or { IsCompleted: true })
        {
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.COMMON_ERROR,
                CloseConnection = true,
                NextState = ClientState.Closed,
                Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.CONNECTION_CLOSED, "Connection Closed")
            };
        }

        using var message = RpcCallMessage.CreateFromMemory(result.Buffer);
        var position = result.Buffer.GetPosition(basic.Length);
        reader.AdvanceTo(position);
        // 这里需要保证效率，异步执行，我们可以不存在Executing 状态，所以我们可以不改变Idle状态
        var parameters = new object[message.ArgumentCount];
        var resolver = service.ResolveParameter(basic.Session);
        for (var i = 0; i < message.ArgumentCount; i++)
        {
            parameters[i] = resolver(message.GetObject(message.Arguments[i]));
        }

        var serviceGenericTypes = new string[message.ServiceGenericTypeCount];
        for (var i = 0; i < message.ServiceGenericTypeCount; i++)
        {
            serviceGenericTypes[i] = message.GetString(message.ServiceGenericType[i]);
        }

        var methodGenericTypes = new string[message.MethodGenericTypeCount];
        for (var i = 0; i < message.MethodGenericTypeCount; i++)
        {
            methodGenericTypes[i] = message.GetString(message.MethodGenericType[i]);
        }

        var transactionId = service.GenerateTransactionId(basic.Session);
        var task = service.InvokeMethodAsync(basic.Session,
            new MethodResolveParameters(message.GetString(message.ServiceName),
                serviceGenericTypes,
                message.GetString(message.MethodName),
                methodGenericTypes),
            parameters);
        if (task.IsFaulted)
        {
            // 不触发事件，同步触发
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.CALL_RESULT,
                CloseConnection = false,
                NextState = ClientState.Idle,
                Package = RpcCallResult.CreateFromExceptionMessage(transactionId,
                    task.Exception.HResult, task.Exception.ToString(), task.Exception.Message)
            };
        }

        if (task.IsCompletedSuccessfully)
        {
            var callResult = service.SerializeResult(basic.Session, task.Result);
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.CALL_RESULT,
                CloseConnection = false,
                NextState = ClientState.Idle,
                Package = RpcCallResult.CreateFromMessage(transactionId, RpcCallResultStatus.Completed, callResult)
            };
        }

        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Executed?.Invoke(this, new ExecuteResultEventArgs(
                    RpcCallResult.CreateFromExceptionMessage(transactionId,
                        t.Exception.HResult, t.Exception.ToString(), t.Exception.Message)));
                return;
            }

            if (!t.IsCompletedSuccessfully) return;
            var callResult = service.SerializeResult(basic.Session, t.Result);
            Executed?.Invoke(this, new ExecuteResultEventArgs(
                RpcCallResult.CreateFromMessage(transactionId, RpcCallResultStatus.Completed, callResult)));
        }).ConfigureAwait(false);

        return new ExecuteResult
        {
            ResultCommand = UmiRpcConstants.CALL_RESULT,
            CloseConnection = false,
            NextState = ClientState.Idle,
            Package = RpcCallResult.CreateFromMessage(transactionId,
                RpcCallResultStatus.Padding, ReadOnlySpan<byte>.Empty)
        };
    }
}

/// <summary>
/// 注意！！这个资源需要使用者释放
/// </summary>
/// <param name="result"></param>
public sealed class ExecuteResultEventArgs(RpcCallResult result) : EventArgs
{
    public RpcCallResult ResultCommand { get; } = result;
}