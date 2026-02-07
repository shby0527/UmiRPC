using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Umi.Rpc.Server.DependencyInjections.Injections;

internal sealed class InternalServerConfigure(IServiceCollection collection) : IRpcServerConfigure
{
    public IRpcServerConfigure AddService<TService>(string name) where TService : class
    {
        return this;
    }

    public IRpcServerConfigure AddService(Type service, string name)
    {
        return this;
    }

    public IRpcServerConfigure AddServices(IDictionary<string, Type> types)
    {
        return this;
    }

    public IRpcServerConfigure RegisterServices(Assembly assembly)
    {
        return this;
    }

    public IRpcServerConfigure AddTypeMappings<TType>(string mapping)
    {
        return this;
    }

    public IRpcServerConfigure AddTypeMappings(Type type, string mapping)
    {
        return this;
    }

    public IRpcServerConfigure AddTypeMappings(IDictionary<string, Type> types)
    {
        return this;
    }

    public IRpcServerMetadata Build()
    {
        throw new NotImplementedException();
    }
}