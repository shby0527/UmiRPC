namespace Umi.Rpc.Server.Authentications;

public interface IAuthenticationService
{
    bool NeedsAuthentication { get; }

    bool PasswordAuthenticationEnabled { get; }

    bool KeyAuthenticationEnabled { get; }

    ushort GenerateChallengeCode();

    bool Authenticate(string username, string password);

    bool Authenticate(string username, ReadOnlySpan<byte> keyData);

    bool SessionCheck(ReadOnlySpan<byte> session);
}