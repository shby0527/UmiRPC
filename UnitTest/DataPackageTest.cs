using System.Dynamic;
using System.Security.Cryptography;
using Umi.Rpc.Base;
using Umi.Rpc.Protocol;

namespace UnitTest;

public class DataPackageTest
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestBasicPackage()
    {
        using var package = RpcBasic.CreateFromMessage(0x4123);
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
    public void MetadataConsentTest()
    {
        var services = new RpcMetadataServiceWrap[]
        {
            new(0x1, "Service-A", "ABCDE"),
            new(0x1, "Service-B", "ABCDE"),
            new(0x1, "Service-C", "ABCDE"),
            new(0x1, "Service-D", "ABCDE"),
            new(0x2, "Service-A", "ABCDE"),
            new(0x3, "Service-A", "ABCDE"),
        };
        var typeMapping = new RpcMetadataTypeMappingWrap[]
        {
            new("System.String", "string"),
            new("System.Int16", "short"),
        };
        var eventHandles = new RpcMetadataEventWrap[]
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "string", "Update")
        };
        using var consent =
            RpcMetadataConsent.CreateFromMessage(0x5, new RpcMetadataContentWrap(services, typeMapping, eventHandles));
        using var reload = RpcMetadataConsent.CreateFromMemory(consent.Memory);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reload.Serialization, Is.EqualTo(consent.Serialization));
            Assert.That(reload.PackageLength, Is.EqualTo(consent.PackageLength));
            Assert.That(reload.ContentHeaderCount, Is.EqualTo(consent.ContentHeaderCount));
            Assert.That(reload.StringPoolOffset, Is.EqualTo(consent.StringPoolOffset));
            Assert.That(consent.ContentHeaderCount, Is.EqualTo(3));
            for (var i = 0; i < consent.ContentHeaders.Length; i++)
            {
                Assert.That(reload.ContentHeaders[i], Is.EqualTo(consent.ContentHeaders[i]));
            }

            var servicesHeader = consent.GetContentHeader<RpcMetadataService>(
                consent.ContentHeaders[0].Offset,
                consent.ContentHeaders[0].Count);
            var reloadServiceHeader = reload.GetContentHeader<RpcMetadataService>(
                reload.ContentHeaders[0].Offset,
                reload.ContentHeaders[0].Count);
            Assert.That(servicesHeader.Length, Is.EqualTo(reloadServiceHeader.Length));
            for (var j = 0; j < servicesHeader.Length; j++)
            {
                Assert.That(servicesHeader[j], Is.EqualTo(reloadServiceHeader[j]));
                Assert.That(servicesHeader[j].Version, Is.EqualTo(services[j].Version));
                Assert.That(
                    consent.GetString(servicesHeader[j].NameOffset, servicesHeader[j].NameLength),
                    Is.EqualTo(reload.GetString(reloadServiceHeader[j].NameOffset,
                        reloadServiceHeader[j].NameLength)));
                Assert.That(
                    consent.GetString(servicesHeader[j].NameOffset, servicesHeader[j].NameLength),
                    Is.EqualTo(services[j].ServiceName));
                Assert.That(consent.GetString(servicesHeader[j].ImplementOffset, servicesHeader[j].ImplementLength),
                    Is.EqualTo("ABCDE"));
            }

            var typeMappingHeader = consent.GetContentHeader<RpcMetadataTypeMapping>(
                consent.ContentHeaders[1].Offset,
                consent.ContentHeaders[1].Count);
            var reloadTypeMappingHeader = reload.GetContentHeader<RpcMetadataTypeMapping>(
                reload.ContentHeaders[1].Offset,
                reload.ContentHeaders[1].Count);
            Assert.That(typeMappingHeader.Length, Is.EqualTo(reloadTypeMappingHeader.Length));
            for (var i = 0; i < typeMappingHeader.Length; i++)
            {
                Assert.That(typeMappingHeader[i], Is.EqualTo(reloadTypeMappingHeader[i]));
                Assert.That(
                    consent.GetString(typeMappingHeader[i].SourceTypeOffset, typeMappingHeader[i].SourceTypeLength),
                    Is.EqualTo(typeMapping[i].Source));
                Assert.That(
                    consent.GetString(typeMappingHeader[i].TargetTypeOffset, typeMappingHeader[i].TargetTypeLength),
                    Is.EqualTo(typeMapping[i].Target));
            }

            var eventHandleHeader = consent.GetContentHeader<RpcMetadataEventHandle>(
                consent.ContentHeaders[2].Offset,
                consent.ContentHeaders[2].Count);
            var reloadEventHandleHeader = reload.GetContentHeader<RpcMetadataEventHandle>(
                reload.ContentHeaders[2].Offset,
                reload.ContentHeaders[2].Count);
            Assert.That(eventHandleHeader.Length, Is.EqualTo(reloadEventHandleHeader.Length));
            for (var i = 0; i < eventHandleHeader.Length; i++)
            {
                Assert.That(eventHandleHeader[i], Is.EqualTo(reloadEventHandleHeader[i]));
                Assert.That(
                    consent.GetString(eventHandleHeader[i].EventNameOffset, eventHandleHeader[i].EventNameLength),
                    Is.EqualTo(eventHandles[i].EventName));
                Assert.That(consent.GetString(eventHandleHeader[i].TypeNameOffset, eventHandleHeader[i].TypeNameLength),
                    Is.EqualTo(eventHandles[i].TypeName));
            }
        }
    }


    [Test]
    public void RpcCallMessageTest()
    {
        RpcCallMessageWrap wrap = new("TestService",
            ["string", "string"],
            "TestMethod",
            ["int", "string"],
            [[0x00, 0x00, 0x00, 0x01], "你好阿"u8.ToArray()]);

        using var msg = RpcCallMessage.CreateFromMessage(wrap);
        using var re = RpcCallMessage.CreateFromMemory(msg.Memory);
        using (Assert.EnterMultipleScope())
        {
            // 验证
            Assert.That(msg.PackageLength, Is.EqualTo(re.PackageLength));
            Assert.That(msg.StringPoolOffset, Is.EqualTo(re.StringPoolOffset));
            Assert.That(msg.ObjectPoolOffset, Is.EqualTo(re.ObjectPoolOffset));
            Assert.That(msg.ServiceGenericTypeCount, Is.EqualTo(re.ServiceGenericTypeCount));
            Assert.That(msg.MethodGenericTypeCount, Is.EqualTo(re.MethodGenericTypeCount));
            Assert.That(msg.ArgumentCount, Is.EqualTo(re.ArgumentCount));
            Assert.That(msg.ServiceName, Is.EqualTo(re.ServiceName));
            Assert.That(msg.GetString(msg.ServiceName), Is.EqualTo("TestService"));
            Assert.That(msg.MethodName, Is.EqualTo(re.MethodName));
            Assert.That(msg.GetString(msg.MethodName), Is.EqualTo("TestMethod"));
            for (var i = 0; i < msg.ServiceGenericType.Length; i++)
            {
                var actual = msg.ServiceGenericType[i];
                var expected = re.ServiceGenericType[i];
                Assert.That(actual, Is.EqualTo(expected), $"loop {i}");
                Assert.That(msg.GetString(actual), Is.EqualTo(re.GetString(expected)));
                Assert.That(msg.GetString(actual), Is.EqualTo("string"));
            }

            for (var i = 0; i < msg.MethodGenericType.Length; i++)
            {
                var actual = msg.MethodGenericType[i];
                var expected = re.MethodGenericType[i];
                Assert.That(actual, Is.EqualTo(expected), $"loop {i}");
                Assert.That(msg.GetString(actual), Is.EqualTo(re.GetString(expected)));
            }

            for (var i = 0; i < msg.Arguments.Length; i++)
            {
                var actual = msg.Arguments[i];
                var expected = re.Arguments[i];
                Assert.That(actual, Is.EqualTo(expected), $"loop {i}");
                Assert.That(msg.GetObject(actual).SequenceEqual(re.GetObject(expected)), $"loop {i}");
            }
        }
    }


    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void RpcCallResultTest(bool emptySpan)
    {
        using var rand = RandomNumberGenerator.Create();
        var transactionId = Random.Shared.NextInt64();
        Span<byte> buffer;
        if (emptySpan)
        {
            buffer = Span<byte>.Empty;
        }
        else
        {
            buffer = new byte[1024];
            rand.GetNonZeroBytes(buffer);
        }

        using var msg = RpcCallResult.CreateFromMessage(transactionId, RpcCallResultStatus.Completed, buffer);
        using var re = RpcCallResult.CreateFromMemory(msg.Memory);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(msg.TransactionId, Is.EqualTo(transactionId));
            Assert.That(msg.TransactionId, Is.EqualTo(re.TransactionId));
            Assert.That(msg.ResultLength, Is.EqualTo(re.ResultLength));
            Assert.That(msg.ResultLength, Is.EqualTo(buffer.Length));
            Assert.That(msg.Status, Is.EqualTo(RpcCallResultStatus.Completed));
            Assert.That(msg.Status, Is.EqualTo(re.Status));
            Assert.That(msg.Result.SequenceEqual(re.Result), "msg and result not equals");
            Assert.That(msg.Result.SequenceEqual(buffer), "result and buffer equals");
        }
    }


    [Test]
    public void DynamicTest()
    {
        dynamic test = new ExpandoObject();
        test.Id = 1;
        test.Name = "Test";
        test.Description = "Test";
    }
}