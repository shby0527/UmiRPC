namespace Umi.Rpc.Server.DependencyInjections.Configurations;

public sealed class ServerConfiguration
{
    public required string ListenAddress { get; init; }
    public required int ListenPort { get; init; }
    public int Backport { get; init; } = 250;

    public bool EnabledAuthentication { get; init; } = false;

    public AuthenticationType AuthenticationType { get; init; } = AuthenticationType.Password;

    public ServerAuthenticationConfiguration? Authentications { get; init; }
}

public enum AuthenticationType : byte
{
    Password,
    PublicKey,
    Custom
}