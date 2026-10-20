using Template.Core.Ui;
using UnityEngine;

namespace Template.UI
{
    /// <summary>
    /// Keeps a desktop window that is wider than 3:4 to a centred portrait column (<see cref="PortraitColumn"/>) with
    /// black bars at the sides. SafeArea and the canvas scaler read <see cref="Column"/>; this component fits every
    /// camera to it and draws the bars on a canvas above all others, so popup dims stay inside the column too.
    /// </summary>
    public sealed class PortraitFrame : MonoBehaviour
    {
        /// <summary>
        /// On in desktop players only. Phones and tablets are portrait already, and in the Editor the screen is the
        /// desktop, which would squeeze test captures; tests switch it on to check the desktop layout.
        /// </summary>
        public static bool Enabled = !Application.isEditor && !Application.isMobilePlatform;

        /// <summary>The column in screen pixels: the whole screen unless the frame is on and the screen is wider than 3:4.</summary>
        public static Rect Column
        {
            get
            {
                if (!Enabled)
                {
                    return new Rect(0f, 0f, Screen.width, Screen.height);
                }

                var (x, width) = PortraitColumn.For(Screen.width, Screen.height);
                return new Rect(x, 0f, width, Screen.height);
            }
        }

        public static PortraitFrame Create()
        {
            var canvas = UiFactory.CreateCanvas("[PortraitFrame]", short.MaxValue);
            DontDestroyOnLoad(canvas.gameObject);
            var frame = canvas.gameObject.AddComponent<PortraitFrame>();
            frame._left = Bar(canvas.transform);
            frame._right = Bar(canvas.transform);
            return frame;
        }

        private static RectTransform Bar(Transform parent)
        {
            var bar = UiFactory.CreatePanel(parent, Color.black);
            bar.raycastTarget = false;
            bar.gameObject.SetActive(false);
            return bar.rectTransform;
        }

        private RectTransform _left;
        private RectTransform _right;
        private Rect _barsFor;
        private Camera[] _cameras = new Camera[4];

        private void LateUpdate()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var column = Column;
            var viewport = new Rect(column.x / Screen.width, 0f, column.width / Screen.width, 1f);
            if (column != _barsFor)
            {
                _barsFor = column;
                bool bars = column.width < Screen.width;
                _left.gameObject.SetActive(bars);
                _right.gameObject.SetActive(bars);
                _left.anchorMax = new Vector2(viewport.xMin, 1f);
                _right.anchorMin = new Vector2(viewport.xMax, 0f);
            }

            if (_cameras.Length < Camera.allCamerasCount)
            {
                _cameras = new Camera[Camera.allCamerasCount];
            }

            int count = Camera.GetAllCameras(_cameras);
            for (int i = 0; i < count; i++)
            {
                if (_cameras[i].rect != viewport)
                {
                    _cameras[i].rect = viewport;
                }
            }
        }
    }
}
