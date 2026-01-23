namespace Umi.Proxy.Dynamic.Dynamic.DynamicCall;

public interface IInstanceMethodCaller
{
    object Call(object instance, object[] arguments);
}