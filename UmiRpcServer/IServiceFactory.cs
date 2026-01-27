using Umi.Rpc.Server.Services;

namespace Umi.Rpc.Server;

public interface IServiceFactory
{
    IAuthenticationService AuthenticationService { get; }

    IMetadataService MetadataService { get; }
}