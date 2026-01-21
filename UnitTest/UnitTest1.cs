using System.Diagnostics;
using System.Reflection;
using DynamicProxy;
using Umi.Proxy.Dynamic.Aspect;
using Umi.Proxy.Dynamic.Dynamic;

namespace UnitTest;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var type = AssemblyGenerator.GetOrGenerateType(typeof(ITest<int>));
        var instance = (ITest<int>)Activator.CreateInstance(type.MakeGenericType(typeof(int)), new TestInvorker(),
            (IEnumerable<IInterceptor>)[]);
        Debug.WriteLine(instance.Test(1));
        Debug.WriteLine(instance.Property);
        Assert.Pass(type.ToString());
    }
}

public class TestInvorker : IInvoker
{
    public void Process(IMethodInvocation input)
    {
        input.ReturnValue = 1;
    }
}

public interface ITest<T>
{
    int Test(T input);

    int Property { get; }
}