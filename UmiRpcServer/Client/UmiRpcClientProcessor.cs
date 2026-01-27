using System.Buffers;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net.Sockets;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Exceptions;
using Umi.Rpc.Server.Executors;

namespace Umi.Rpc.Server.Client;

public abstract class UmiRpcClientProcessor : IDisposable
{
    private readonly Socket _socket;

    private readonly SocketAsyncEventArgs _receiveArgs;

    private readonly Pipe _pipe;

    private readonly IReadOnlyDictionary<ClientState, IReadOnlyDictionary<uint, IServerExecutor>> _executors;

    private ClientState _state = ClientState.Init;

    public ClientState State => _state;

    public LinkedListNode<UmiRpcClientProcessor> Node { get; }

    protected UmiRpcClientProcessor(Socket socket)
    {
        _socket = socket;
        _receiveArgs = new SocketAsyncEventArgs();
        _receiveArgs.Completed += ReceivedArgsOnCompleted;
        _receiveArgs.SetBuffer(new byte[4096]);
        _socket.ReceiveBufferSize = 4096;
        _socket.SendBufferSize = 4096;
        _pipe = new Pipe();
        Node = new LinkedListNode<UmiRpcClientProcessor>(this);
        _executors = RegisterSystemExecutor();
    }

#pragma warning disable CA1859
    private IReadOnlyDictionary<ClientState, IReadOnlyDictionary<uint, IServerExecutor>> RegisterSystemExecutor()
    {
        var extensionsExecutors = RegisterExtensionsExecutors();
        // 合规性检查
        foreach (var executor in extensionsExecutors)
        {
            if (executor.Key is not (>= UmiRpcConstants.EXTENSIONS_BEGIN and <= UmiRpcConstants.EXTENSIONS_END))
            {
                throw new ProtocolCommandConflictException($"{nameof(executor.Key)} is out of Extension Range");
            }
        }

        var dic = new Dictionary<ClientState, IReadOnlyDictionary<uint, IServerExecutor>>
        {
            {
                ClientState.Handshake, // Handshake 阶段
                new Dictionary<uint, IServerExecutor>
                {
                    {
                        UmiRpcConstants.HANDSHAKE,
                        new HandshakeExecutor(ServiceFactory.AuthenticationService)
                    },
                    {
                        UmiRpcConstants.HANDSHAKE_CONTINUE,
                        new HandshakeContinueExecutor(ServiceFactory.AuthenticationService)
                    }
                }.ToImmutableDictionary()
            },
            {
                ClientState.Authentication,
                new Dictionary<uint, IServerExecutor>
                {
                    {
                        UmiRpcConstants.AUTHENTICATION,
                        new AuthenticationExecutor(ServiceFactory.AuthenticationService)
                    }
                }.ToImmutableDictionary()
            },
            {
                ClientState.MetadataConsent,
                new Dictionary<uint, IServerExecutor>
                {
                }.ToImmutableDictionary()
            },
            {
                ClientState.Idle,
                new Dictionary<uint, IServerExecutor>(extensionsExecutors)
                {
                }.ToImmutableDictionary()
            }
        };
        return dic.ToImmutableDictionary();
    }
#pragma warning restore CA1859

    protected abstract IServiceFactory ServiceFactory { get; }

    protected abstract IReadOnlyDictionary<uint, IServerExecutor> RegisterExtensionsExecutors();

    private void ReceivedArgsOnCompleted(object? sender, SocketAsyncEventArgs args)
    {
        var writer = _pipe.Writer;
        if (args is not
            { LastOperation: SocketAsyncOperation.Receive, SocketError: SocketError.Success, BytesTransferred: > 0 })
        {
            writer.Complete();
            if (sender is Socket client) client.Close();
            Close?.Invoke(this, new UmiRpcClientCloseEventArgs(Node));
            return;
        }

        var memory = writer.GetSpan(args.BytesTransferred);
        args.MemoryBuffer[..args.BytesTransferred].Span.CopyTo(memory);
        writer.Advance(args.BytesTransferred);
        _ = writer.FlushAsync()
            .AsTask()
            .ContinueWith(_ =>
            {
                if (!_socket.ReceiveAsync(_receiveArgs))
                {
                    ReceivedArgsOnCompleted(this, _receiveArgs);
                }
            });
    }

