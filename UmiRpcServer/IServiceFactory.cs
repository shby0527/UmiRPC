using Umi.Rpc.Server.Authentications;

namespace Umi.Rpc.Server;

public interface IServiceFactory
{
    IAuthenticationService AuthenticationService { get; }

    IMetadataService MetadataService { get; }
}