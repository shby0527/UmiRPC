using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DynamicProxy;
using Moq;
using Umi.Proxy.Dynamic.Aspect;
using Umi.Proxy.Dynamic.Dynamic;

namespace UnitTest;

public class AutoProxyTest
{
    private IInvoker _invoker;
    private IInterceptor _interceptor;

    [SetUp]
    public void Setup()
    {
        Mock<IInvoker> mock = new(MockBehavior.Default);
        mock.Setup(e => e.Process(It.IsAny<IMethodInvocation>()))
            .Callback((IMethodInvocation invocation) => { invocation.ReturnValue = invocation.GetArgumentValue(0); });
        _invoker = mock.Object;
        Mock<IInterceptor> interceptor = new(MockBehavior.Default);
        interceptor.Setup(e => e.BeforeInvoke(It.IsAny<IMethodInvocation>()))
            .Callback((IMethodInvocation invocation) => { })
            .Returns(true);
        interceptor.Setup(e => e.AfterInvoke(It.IsAny<IMethodInvocation>()));
        interceptor.Setup(e => e.ExceptionInvoke(It.IsAny<IMethodInvocation>(), It.IsAny<Exception>()));
        _interceptor = interceptor.Object;
    }

    [Test]
    public void Test1()
    {
        var type = AssemblyGenerator.GetOrGenerateType(typeof(ITest));
        var instance =
            Activator.CreateInstance(type, _invoker, new RandomRaisePublish(), (IInterceptor[])[_interceptor]);
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
        var info1 = typeof(TestClass<int>).GetMethod(nameof(TestClass<>.TestMethod))!;
        var infoTest = DynamicMethodInvokeGenerator.GenerateInstanceExpressionMethod(info1);
        infoTest(testClass, []);
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


    [Test]
    public void ProxyObjectTest()
    {
        var type = AssemblyGenerator.GetOrGenerateType(typeof(ITest));
        RandomRaisePublish publish = new();
        var o = Activator.CreateInstance(type, _invoker, publish, (IInterceptor[])[_interceptor]);
        Assert.That(o, Is.Not.Null);
        Assert.That(o, Is.InstanceOf<ITest>());
        var test = o as ITest;
        var f = test!.Test(1);
        Assert.That(f, Is.EqualTo(1));
        test!.TestEvent += (sender, args) => Assert.That(args, Is.InstanceOf<EventArgs>());
        test!.TestEvent2 += (sender, args) => Assert.That(sender, Is.EqualTo(test));
        test!.TestEvent2 += (sender, args) => Debug.WriteLine(sender!.ToString());
        test!.TestEvent2 += (sender, args) => Debug.WriteLine(sender!.ToString());
        publish.Raise();
    }
}

public class RandomRaisePublish : EventProcessorBase
{
    private readonly Dictionary<Guid, ISet<string>> _dictionary = new();

    public override void Subscribe(IEventRaise raise, string eventName)
    {
        base.Subscribe(raise, eventName);
        var collection = _dictionary.GetOrDefault(raise.ObjectUuid, () => new HashSet<string>());
        collection.Add(eventName);
    }

    public override void Unsubscribe(IEventRaise raise, string eventName)
    {
        if (_dictionary.TryGetValue(raise.ObjectUuid, out var set))
        {
            set.Remove(eventName);
            if (set.Count == 0)
            {
                _dictionary.Remove(raise.ObjectUuid);
                _events.TryRemove(raise.RaiseUuid, out var dic);
                dic?.TryRemove(raise.ObjectUuid, out _);
            }
        }
    }

    public void Raise()
    {
        foreach (var type in _events)
        {
            foreach (var reference in type.Value)
            {
                if (reference.Value.TryGetTarget(out var target))
                {
                    if (_dictionary.TryGetValue(target.ObjectUuid, out var set))
                    {
                        foreach (var e in set)
                        {
                            target.RaiseEvent(e, EventArgs.Empty);
                        }
                    }
                }
            }
        }
    }
}

public interface ITest
{
    float Test(float input);

    event EventHandler TestEvent;

    event EventHandler TestEvent2;
}

public class TestClassNoG
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string TestMethod(string s, string b)
    {
        return s + b;
    }

    public void Test()
    {
    }
}

public class TestClass<TU>
{
    public T Test<T>(T input, TU output)
    {
        return input;
    }

    public void TestMethod()
    {
        Thread.Sleep(500);
    }

    public static TU TestStat(TU input)
    {
        return input;
    }
}