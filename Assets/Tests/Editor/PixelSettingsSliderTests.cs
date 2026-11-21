using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.Tests
{
    public class PixelSettingsSliderTests
    {
        private static Behaviour Attach(Slider slider)
        {
            var type = Type.GetType("KillingMahjong.UI.PixelSettingsSlider, Assembly-CSharp", true);
            var skin = (Behaviour)type.GetMethod("Attach").Invoke(null, new object[] { slider });
            // 通常のMonoBehaviourはEditModeではライフサイクルが走らない。
            if (slider.transform.Find("PixelSliderArt") == null)
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetMethod("Awake", flags).Invoke(skin, null);
                type.GetMethod("OnEnable", flags).Invoke(skin, null);
            }
            return skin;
        }

        [Test]
        public void SkinPreservesContinuousValuesListenersAndInputRect()
        {
            var root = DefaultControls.CreateSlider(new DefaultControls.Resources());
            try
            {
                var slider = root.GetComponent<Slider>();
                var rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(300, 30);
                slider.SetValueWithoutNotify(.371f);
                int calls = 0;
                slider.onValueChanged.AddListener(value => calls++);
                var size = rect.sizeDelta;
                var handle = slider.handleRect;
                var type = Type.GetType("KillingMahjong.UI.PixelSettingsSlider, Assembly-CSharp", true);
                var skin = Attach(slider);
                Assert.That(slider.value, Is.EqualTo(.371f));
                Assert.That(slider.wholeNumbers, Is.False);
                Assert.That(calls, Is.EqualTo(0));
                Assert.That(rect.sizeDelta, Is.EqualTo(size));
                Assert.That(slider.handleRect, Is.SameAs(handle));
                slider.value = .823f;
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(slider.value, Is.EqualTo(.823f));
                skin.enabled = false;
                type.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(skin, null);
                Assert.That(handle.GetComponent<Image>().enabled, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(0f)]
        [TestCase(.371f)]
        [TestCase(1f)]
        public void AllDrawnCornersStayOnTwoUnitGrid(float value)
        {
            var root = DefaultControls.CreateSlider(new DefaultControls.Resources());
            var mesh = new Mesh();
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 30);
                var slider = root.GetComponent<Slider>();
                slider.SetValueWithoutNotify(value);
                var type = Type.GetType("KillingMahjong.UI.PixelSettingsSlider, Assembly-CSharp", true);
                var skin = Attach(slider);
                type.GetMethod("Refresh").Invoke(skin, null);
                var art = root.transform.Find("PixelSliderArt").GetComponent<Graphic>();
                using (var helper = new VertexHelper())
                {
                    art.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null)
                        .Invoke(art, new object[] { helper });
                    helper.FillMesh(mesh);
                }
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                foreach (var vertex in mesh.vertices)
                {
                    Assert.That(vertex.x / 2, Is.EqualTo(Mathf.Round(vertex.x / 2)).Within(.0001f));
                    Assert.That(vertex.y / 2, Is.EqualTo(Mathf.Round(vertex.y / 2)).Within(.0001f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
