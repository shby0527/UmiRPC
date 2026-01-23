using System.Diagnostics;
using System.Security.Cryptography;
using DynamicProxy;
using Umi.Proxy.Dynamic.Aspect;
using Umi.Proxy.Dynamic.Dynamic;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;

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
        using RpcBasic package = RpcBasic.CreateFromMessage(0x4123);
        using var rnd = RandomNumberGenerator.Create();
        package.Length = 0;
        rnd.GetNonZeroBytes(package.Session);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(package.Magic, Is.EqualTo(UmiRpcConstants.MAGIC));
            Assert.That(package.Version, Is.EqualTo(UmiRpcConstants.VERSION));
            Assert.That(package.Command, Is.EqualTo(0x4123));
            Assert.That(package.Length, Is.EqualTo(0));
        }
    }

    [Test]
    public void TestCommonErrorPackage()
    {
        using var p = RpcCommonError.CreateFromMessage(unchecked((int)0x80_12_34_56), "123456789阿加法术的入口处");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(p.IsSuccess, Is.False);
            Assert.That(p.ErrorCode, Is.EqualTo(unchecked((int)0x80_12_34_56)));
            Assert.That(p.Code, Is.EqualTo(0x12_34_56));
            Assert.That(p.MessageLength, Is.EqualTo(33));
            Assert.That(p.Message, Is.EqualTo("123456789阿加法术的入口处"));
        }

        using var p2 = RpcCommonError.CreateFromMemory(p.Memory);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(p2.IsSuccess, Is.False);
            Assert.That(p2.ErrorCode, Is.EqualTo(unchecked((int)0x80_12_34_56)));
            Assert.That(p2.Code, Is.EqualTo(0x12_34_56));
            Assert.That(p2.MessageLength, Is.EqualTo(33));
            Assert.That(p2.Message, Is.EqualTo("123456789阿加法术的入口处"));
        }
    }

    [Test]
    public void TestAuthenticationMessage()
    {
        using var rng = RandomNumberGenerator.Create();
        Span<byte> buffer = stackalloc byte[40];
        rng.GetBytes(buffer);
        using var msg = RpcAuthenticationMessage.CreateFromMessage(0x3, "admin", "a password text", buffer);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(msg.LoginType, Is.EqualTo(0x3));
            Assert.That(msg.UserName, Is.EqualTo("admin"));
            Assert.That(msg.Password, Is.EqualTo("a password text"));
            Assert.That(msg.KeySignedData.SequenceEqual(buffer),
                $"{nameof(msg.KeySignedData)} is not equal {nameof(buffer)}");
        }

        using var msg2 = RpcAuthenticationMessage.CreateFromMemory(msg.Memory);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(msg2.LoginType, Is.EqualTo(0x3));
            Assert.That(msg2.UserName, Is.EqualTo("admin"));
            Assert.That(msg2.Password, Is.EqualTo("a password text"));
            Assert.That(msg2.KeySignedData.SequenceEqual(buffer),
                $"{nameof(msg2.KeySignedData)} is not equal {nameof(buffer)}");
        }
    }

    [Test]
    public void TestTypeInfo()
    {
        
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