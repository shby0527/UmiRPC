using System.Diagnostics;
using System.Security.Cryptography;
using DynamicProxy;
using Umi.Proxy.Dynamic.Aspect;
using Umi.Proxy.Dynamic.Dynamic;
using UmiRpcProtocolStruct.Protocol;

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
        var type = AssemblyGenerator.GetOrGenerateType(typeof(ITest));
        IEnumerable<IInterceptor> interceptors = [new TestInterceptor()];
        var instance = Activator.CreateInstance(type, new TestInvoker(), interceptors);
        if (instance is ITest test)
        {
            var parameter = 20f;
            var f = test.Test(parameter);
            Assert.That(f, Is.EqualTo(parameter), $"{f} should be {parameter}");
            return;
        }

        Assert.Fail("instance is null or not ITest");
    }

    [Test]
    public void TestBasicPackage()
    {
        using RpcBasicPackage package = new();
        using var rnd = RandomNumberGenerator.Create();
        package.Magic = 0x123;
        package.Version = 0x1;
        package.Command = 0x4123;
        package.Length = 0;
        rnd.GetNonZeroBytes(package.Session);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(package.Magic, Is.EqualTo(0x123));
            Assert.That(package.Version, Is.EqualTo(0x1));
            Assert.That(package.Command, Is.EqualTo(0x4123));
            Assert.That(package.Length, Is.EqualTo(0));
        }
    }

    [Test]
    public void TestCommonErrorPackage()
    {
        using var p = RpcCommonErrorPackage.CreateFromMessage(unchecked((int)0x80_12_34_56), "123456789阿加法术的入口处");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(p.IsSuccess, Is.False);
            Assert.That(p.ErrorCode, Is.EqualTo(unchecked((int)0x80_12_34_56)));
            Assert.That(p.Code, Is.EqualTo(0x12_34_56));
            Assert.That(p.MessageLength, Is.EqualTo(33));
            Assert.That(p.Message, Is.EqualTo("123456789阿加法术的入口处"));
        }

        using var p2 = RpcCommonErrorPackage.CreateFromMemory(p.Memory);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(p2.IsSuccess, Is.False);
            Assert.That(p2.ErrorCode, Is.EqualTo(unchecked((int)0x80_12_34_56)));
            Assert.That(p2.Code, Is.EqualTo(0x12_34_56));
            Assert.That(p2.MessageLength, Is.EqualTo(33));
            Assert.That(p2.Message, Is.EqualTo("123456789阿加法术的入口处"));
        }
    }
}

public class TestInterceptor : IInterceptor
{
    public bool BeforeInvoke(IMethodInvocation invocation)
    {
        Debug.WriteLine("before invoke" + invocation.TargetType.FullName);
        return true;
    }

    public void AfterInvoke(IMethodInvocation invocation)
    {
        Debug.WriteLine("after invoke" + invocation.TargetType.FullName);
    }

    public void ExceptionInvoke(IMethodInvocation invocation, Exception exception)
    {
        Debug.WriteLine("exception" + invocation.TargetType.FullName + exception.Message);
    }
}

public class TestInvoker : IInvoker
{
    public void Process(IMethodInvocation input)
    {
        input.ReturnValue = input.GetArgumentValue(0);
    }
}

public interface ITest
{
    float Test(float input);
}