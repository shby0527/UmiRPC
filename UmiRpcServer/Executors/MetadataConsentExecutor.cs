using System.IO.Pipelines;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;
using Umi.Rpc.Server.Client;
using Umi.Rpc.Server.Services;

namespace Umi.Rpc.Server.Executors;

public sealed class MetadataConsentExecutor(IMetadataService service) : IServerExecutor
{
    public async ValueTask<ExecuteResult> ExecuteCommandAsync(RpcBasic basic, PipeReader reader)
    {
        // 这里应该要有一个 Payload ，如果没有，就是错误包，应该返回通用错误并保持原样
        if (basic.Length <= 0)
        {
            return new ExecuteResult
            {
                CloseConnection = false,
                ResultCommand = UmiRpcConstants.COMMON_ERROR,
                NextState = ClientState.MetadataConsent,
                Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.METADATA_CONSENT_FAILURE, "payload required")
            };
        }

        // 开始读取元数据协商包
        var result = await reader.ReadAtLeastAsync(basic.Length);
        if (result.IsCanceled || result.IsCompleted)
        {
            return new ExecuteResult
            {
                ResultCommand = UmiRpcConstants.COMMON_ERROR,
                CloseConnection = true,
                NextState = ClientState.Closed,
                Package = RpcCommonError.CreateFromMessage(UmiRpcConstants.CONNECTION_CLOSED, "Connection Closed")
            };
        }

        using var metadata = RpcMetadataConsent.CreateFromMemory(result.Buffer);
        // 消费数据
        var position = result.Buffer.GetPosition(basic.Length);
        reader.AdvanceTo(position);
        // 开始构建非托管的Wrap结构对象(这里需要构建大量托管对象了, 毕竟为了屏蔽实现细节，但是整个会话周期，只会进行一次, 可以接受）
        RpcMetadataServiceWrap[]? serviceWraps = null;
        RpcMetadataTypeMappingWrap[]? mappingWraps = null;
        RpcMetadataEventWrap[]? eventWraps = null;
        for (var i = 0; i < metadata.ContentHeaderCount; i++)
        {
            var header = metadata.ContentHeaders[i];
            switch (header.Flag)
            {
                case RpcMetadataContent.FLAG_SERVICE:
                    serviceWraps = new RpcMetadataServiceWrap[header.Count];
                    var contentService = metadata.GetContentHeader<RpcMetadataService>(header.Offset, header.Count);
                    for (var j = 0; j < header.Count; j++)
                    {
                        serviceWraps[j] = new RpcMetadataServiceWrap(contentService[j].Version,
                            metadata.GetString(contentService[j].NameOffset, contentService[j].NameLength),
                            metadata.GetString(contentService[j].ImplementOffset, contentService[j].ImplementLength));
                    }

                    break;
                case RpcMetadataContent.FLAG_TYPE_MAPPING:
                    mappingWraps = new RpcMetadataTypeMappingWrap[header.Count];
                    var contentMapping = metadata.GetContentHeader<RpcMetadataTypeMapping>(header.Offset, header.Count);
                    for (var j = 0; j < header.Count; j++)
                    {
                        mappingWraps[j] = new RpcMetadataTypeMappingWrap(
                            metadata.GetString(contentMapping[j].SourceTypeOffset, contentMapping[j].SourceTypeLength),
                            metadata.GetString(contentMapping[j].TargetTypeOffset, contentMapping[j].TargetTypeLength));
                    }

                    break;
                case RpcMetadataContent.FLAG_EVENT_HANDLE:
                    eventWraps = new RpcMetadataEventWrap[header.Count];
                    var contentEvent = metadata.GetContentHeader<RpcMetadataEventHandle>(header.Offset, header.Count);
                    for (var j = 0; j < header.Count; j++)
                    {
                        eventWraps[j] = new RpcMetadataEventWrap(contentEvent[j].TypeGuid, contentEvent[j].ObjectGuid,
                            metadata.GetString(contentEvent[j].TypeNameOffset, contentEvent[j].TypeNameLength),
                            metadata.GetString(contentEvent[j].EventNameOffset, contentEvent[j].EventNameLength));
                    }

                    break;
            }
        }

        var services = service.GetAvailableServices(basic.Session, serviceWraps ?? []);
        var mappings = service.GetAvailableTypes(basic.Session, mappingWraps ?? []);
        service.ClientTypeEventAdvise(basic.Session, eventWraps ?? []);
        return new ExecuteResult
        {
            CloseConnection = false,
            ResultCommand = UmiRpcConstants.METADATA_RESULT,
            NextState = ClientState.Idle,
            Package = RpcMetadataConsent.CreateFromMessage(
                service.SerializationAck(basic.Session, metadata.Serialization),
                new RpcMetadataContentWrap(services.ToArray(), mappings.ToArray(), null))
        };
    }
}