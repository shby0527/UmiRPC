using System.Reflection;

namespace Umi.Proxy.Dynamic.Aspect.Invocations;

public sealed class StaticMethodInvocation(
    Type interfaceType,
    MethodInfo method,
    object[] arguments,
    Type[] argumentTypes,
    Type[] genericArguments,
    Type returnType)
    : InvocationBase(interfaceType, method, arguments, argumentTypes, genericArguments, returnType)
{
    public override bool IsStatic => true;
}