// ReSharper disable InconsistentNaming

namespace Umi.Rpc.Base;

public static class UmiRpcConstants
{
    // Magic 固定是这个
    public const uint MAGIC = 0x79_38_25_39;

    // 版本号固定这个
    public const byte VERSION = 0x01;

    // ///////////////////////////////////////////////////

    public const int UNKNOWN_PROTOCOL = unchecked((int)0x80_00_00_01);

    public const int UNSUPPORTED_VERSION = unchecked((int)0x80_00_00_02);


    public const int CONNECTION_CLOSED = unchecked((int)0xFF_FF_FF_FF);

    // 需要认证
    public const int NEED_AUTHENTICATION = 0x7F_00_00_00;

    public const int SESSION_CONFLICT = unchecked((int)0x80_00_00_10);
    public const int SESSION_ERROR = unchecked((int)0x80_00_00_11);


    public const int AUTHENTICATION_REQUIRED = unchecked((int)0x80_00_00_20);
    public const int AUTHENTICATION_FAILURE = unchecked((int)0x80_00_00_21);

    ///////////////////////////////////////////////////////
    // 下面开始 command 

    #region Bootstrap 阶段定义

    /*
     * session 空闲有效60分钟，60分钟内除了 心跳包无其他动作，session 失效，需要通知所有 该session 的连接关闭
     * client <-----------------------------> server
     * (这部分是bootstrap 部分，会有一个 20 字节的 session
     * handshake ----------------------->
     *          <---------------------- handshake result (是否需要authentication 以及确认)
     * authentication ---------------->
     *          <-----------------------authentication result (认证是否通过，不通过允许重试）
     * metadata consent ---------------->  （协商元数据，客户端发送需要的元数据 （我需要的接口实现，序列化方式等）
     *          <----------------------- metadata result (依据客户端发送的，返回支持的特性，客户端依据服务端返回，再依照自己的设置确认是否可以通讯
     * 后续如果 session 相同，不需要重复上述bootstrap 过程，否则等同建立新客户端连接, session 如果相同，会直接异常
     * 客户端 bootstrap 后建立的连接后，直接发送 handshake continue 并使用 同一个 session ，服务端将会将其纳入正常client通信中)
     * ping <---------------------------> pong (建立认证后每 5 分钟一次，超时等待5分钟后，服务端主动断开连接）
     *
     * call ----------------------------> （方法调用
     *      <--------------------------- call result （方法返回
     *      <--------------------------- call exception (方法异常
     *      <--------------------------- call method not found (方法没找到
     *      <--------------------------- call event raise (远程事件触发【暂时未实现】
     */
    public const uint COMMON_ERROR = 0xEF_FF_FF_FF;
    public const uint HANDSHAKE = 0x00; // 无payload
    public const uint HANDSHAKE_RESULT = 0x01; // payload 为 common error
    public const uint HANDSHAKE_CONTINUE = 0x02; // 无 payload
    public const uint HANDSHAKE_CONTINUE_ACK = 0x03; // 无 payload
    public const uint METADATA_CONSENT = 0x10_01; // payload 待定
    public const uint METADATA_RESULT = 0x10_02; // payload 待定

    public const uint AUTHENTICATION = 0x01_00_00; // payload 为 Authentication
    public const uint AUTHENTICATION_RESULT = 0x01_00_01; // payload 为 Common Error

    #endregion

    #region Session 管理区域

    // 客户端 或 服务端 发送 session 结束 ， 无 payload 
    public const uint SESSION_CLOSE = 0xF0_00_10_01;

    // 确认后，所有拥有相同 session 的客户端均会收到 session close 包并且服务端断开连接 , 无 payload
    public const uint SESSION_CLOSE_ACK = 0xF0_00_10_02;

    // 客户端刷新Session (使用旧的session 刷新新的 session)  payload 直接为 20 字节 session (新session)
    public const uint SESSION_REFRESH = 0xF0_00_10_03;

    // 服务端确认新的 session 无payload 
    public const uint SESSION_REFRESH_ACK = 0xF0_00_10_04;

    #endregion


    public const uint PING = 0x02_00_00; // 无 payload
    public const uint PONG = 0x02_00_01; // 无 payload

    #region 方法调用区

    public const uint CALL = 0x03_00_00; // payload 待定
    public const uint CALL_RESULT = 0x03_00_01; // payload 待定
    public const uint CALL_EXCEPTION = 0x03_00_02; // payload 待定
    public const uint CALL_EVENT_RAISE = 0x03_00_03; // payload 待定
    public const uint CALL_METHOD_NOT_FOUND = 0x03_00_04; // payload 为 CommonError

    #endregion


    public const uint EXTENSIONS_BEGIN = 0x80_00_00_00; // 扩展区 0x80000000 - 0x8FFFFFFF
    public const uint EXTENSIONS_END = 0x8F_FF_FF_FF; // 扩展区 0x80000000 - 0x8FFFFFFF
}