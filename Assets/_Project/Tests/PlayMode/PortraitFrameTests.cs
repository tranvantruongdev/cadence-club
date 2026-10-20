using System.Collections;
using NUnit.Framework;
using Template.Core.Ui;
using Template.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Template.Runtime.Tests
{
    /// <summary>
    /// The Editor's screen is the desktop, usually wider than 3:4, so switching the frame on here exercises the desktop
    /// player's pillarbox: cameras, safe area and canvas scale all follow the portrait column.
    /// </summary>
    public sealed class PortraitFrameTests
    {
        private bool _wasEnabled;
        private GameObject[] _objects;

        [SetUp]
        public void SetUp() => _wasEnabled = PortraitFrame.Enabled;

        [TearDown]
        public void TearDown()
        {
            PortraitFrame.Enabled = _wasEnabled;
            foreach (var go in _objects ?? new GameObject[0])
            {
                Object.Destroy(go);
            }
        }

        [UnityTest]
        public IEnumerator Frame_on_keeps_camera_ui_and_scale_in_the_column()
        {
            PortraitFrame.Enabled = true;
            var (camera, canvas, safe, frame) = Build();
            yield return null;
            yield return null;

            var (x, width) = PortraitColumn.For(Screen.width, Screen.height);
            TestContext.WriteLine($"screen {Screen.width}x{Screen.height}, column x={x} width={width}");
            Assert.That(camera.pixelRect.x, Is.EqualTo(x).Within(1f), "camera left edge");
            Assert.That(camera.pixelRect.width, Is.EqualTo(width).Within(1f), "camera width");

            var corners = new Vector3[4];
            safe.GetWorldCorners(corners); // overlay canvas: world space is screen pixels
            Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(x - 1f), "safe area left edge");
            Assert.That(corners[2].x, Is.LessThanOrEqualTo(x + width + 1f), "safe area right edge");

            float expected = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / 1080f, 2f), Mathf.Log(Screen.height / 1920f, 2f), 0.5f));
            Assert.That(canvas.scaleFactor, Is.EqualTo(expected).Within(1e-3f), "canvas scale from the column");

            // Bars sit on a canvas above the fade (1000) and every popup, so a full-screen dim can't tint them.
            Assert.That(frame.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(1000), "bars above all UI");
            var left = (RectTransform)frame.transform.GetChild(0);
            var right = (RectTransform)frame.transform.GetChild(1);
            bool wide = width < Screen.width;
            Assert.AreEqual(wide, left.gameObject.activeSelf, "left bar shown only when the screen is wider than the column");
            Assert.AreEqual(wide, right.gameObject.activeSelf, "right bar shown only when the screen is wider than the column");
            if (wide)
            {
                left.GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.EqualTo(0f).Within(1f), "left bar from the screen edge");
                Assert.That(corners[2].x, Is.EqualTo(x).Within(1f), "left bar to the column");
                right.GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.EqualTo(x + width).Within(1f), "right bar from the column");
                Assert.That(corners[2].x, Is.EqualTo(Screen.width).Within(1f), "right bar to the screen edge");
            }
        }

        [UnityTest]
        public IEnumerator Frame_off_leaves_the_whole_screen()
        {
            PortraitFrame.Enabled = false;
            var (camera, _, safe, frame) = Build();
            yield return null;
            yield return null;

            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), camera.rect);
            Assert.IsFalse(frame.transform.GetChild(0).gameObject.activeSelf, "no left bar");
            Assert.IsFalse(frame.transform.GetChild(1).gameObject.activeSelf, "no right bar");
            var corners = new Vector3[4];
            safe.GetWorldCorners(corners);
            Assert.That(corners[2].x - corners[0].x, Is.EqualTo(Screen.safeArea.width).Within(1f));
        }

        private (Camera, Canvas, RectTransform, PortraitFrame) Build()
        {
            var camera = new GameObject("Test Camera").AddComponent<Camera>();
            var frame = PortraitFrame.Create();
            var canvas = UiFactory.CreateCanvas("Test UI");
            var safe = UiFactory.CreateSafeArea(canvas.transform);
            _objects = new[] { camera.gameObject, frame.gameObject, canvas.gameObject };
            return (camera, canvas, safe, frame);
        }
    }
}
