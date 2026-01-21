using System.Reflection;

namespace Umi.Proxy.Dynamic.Aspect.Invocations;

public sealed class InstanceMethodInvocation(
    object target,
    Type[] typeGenericArguments,
    Type interfaceType,
    MethodInfo method,
    object[] arguments,
    Type[] argumentTypes,
    Type[] genericArguments,
    Type returnType)
    : InvocationBase(typeGenericArguments, interfaceType, method,
        arguments, argumentTypes, genericArguments, returnType)
{
    public override bool IsStatic => false;

    public object Target => target;
}