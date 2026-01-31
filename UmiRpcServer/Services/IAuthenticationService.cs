namespace Umi.Rpc.Server.Services;

public interface IAuthenticationService
{
    bool NeedsAuthentication { get; }

    bool PasswordAuthenticationEnabled { get; }

    bool KeyAuthenticationEnabled { get; }

    ushort GenerateChallengeCode();

    bool Authenticate(string username, string password, scoped ReadOnlySpan<byte> session);

    bool Authenticate(string username, ReadOnlySpan<byte> keyData, scoped ReadOnlySpan<byte> session);

    bool SessionCheck(scoped ReadOnlySpan<byte> session);
}