namespace Umi.Proxy.Dynamic.Aspect;

public interface IInterceptor
{
    bool BeforeInvoke(IMethodInvocation invocation);

    void AfterInvoke(IMethodInvocation invocation);

    void ExceptionInvoke(IMethodInvocation invocation, Exception exception);
}