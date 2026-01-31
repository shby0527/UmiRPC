using System.Net;
using System.Net.Sockets;
using Umi.Rpc.Server.Client;

// ReSharper disable IntroduceOptionalParameters.Global

namespace Umi.Rpc.Server;

public abstract class UmiRpcServerBase : IDisposable
{
    private readonly Socket _socket;

    private readonly SocketAsyncEventArgs _acceptArgs;

    private readonly LinkedList<UmiRpcClientProcessor> _clientProcessors;

    private readonly Lock _lock;

    private readonly Timer _timer;

    protected UmiRpcServerBase(IPAddress address, int port, int backlog)
    {
        _socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _socket.Bind(new IPEndPoint(address, port));
        _socket.Listen(backlog);
        _acceptArgs = new SocketAsyncEventArgs();
        _acceptArgs.AcceptSocket = null;
        _acceptArgs.Completed += AcceptArgsOnCompleted;
        _clientProcessors = [];
        _lock = new Lock();
        _timer = new Timer(HealthWatchdog, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    private void HealthWatchdog(object? state)
    {
        var node = _clientProcessors.First;
        while (node is not null)
        {
            var current = node;
            node = node.Next;
            if (current.Value.Health is ClientHealthStatus.Unhealthy)
            {
                // Unhealthy， need remove and close
                current.Value.Stop();
            }
        }
    }

    protected UmiRpcServerBase(IPAddress address, int port)
        : this(address, port, 254)
    {
    }

    protected UmiRpcServerBase(int port)
        : this(IPAddress.Loopback, port)
    {
    }

    protected abstract UmiRpcClientProcessor CreateClientProcessor(Socket socket);

    private void AcceptArgsOnCompleted(object? sender, SocketAsyncEventArgs args)
    {
        if (args is not
            { LastOperation: SocketAsyncOperation.Accept, SocketError: SocketError.Success, AcceptSocket: not null })
        {
            return;
        }


        var client = CreateClientProcessor(args.AcceptSocket);
        lock (_lock)
        {
            _clientProcessors.AddLast(client.Node);
            client.Close += ClientOnClose;
            client.Start();
        }

        _acceptArgs.AcceptSocket = null;
        if (!_socket.AcceptAsync(_acceptArgs))
        {
            AcceptArgsOnCompleted(_socket, _acceptArgs);
        }
    }

    private void ClientOnClose(object? sender, UmiRpcClientCloseEventArgs e)
    {
        lock (_lock)
        {
            _clientProcessors.Remove(e.Node);
        }

        if (sender is UmiRpcClientProcessor clientProcessor) clientProcessor.Dispose();
    }

    public void Start()
    {
        if (!_socket.AcceptAsync(_acceptArgs))
        {
            AcceptArgsOnCompleted(_socket, _acceptArgs);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            foreach (var clientProcessor in _clientProcessors)
            {
                clientProcessor.Stop();
            }
        }

        _socket.Close();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _socket.Dispose();
        _acceptArgs.Completed -= AcceptArgsOnCompleted;
        _acceptArgs.Dispose();
        GC.SuppressFinalize(this);
    }
}