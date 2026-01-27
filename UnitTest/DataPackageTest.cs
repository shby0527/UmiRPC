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
    public void MetadataConsentTest()
    {
        string[] names = ["ServiceA", "ServiceB-Test", "ProxyServiceC", "AbcTarget-ServiceD"];
        RpcMetadataService[] versions =
        [
            new(0x1),
            new(0x1),
            new(0x1),
            new(0x1),
        ];
        using var consent = RpcMetadataConsent.CreateFromMessage(0x5, versions, names);
        using var reload = RpcMetadataConsent.CreateFromMemory(consent.Memory);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reload.Serialization, Is.EqualTo(consent.Serialization));
            Assert.That(reload.ServiceArrayLength, Is.EqualTo(consent.ServiceArrayLength));
            Assert.That(reload.StringPoolLength, Is.EqualTo(consent.StringPoolLength));
            Assert.That(reload.RpcMetadataServices.Length, Is.EqualTo(consent.RpcMetadataServices.Length));

            for (var i = 0; i < consent.RpcMetadataServices.Length; i++)
            {
                Assert.That(reload.RpcMetadataServices[i], Is.EqualTo(consent.RpcMetadataServices[i]));
                Assert.That(
                    reload.GetString(reload.RpcMetadataServices[i].NameOffset,
                        reload.RpcMetadataServices[i].NameLength),
                    Is.EqualTo(consent.GetString(consent.RpcMetadataServices[i].NameOffset,
                        consent.RpcMetadataServices[i].NameLength)));
            }
        }
    }
}