// ReSharper disable InconsistentNaming

namespace Umi.Rpc.Base;

public static class UmiRpcConstants
{
    // Magic 固定是这个
    public const uint MAGIC = 0x79_38_25_39;

    // 版本号固定这个
    public const byte VERSION = 0x01;


    ///////////////////////////////////////////////////////
    // 下面开始 command 
    public const uint HANDSHAKE = 0x00;
    public const uint HANDSHAKE_RESULT = 0x01;

    public const uint AUTHENTICATION = 0x01_00_00;
    public const uint AUTHENTICATION_RESULT = 0x01_00_01;

    public const uint PING = 0x02_00_00;
    public const uint PONG = 0x02_00_01;

    public const uint CALL = 0x03_00_00;
    public const uint CALL_RESULT = 0x03_00_01;
    public const uint CALL_EXCEPTION = 0x03_00_02;
    public const uint CALL_EVENT_RAISE = 0x03_00_03;

    public const uint EXTENSIONS = 0x80_00_00_00;
}