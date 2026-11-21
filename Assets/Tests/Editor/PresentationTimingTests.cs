using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace KillingMahjong.Tests
{
    public class PresentationTimingTests
    {
        private static Type Timing => Type.GetType("KillingMahjong.UI.PresentationTiming, Assembly-CSharp", true);
        private static float Sample(float seconds, float fps = 60f) =>
            (float)Timing.GetMethod("Sample").Invoke(null, new object[] { seconds, fps });

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void DisplayUsesSixtyPosesPerSecondWithinRenderRate(int renderFps)
        {
            var poses = new HashSet<float>();
            for (int frame = 0; frame < renderFps; frame++) poses.Add(Sample(frame / (float)renderFps));
            Assert.That(poses.Count, Is.EqualTo(Math.Min(renderFps, 60)));
            Assert.That(Sample(.001f), Is.EqualTo(Sample(.015f)));
            Assert.That(Sample(.02f), Is.GreaterThan(Sample(.015f)));
            Assert.That((float)Timing.GetField("TextFps").GetRawConstantValue(), Is.EqualTo(60f));
        }

        [TestCase(.04f)]
        [TestCase(.14f)]
        [TestCase(.35f)]
        public void ProgressReachesExactEndEvenForShortOrNonFrameAlignedDuration(float duration)
        {
            var progress = Timing.GetMethod("Progress");
            Assert.That(progress.Invoke(null, new object[] { duration, duration, 60f }), Is.EqualTo(1f));
            Assert.That(progress.Invoke(null, new object[] { duration + .01f, duration, 60f }), Is.EqualTo(1f));
            Assert.That(progress.Invoke(null, new object[] { -.01f, duration, 60f }), Is.EqualTo(0f));
        }
    }
}
