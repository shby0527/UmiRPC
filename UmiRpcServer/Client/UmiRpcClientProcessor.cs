using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;

namespace Umi.Rpc.Server.Client;

public abstract class UmiRpcClientProcessor : IDisposable
{
    private readonly Socket _socket;

    private readonly SocketAsyncEventArgs _receiveArgs;

    private readonly Pipe _pipe;

    private LinkedListNode<UmiRpcClientProcessor>? _node;

    protected UmiRpcClientProcessor(Socket socket)
    {
        _socket = socket;
        _receiveArgs = new SocketAsyncEventArgs();
        _receiveArgs.Completed += ReceivedArgsOnCompleted;
        _receiveArgs.SetBuffer(new byte[4096]);
        _socket.ReceiveBufferSize = 4096;
        _socket.SendBufferSize = 4096;
        _pipe = new Pipe();
    }

    private void ReceivedArgsOnCompleted(object? sender, SocketAsyncEventArgs args)
    {
        var writer = _pipe.Writer;
        if (args is not
            { LastOperation: SocketAsyncOperation.Receive, SocketError: SocketError.Success, BytesTransferred: > 0 })
        {
            writer.Complete();
            if (sender is Socket client) client.Close();
            Close?.Invoke(this, new UmiRpcClientCloseEventArgs(_node!));
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
            if (basic.Magic != UmiRpcConstants.MAGIC) continue;
            if (basic.Version > UmiRpcConstants.VERSION) continue;
            //  后续的其他验证 逻辑
            if (!SessionValid(basic.Session))
            {
                var code = GeneratedChallenge();
                using var err =
                    RpcCommonError.CreateFromMessage(UmiRpcConstants.NEED_AUTHENTICATION | code, "need authentication");
                using var rtp = RpcBasic.CreateFromMessage(UmiRpcConstants.HANDSHAKE_RESULT);
                rtp.Length = err.Memory.Length;
                using var buffer = MemoryPool<byte>.Shared.Rent(rtp.Memory.Length + err.Memory.Length);
                rtp.Memory.CopyTo(buffer.Memory.Span);
                err.Memory.CopyTo(buffer.Memory[rtp.Memory.Length..].Span);
                var total = rtp.Memory.Length + err.Memory.Length;
                var send = 0;
                while (total - send > 0)
                {
                    send += await _socket.SendAsync(buffer.Memory[send..total], SocketFlags.None);
                }

                continue;
            }
        }
    }

    protected abstract ushort GeneratedChallenge();

    protected abstract bool SessionValid(scoped ReadOnlySpan<byte> session);

    public event EventHandler<UmiRpcClientCloseEventArgs>? Close;

    public void Start(LinkedListNode<UmiRpcClientProcessor> node)
    {
        _node = node;
        if (!_socket.ReceiveAsync(_receiveArgs))
        {
            ReceivedArgsOnCompleted(this, _receiveArgs);
        }

        _ = ProcessDataAsync();
    }

    public void Stop()
    {
        _socket.Shutdown(SocketShutdown.Both);
        _socket.Close();
        if (_node != null)
            Close?.Invoke(this, new UmiRpcClientCloseEventArgs(_node));
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
    }
}

public sealed class UmiRpcClientCloseEventArgs(LinkedListNode<UmiRpcClientProcessor> node) : EventArgs
{
    public LinkedListNode<UmiRpcClientProcessor> Node { get; } = node;
}