using System.Reflection;

namespace Umi.Proxy.Dynamic.Aspect.Invocations;

public abstract class InvocationBase(
    Type interfaceType,
    MethodInfo method,
    object[] arguments,
    Type[] argumentTypes,
    Type[] genericArguments,
    Type returnType)
    : IMethodInvocation
{
    private readonly Type[] _argumentTypes = argumentTypes ?? throw new ArgumentNullException(nameof(argumentTypes));

    private readonly Type[] _genericArguments =
        genericArguments ?? throw new ArgumentNullException(nameof(genericArguments));

    private readonly object[] _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));

    public Type TargetType { get; } = interfaceType ?? throw new ArgumentNullException(nameof(interfaceType));

    public MethodInfo TargetMethod { get; } = method ?? throw new ArgumentNullException(nameof(method));

    public abstract bool IsStatic { get; }

    public Type GetGenericArguments(int index)
    {
        if (index < 0 || index >= _genericArguments.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _genericArguments[index];
    }

    public Type ReturnType { get; } = returnType ?? throw new ArgumentNullException(nameof(returnType));

    public Type GetArgumentType(int index)
    {
        if (index < 0 || index >= _argumentTypes.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _argumentTypes[index];
    }

    public object? GetArgumentValue(int index)
    {
        if (index < 0 || index >= _arguments.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _arguments[index];
    }

    public object? ReturnValue
    {
        get;
        set
        {
            if (ReturnType == typeof(void))
            {
                throw new InvalidOperationException($"{nameof(ReturnType)}  cannot be set to void");
            }

            field = value;
        }
    }
}