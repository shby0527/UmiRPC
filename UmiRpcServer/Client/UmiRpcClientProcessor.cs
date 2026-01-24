using System.IO.Pipelines;
using System.Net.Sockets;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;

namespace Umi.Rpc.Server.Client;

public abstract class UmiRpcClientProcessor : IDisposable
{
    private readonly Socket _socket;

    private readonly SocketAsyncEventArgs _acceptArgs;

    private readonly Pipe _pipe;

    private LinkedListNode<UmiRpcClientProcessor>? _node;

    protected UmiRpcClientProcessor(Socket socket)
    {
        _socket = socket;
        _acceptArgs = new SocketAsyncEventArgs();
        _acceptArgs.Completed += AcceptArgsOnCompleted;
        _acceptArgs.SetBuffer(new byte[4096]);
        _socket.ReceiveBufferSize = 4096;
        _socket.SendBufferSize = 4096;
        _pipe = new Pipe();
    }

    private void AcceptArgsOnCompleted(object? sender, SocketAsyncEventArgs args)
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
        _ = writer.FlushAsync().AsTask();

        while (!_socket.ReceiveAsync(_acceptArgs))
        {
            AcceptArgsOnCompleted(this, _acceptArgs);
        }
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
        }
    }

    public event EventHandler<UmiRpcClientCloseEventArgs>? Close;

    public void Start(LinkedListNode<UmiRpcClientProcessor> node)
    {
        _node = node;
        while (!_socket.ReceiveAsync(_acceptArgs))
        {
            AcceptArgsOnCompleted(this, _acceptArgs);
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
        _acceptArgs.Completed -= AcceptArgsOnCompleted;
        _acceptArgs.Dispose();
    }
}

public sealed class UmiRpcClientCloseEventArgs(LinkedListNode<UmiRpcClientProcessor> node) : EventArgs
{
    public LinkedListNode<UmiRpcClientProcessor> Node { get; } = node;
}