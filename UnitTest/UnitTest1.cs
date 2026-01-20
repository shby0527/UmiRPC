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
        ITest? a = null;
        ITest? b = null;
        var d = a.TestInt;
    }
}

public interface ITest
{
    static abstract ITest operator +(ITest l, ITest b);

    static abstract event EventHandler TestEvent;

    static abstract int TestMethod();

    static abstract int Test { get; }
    
    int TestInt { get; }
}