using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
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
        var v = "abcdefghijklmnop";
        var indexOf = v.IndexOf('b');
        var methodInfo = typeof(string).GetMethod(nameof(string.IndexOf), [typeof(char)])!;
        var method = DynamicMethodInvokeGenerator.GenerateInstanceMethod(methodInfo);
        Assert.That(method(v, ['b']), Is.EqualTo(indexOf));
        TestClass<int> testClass = new();
        var info = typeof(TestClass<int>).GetMethod(nameof(TestClass<>.Test));
        var stm = typeof(TestClass<int>).GetMethod(nameof(TestClass<>.TestStat));
        var genericMethod = info!.MakeGenericMethod(typeof(int));
        var func = DynamicMethodInvokeGenerator.GenerateInstanceMethod(genericMethod);
        Assert.That(func(testClass, [1, 2]), Is.EqualTo(1));
        var stmFunc = DynamicMethodInvokeGenerator.GenerateStaticMethod(stm!);
        Assert.That(stmFunc([1]), Is.EqualTo(1));
        var caller = DynamicMethodInvokeGenerator.GenerateInstanceMethodCaller(genericMethod);
        Assert.That(caller.Call(testClass, [1, 2]), Is.EqualTo(1));
        var expressionMethod = DynamicMethodInvokeGenerator.GenerateInstanceExpressionMethod(genericMethod);
        Assert.That(expressionMethod(testClass, [1, 2]), Is.EqualTo(1));
        var sCaller = DynamicMethodInvokeGenerator.GenerateStaticMethodCaller(stm!);
        Assert.That(sCaller.Call([1]), Is.EqualTo(1));
        var expressionStatic = DynamicMethodInvokeGenerator.GenerateStaticExpressionMethod(stm!);
        Assert.That(expressionStatic([1]), Is.EqualTo(1));
    }


    [Test]
    public void ExpressionTest()
    {
        Expression<Func<object, object[], object>> expression = (a, b) =>
            ((TestClassNoG)a).TestMethod((string)b[0], (string)b[1]);

        Assert.That(expression, Is.Not.Null);
    }


    [Test]
    public void PerformanceTest()
    {
        TestClassNoG ng = new();
        var methodInfo =
            typeof(TestClassNoG).GetMethod(nameof(TestClassNoG.TestMethod), [typeof(string), typeof(string)])!;
        var method = DynamicMethodInvokeGenerator.GenerateInstanceMethod(methodInfo);
        var caller = DynamicMethodInvokeGenerator.GenerateInstanceMethodCaller(methodInfo);
        var expression = DynamicMethodInvokeGenerator.GenerateInstanceExpressionMethod(methodInfo);
        var sw = Stopwatch.StartNew();
        var func = ng.TestMethod;
        sw.Start();
        for (var i = 0; i < 10; i++)
        {
            for (var j = 0; j < 10000000; j++)
            {
                _ = func("a", "b");
            }
        }

        sw.Stop();
        Debug.WriteLine("原生方法调用 10 x 10000000 耗时 :{0}ms", sw.ElapsedMilliseconds);
        sw.Reset();
        sw.Start();
        for (var i = 0; i < 10; i++)
        {
            for (var j = 0; j < 10000000; j++)
            {
                _ = methodInfo.Invoke(ng, ["a", "b"]);
            }
        }

        sw.Stop();
        Debug.WriteLine("反射调用 10 x 10000000 耗时: {0}ms", sw.ElapsedMilliseconds);
        sw.Reset();
        sw.Start();
        for (var i = 0; i < 10; i++)
        {
            for (var j = 0; j < 10000000; j++)
            {
                _ = method(ng, ["a", "b"]);
            }
        }

        sw.Stop();
        Debug.WriteLine("IL发射调用 10 x 10000000 耗时: {0}ms", sw.ElapsedMilliseconds);
        sw.Reset();
        sw.Start();
        for (var i = 0; i < 10; i++)
        {
            for (var j = 0; j < 10000000; j++)
            {
                _ = caller.Call(ng, ["a", "b"]);
            }
        }

        sw.Stop();

        Debug.WriteLine("IL发射(生成接口)调用 10 x 10000000 耗时: {0}ms", sw.ElapsedMilliseconds);
        sw.Reset();
        sw.Start();
        for (var i = 0; i < 10; i++)
        {
            for (var j = 0; j < 10000000; j++)
            {
                _ = expression(ng, ["a", "b"]);
            }
        }

        sw.Stop();

        Debug.WriteLine("表达式树生成调用 10 x 10000000 耗时: {0}ms", sw.ElapsedMilliseconds);
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

public class TestClassNoG
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string TestMethod(string s, string b)
    {
        return s + b;
    }
}

public class TestClass<TU>
{
    public T Test<T>(T input, TU output)
    {
        return input;
    }

    public static TU TestStat(TU input)
    {
        return input;
    }
}