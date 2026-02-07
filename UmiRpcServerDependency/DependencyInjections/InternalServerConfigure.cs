using Microsoft.Extensions.DependencyInjection;

namespace Umi.Rpc.Server.DependencyInjections.Injections;

internal sealed class InternalServerConfigure(IServiceCollection collection) : IRpcServerConfigure
{
}