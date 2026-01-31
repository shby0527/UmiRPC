namespace Umi.Rpc.Server.Client;

public enum ClientState : byte
{
    /// <summary>
    /// 初始连接(逻辑存在，实际不存在）
    /// </summary>
    Init = 0,

    /// <summary>
    /// 等待接收握手包（Command 为 Handshake 或 Handshake Continue 的包) 
    /// </summary>
    Handshake,

    /// <summary>
    /// 等待认证阶段，等待认证的包 command 为 Authentication 的包
    /// </summary>
    Authentication,

    /// <summary>
    /// 等待元数据协商阶段 等待 command 为 METADATA_CONSENT 的包
    /// </summary>
    MetadataConsent,

    /// <summary>
    /// 空闲, 等待 Call 或 Close 的包
    /// </summary>
    Idle,

    /// <summary>
    /// 方法执行中,无法并行处理 方法执行中, 不接受任何包
    /// </summary>
    Executing,

    /// <summary>
    /// 客户端关闭 逻辑存在，实际不存在
    /// </summary>
    Closed,
}

public enum ClientHealthStatus : byte
{
    Healthy,
    Questing,
    Unhealthy,
}