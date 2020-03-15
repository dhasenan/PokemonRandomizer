using NUnit.Framework;

namespace Ikeran.Util.Test
{
    [TestFixture]
    public class SliceTest
    {
        private byte[] bytes;
        private Slice<byte> target;

        [SetUp]
        public void Setup()
        {
            bytes = new byte[512];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)i;
            }
            target = new Slice<byte>(bytes);
        }

        [Test]
        public void AfterWithinRange()
        {
            var after = target.After(3);
            Assert.That(after.Offset, Is.EqualTo(3));
            Assert.That(after[0], Is.EqualTo(3));
            after = after.After(5);
            Assert.That(after.Offset, Is.EqualTo(8));
            Assert.That(after[0], Is.EqualTo(8));
        }
    }
}
