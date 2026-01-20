using System.Reflection;

namespace Umi.Proxy.Dynamic.Aspect;

/// <summary>
/// AOP 参数 （ 这里的 AOP 因为是 RPC 的接口动态代理，实际没有真正的被代理对象）
/// </summary>
public interface IMethodInvocation
{
    /// <summary>
    /// 被代理类型
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// 被代理方法
    /// </summary>
    public MethodInfo TargetMethod { get; }

    /// <summary>
    /// 是否是静态调用
    /// </summary>
    public bool IsStatic { get; }

    /// <summary>
    /// 获取泛型参数
    /// </summary>
    /// <param name="index">泛型参数索引</param>
    /// <returns>泛型参数类型</returns>
    public Type GetGenericArguments(int index);

    /// <summary>
    /// 方法返回值
    /// </summary>
    public Type ReturnType { get; }

    /// <summary>
    /// 获取参数类型
    /// </summary>
    /// <param name="index">参数索引</param>
    /// <returns>参数值</returns>
    public Type GetArgumentType(int index);

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <param name="index">参数索引</param>
    /// <returns>参数值</returns>
    public object? GetArgumentValue(int index);

    /// <summary>
    /// 返回值
    /// </summary>
    public object? ReturnValue { get; set; }
}