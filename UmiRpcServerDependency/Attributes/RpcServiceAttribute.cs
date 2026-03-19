namespace UmiRpcServerDependency.Attributes;

/// <summary>
/// Rpc服务注册
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RpcServiceAttribute : Attribute
{
    /// <summary>
    /// 服务名
    /// </summary>
    public string? Name { get; }

    public int Version { get; }

    public RpcServiceAttribute()
    {
        Version = 1;
    }

    public RpcServiceAttribute(int version)
    {
        Version = version;
    }

    public RpcServiceAttribute(string? name, int version)
        : this(version)
    {
        Name = name;
    }

    public RpcServiceAttribute(string name)
        : this()
    {
        Name = name;
    }
}