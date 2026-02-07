using System.Reflection;

namespace Umi.Rpc.Server.DependencyInjections.Injections;

public interface IRpcServerConfigure
{
    IRpcServerConfigure AddService<TService>(string name) where TService : class;

    IRpcServerConfigure AddService(Type service, string name);

    IRpcServerConfigure AddServices(IDictionary<string, Type> types);

    IRpcServerConfigure RegisterServices(Assembly assembly);

    IRpcServerConfigure AddTypeMappings<TType>(string mapping);

    IRpcServerConfigure AddTypeMappings(Type type, string mapping);

    IRpcServerConfigure AddTypeMappings(IDictionary<string, Type> types);


    IRpcServerMetadata Build();
}