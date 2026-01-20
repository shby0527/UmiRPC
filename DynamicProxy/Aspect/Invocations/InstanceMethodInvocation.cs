using System.Reflection;

namespace Umi.Proxy.Dynamic.Aspect.Invocations;

internal sealed class InstanceMethodInvocation(
    object target,
    Type interfaceType,
    MethodInfo method,
    object[] arguments,
    Type[] argumentTypes,
    Type[] genericArguments,
    Type returnType)
    : InvocationBase(interfaceType, method, arguments, argumentTypes, genericArguments, returnType)
{
    public override bool IsStatic => false;

    public object Target => target;
}