using Umi.Proxy.Dynamic.Aspect;

namespace DynamicProxy;

public interface IInvoker
{
    void Process(IMethodInvocation input);
}