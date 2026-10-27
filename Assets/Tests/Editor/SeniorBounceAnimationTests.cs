using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KillingMahjong.Tests
{
    public class SeniorBounceAnimationTests
    {
        [Test]
        public void TwentyFourFpsBounceHasSmallSilhouetteStepsAndSeamlessLoop()
        {
            var type = Type.GetType("KillingMahjong.UI.SlimeBounceAnimator, Assembly-CSharp", true);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var go = new GameObject("SeniorBounceTrajectory", typeof(SpriteRenderer));
            try
            {
                var anim = go.AddComponent(type);
                var shape = type.GetMethod("Shape", flags);
                float cycle = (float)type.GetField("cycleSeconds", flags).GetValue(anim);
                int frames = Mathf.RoundToInt(cycle * 24f);
                Assert.That(frames, Is.EqualTo(30));
                Vector2? previous = null;
                Vector2 first = Vector2.zero;
                for (int frame = 0; frame <= frames; frame++)
                {
                    object[] args = { frame / (float)frames, 0f, 0f };
                    shape.Invoke(anim, args);
                    float sy = (float)args[1], lift = (float)args[2];
                    // 基準の表示サイズ（高さ380px）で頭の輪郭を追う。急な伸縮と跳躍の両方を検出する。
                    var head = new Vector2(155f / sy, 380f * (.65f * sy + lift));
                    if (frame == 0) first = head;
                    if (previous.HasValue)
                        Assert.That(Vector2.Distance(previous.Value, head), Is.LessThan(5f), "1コマの輪郭移動が大きすぎる: " + frame);
                    previous = head;
                }
                Assert.That(Vector2.Distance(first, previous.Value), Is.LessThan(.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void HeldPoseSurvivesAnotherAnimatorRestoringPositionAndStopsCleanly()
        {
            var type = Type.GetType("KillingMahjong.UI.SlimeBounceAnimator, Assembly-CSharp", true);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var go = new GameObject("SeniorBounceRegression", typeof(SpriteRenderer));
            try
            {
                var anim = go.AddComponent(type);
                type.GetMethod("Play").Invoke(anim, new object[] { true });
                type.GetField("poseScaleY", flags).SetValue(anim, .8f);
                type.GetField("poseLift", flags).SetValue(anim, .04f);
                var home = new Vector3(2f, 3f, 0f);
                Vector3 held = Vector3.zero;
                for (int i = 0; i < 3; i++)
                {
                    // 会話側が毎フレーム初期位置とスケールへ戻す状況を再現する。
                    go.transform.localPosition = home;
                    go.transform.localScale = Vector3.one;
                    float cycle = (float)type.GetField("cycleSeconds", flags).GetValue(anim);
                    float phase = (float)type.GetField("phase", flags).GetValue(anim);
                    type.GetField("presentationFrame", flags).SetValue(anim,
                        Mathf.FloorToInt((phase + Time.deltaTime / cycle) * cycle * 24f));
                    type.GetMethod("LateUpdate", flags).Invoke(anim, null);
                    Assert.That(go.transform.localScale.y, Is.EqualTo(.8f).Within(.0001f));
                    if (i == 0)
                    {
                        held = go.transform.localPosition;
                        Assert.That(held, Is.Not.EqualTo(home));
                    }
                    else Assert.That(go.transform.localPosition, Is.EqualTo(held));
                }
                type.GetMethod("Play").Invoke(anim, new object[] { false });
                Assert.That(go.transform.localPosition, Is.EqualTo(home));
                Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
