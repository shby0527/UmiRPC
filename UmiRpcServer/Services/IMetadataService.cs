using Umi.Rpc.Protocol;

namespace Umi.Rpc.Server.Services;

public interface IMetadataService
{
    IEnumerable<RpcMetadataServiceWrap> GetAvailableServices();

    IEnumerable<RpcMetadataTypeMappingWrap> GetAvailableTypes();

    IEnumerable<RpcMetadataEventWrap> GetAvailableEvents();

    bool CheckSerialization(uint serialization);

    IEnumerable<RpcMetadataServiceWrap> CheckUnavailableServices(IEnumerable<RpcMetadataServiceWrap> services);

    IEnumerable<RpcMetadataTypeMappingWrap> CheckUnavailableTypes(IEnumerable<RpcMetadataTypeMappingWrap> types);

    IEnumerable<RpcMetadataEventWrap> CheckUnavailableEvents(IEnumerable<RpcMetadataEventWrap> events);
}