    private async Task ProcessDataAsync()
    {
        try
        {
            var reader = _pipe.Reader;
            while (true)
            {
                var result = await reader.ReadAtLeastAsync(RpcBasic.SIZE_OF_PACKAGE);
                if (result is { IsCanceled: true } or { IsCompleted: true })
                {
                    reader.AdvanceTo(result.Buffer.End);
                    await reader.CompleteAsync();
                    return;
                }

                using var basic = RpcBasic.CreateFromMemory(result.Buffer);
                var position = result.Buffer.GetPosition(RpcBasic.SIZE_OF_PACKAGE);
                reader.AdvanceTo(position);
                // 忽略Magic 不正确的包
                if (basic.Magic != UmiRpcConstants.MAGIC)
                {
                    using var msg =
                        RpcCommonError.CreateFromMessage(UmiRpcConstants.UNKNOWN_PROTOCOL, "unknown protocol");
                    await SendingPackage(UmiRpcConstants.COMMON_ERROR, msg);
                    Stop();
                    return;
                }

                if (basic.Version > UmiRpcConstants.VERSION)
                {
                    // 版本不正确
                    using var msg =
                        RpcCommonError.CreateFromMessage(UmiRpcConstants.UNSUPPORTED_VERSION, "unsupported version");
                    await SendingPackage(UmiRpcConstants.COMMON_ERROR, msg);
                    Stop();
                    return;
                }

                if (_executors.TryGetValue(_state, out var executor))
                {
                    if (executor.TryGetValue(basic.Command, out var serverExecutor))
                    {
                        var rp = await serverExecutor.ExecuteCommandAsync(basic, reader);
                        _state = rp.NextState;
                        using var pack = rp.Package;
                        await SendingPackage(rp.ResultCommand, pack);
                        if (rp.CloseConnection)
                        {
                            Stop();
                            return;
                        }

                        continue;
                    }
                }

                // default executor 
                // 默认仅消耗数据包，然后丢弃它
                if (basic.Length <= 0) continue;
                var errData = await reader.ReadAtLeastAsync(basic.Length);
                if (errData is { IsCanceled: true } or { IsCompleted: true })
                {
                    reader.AdvanceTo(errData.Buffer.End);
                    await reader.CompleteAsync();
                    return;
                }

                var sequencePosition = errData.Buffer.GetPosition(basic.Length);
                reader.AdvanceTo(sequencePosition);
            }
        }
        catch (Exception)
        {
            Stop();
        }
    }

    // ReSharper disable once MemberCanBePrivate.Global
    protected async ValueTask SendingPackage<T>(uint command, T? package = null) where T : RpcPackageBase
    {
        using var basic = RpcBasic.CreateFromMessage(command);
        basic.Length = package?.Memory.Length ?? 0;
        var totalLength = basic.Memory.Length + basic.Length;
        using var memory = MemoryPool<byte>.Shared.Rent(totalLength);
        basic.Memory.CopyTo(memory.Memory.Span);
        package?.Memory.CopyTo(memory.Memory[basic.Memory.Length..].Span);
        var send = 0;
        while (send < totalLength)
        {
            send += await _socket.SendAsync(memory.Memory[send..], SocketFlags.None);
        }
    }

    public event EventHandler<UmiRpcClientCloseEventArgs>? Close;

    public void Start()
    {
        if (!_socket.ReceiveAsync(_receiveArgs))
        {
            ReceivedArgsOnCompleted(this, _receiveArgs);
        }

        _state = ClientState.Handshake;

        _ = ProcessDataAsync();
    }

    public void Stop()
    {
        _socket.Shutdown(SocketShutdown.Both);
        _socket.Close();
        Close?.Invoke(this, new UmiRpcClientCloseEventArgs(Node));
    }

    public void Dispose()
    {
        if (_socket.Connected)
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

        _socket.Dispose();
        _receiveArgs.Completed -= ReceivedArgsOnCompleted;
        _receiveArgs.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class UmiRpcClientCloseEventArgs(LinkedListNode<UmiRpcClientProcessor> node) : EventArgs
{
    public LinkedListNode<UmiRpcClientProcessor> Node { get; } = node;
}