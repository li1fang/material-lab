using NUnit.Framework;

namespace MaterialDestruction.Tests
{
    public sealed class RibbonTests
    {
        [Test]
        public void PausingReleasingAndRegrippingPreservesTheFront()
        {
            var strip=new RibbonTear();
            strip.Grab(1);strip.Open(.008f);
            Assert.AreEqual(0,strip.Broken);
            strip.Open(.132f);
            int half=strip.Broken;
            Assert.That(half,Is.InRange(25,33));
            strip.Open(.132f);strip.Open(.025f);
            Assert.AreEqual(half,strip.Broken);
            var first=strip.Release();
            strip.Open(.2f);
            Assert.AreEqual(half,strip.Broken);
            strip.Grab(-1);strip.Open(.01f);
            Assert.AreEqual(half,strip.Broken);
            strip.Open(.075f);
            var second=strip.Release();
            Assert.That(strip.Broken,Is.GreaterThan(half));
            Assert.AreEqual(strip.Broken,new RibbonTake {grips=new[]{first,second}}.Replay());
            strip.Grab(1); strip.Open(.4f);
            Assert.IsTrue(strip.Split);
        }
    }
}
