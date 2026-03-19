using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using UmiRpcServerDependency.Attributes;

namespace Umi.Rpc.Server.DependencyInjections.Injections.Implements;

internal sealed class InternalServerConfigure(IServiceCollection collection) : IRpcServerConfigure
{
    private readonly Dictionary<(string Name, int Version), ServiceType> _services = new();

    private readonly Dictionary<Type, string> _mappings = new();

    private readonly Dictionary<Guid, string> _events = new();


    public IRpcServerConfigure AddService<TService>(string name, int version) where TService : class
    {
        return AddService(typeof(TService), name, version);
    }

    public IRpcServerConfigure AddService(Type service, string name, int version)
    {
        if (service is { IsAbstract: true } or { IsInterface: true })
            throw new NotSupportedException($"{service.Name}is not supported for UmiRPC");
        _services[(name, version)] = new ServiceType(version, service);
        return this;
    }

    public IRpcServerConfigure AddServices(IDictionary<string, Type> types, int version)
    {
        foreach (var type in types)
        {
            AddService(type.Value, type.Key, version);
        }

        return this;
    }

    public IRpcServerConfigure RegisterServices(Assembly assembly)
    {
        var types = assembly.GetTypes();
        foreach (var type in types)
        {
            var attribute = type.GetCustomAttribute<RpcServiceAttribute>();
            if (attribute is null) continue;
            AddService(type, string.IsNullOrEmpty(attribute.Name) ? type.Name : attribute.Name, attribute.Version);
        }

        return this;
    }

    public IRpcServerConfigure AddTypeMappings<TType>(string mapping)
    {
        return AddTypeMappings(typeof(TType), mapping);
    }

    public IRpcServerConfigure AddTypeMappings(Type type, string mapping)
    {
        _mappings[type] = mapping;
        return this;
    }

    public IRpcServerConfigure AddTypeMappings(IDictionary<Type, string> types)
    {
        foreach (var type in types)
        {
            AddTypeMappings(type.Key, type.Value);
        }

        return this;
    }

    public IRpcServerConfigure AddEvent(Guid guid, string name)
    {
        _events[guid] = name;
        return this;
    }

    public IRpcServerConfigure AddEvents(IDictionary<Guid, string> events)
    {
        foreach (var ev in events)
        {
            AddEvent(ev.Key, ev.Value);
        }

        return this;
    }

    public IRpcServerMetadata Build()
    {
        throw new NotImplementedException();
    }
}