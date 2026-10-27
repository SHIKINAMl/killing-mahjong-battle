using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KillingMahjong.Tests
{
    public class SeniorBounceAnimationTests
    {
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
                    type.GetField("presentationFrame", flags).SetValue(anim, Mathf.FloorToInt(Time.time * 24f));
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
