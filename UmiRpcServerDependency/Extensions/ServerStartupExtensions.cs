using Microsoft.Extensions.DependencyInjection;
using Umi.Rpc.Server.DependencyInjections.Injections;
using UmiRpcServerDependency.Server;

namespace Umi.Rpc.Server.DependencyInjections.Extensions;

public static class ServerStartupExtensions
{
    public static IRpcServerConfigure AddRpcServer(this IServiceCollection collection)
    {
        collection.AddSingleton<ServerBackgroundWorker>();
        collection.AddSingleton<DependencyRpcServer>();
        return new InternalServerConfigure(collection);
    }
}