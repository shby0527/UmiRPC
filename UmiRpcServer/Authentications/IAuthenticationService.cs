namespace Umi.Rpc.Server.Authentications;

public interface IAuthenticationService
{
    bool NeedsAuthentication { get; }

    bool PasswordAuthenticationEnabled { get; }

    bool KeyAuthenticationEnabled { get; }

    ushort GenerateChallengeCode();

    bool Authenticate(string username, string password, ReadOnlySpan<byte> session);

    bool Authenticate(string username, ReadOnlySpan<byte> keyData, ReadOnlySpan<byte> session);

    bool SessionCheck(ReadOnlySpan<byte> session);
}