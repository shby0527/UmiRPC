// ReSharper disable ClassNeverInstantiated.Global

namespace Umi.Rpc.Server.DependencyInjections.Configurations;

public sealed class ServerAuthenticationConfiguration
{
    public required IReadOnlyDictionary<string, AuthenticationConfiguration> Users { get; init; }
}

public sealed class AuthenticationConfiguration
{
    public required int Id { get; init; }
    public required string Username { get; init; }
    public string? Password { get; init; }
    public string? PublicKey { get; init; }
    public required IReadOnlyCollection<PermissionConfiguration> Permissions { get; init; }
}

public sealed class PermissionConfiguration
{
    public required string ServiceName { get; init; }

    public required bool Enabled { get; init; }
}