using NUnit.Framework;
using SmartLife.Core;
using SmartLife.Voice;

namespace SmartLife.Tests
{
    public class VoiceCommandRouterTests
    {
        [TestCase("skip step", OverrideCommand.Skip)]
        [TestCase("Reset task", OverrideCommand.Reset)]
        [TestCase("emergency pause", OverrideCommand.Pause)]
        [TestCase("please resume", OverrideCommand.Resume)]
        [TestCase("stop and skip", OverrideCommand.Pause)] // safety wins
        public void Maps_phrases(string phrase, OverrideCommand expected)
        {
            Assert.IsTrue(VoiceCommandRouter.TryParse(phrase, out var cmd));
            Assert.AreEqual(expected, cmd);
        }

        [Test]
        public void Ignores_unrelated_speech() => Assert.IsFalse(VoiceCommandRouter.TryParse("i like apples", out _));
    }
}
