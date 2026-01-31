namespace Umi.Rpc.Server.Services;

public interface ISessionService
{
    /// <summary>
    /// 刷新 session
    /// </summary>
    /// <param name="oldSession">老session</param>
    /// <param name="newSession">新session</param>
    /// <returns></returns>
    bool Refresh(scoped ReadOnlySpan<byte> oldSession, scoped ReadOnlySpan<byte> newSession);

    /// <summary>
    /// 无效化session
    /// </summary>
    /// <param name="session"></param>
    /// <returns></returns>
    bool InvalidateSession(scoped ReadOnlySpan<byte> session);

    /// <summary>
    /// 检查Session是否有效
    /// </summary>
    /// <param name="session"></param>
    /// <returns></returns>
    bool CheckSession(scoped ReadOnlySpan<byte> session);
}