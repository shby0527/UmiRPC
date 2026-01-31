using Umi.Rpc.Protocol;

namespace Umi.Rpc.Server.Services;

public interface IMetadataService
{
    /// <summary>
    /// 通用序列化方式确认
    /// </summary>
    /// <param name="session">绑定到session</param>
    /// <param name="serialization">约定的序列化编号</param>
    /// <returns>返回确认可用的序列化方案</returns>
    uint SerializationAck(scoped ReadOnlySpan<byte> session, uint serialization);

    /// <summary>
    /// 获取可用服务（填入对应 Service 的 Implement 并返回），不支持的将被剔除
    /// </summary>
    /// <param name="session">绑定到session</param>
    /// <param name="services">输入的查询服务</param>
    /// <returns></returns>
    IEnumerable<RpcMetadataServiceWrap> GetAvailableServices(scoped ReadOnlySpan<byte> session,
        IEnumerable<RpcMetadataServiceWrap> services);

    /// <summary>
    /// 获取或填充特殊类型序列化操作
    /// </summary>
    /// <param name="session">绑定到的session</param>
    /// <param name="types">返回协商成功的</param>
    /// <returns></returns>
    IEnumerable<RpcMetadataTypeMappingWrap> GetAvailableTypes(scoped ReadOnlySpan<byte> session,
        IEnumerable<RpcMetadataTypeMappingWrap> types);

    /// <summary>
    /// 客户端通知的可能应用事件的信息
    /// </summary>
    /// <param name="session">绑定到session</param>
    /// <param name="events">事件</param>
    void ClientTypeEventAdvise(scoped ReadOnlySpan<byte> session, IEnumerable<RpcMetadataEventWrap> events);
}