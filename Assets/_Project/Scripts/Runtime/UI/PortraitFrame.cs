using Template.Core.Ui;
using UnityEngine;

namespace Template.UI
{
    /// <summary>
    /// Keeps a desktop window that is wider than 3:4 to a centred portrait column (<see cref="PortraitColumn"/>) with
    /// black bars at the sides. SafeArea and the canvas scaler read <see cref="Column"/>; this component fits every
    /// camera to it.
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
            var go = new GameObject("[PortraitFrame]");
            DontDestroyOnLoad(go);
            var bars = go.AddComponent<Camera>();
            bars.depth = -100f;
            bars.clearFlags = CameraClearFlags.SolidColor;
            bars.backgroundColor = Color.black;
            bars.cullingMask = 0;
            bars.enabled = false;
            return go.AddComponent<PortraitFrame>();
        }

        private Camera _bars;
        private Camera[] _cameras = new Camera[4];

        private void Awake() => _bars = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var column = Column;
            _bars.enabled = column.width < Screen.width;
            var viewport = new Rect(column.x / Screen.width, 0f, column.width / Screen.width, 1f);

            if (_cameras.Length < Camera.allCamerasCount)
            {
                _cameras = new Camera[Camera.allCamerasCount];
            }

            int count = Camera.GetAllCameras(_cameras);
            for (int i = 0; i < count; i++)
            {
                if (_cameras[i] != _bars && _cameras[i].rect != viewport)
                {
                    _cameras[i].rect = viewport;
                }
            }
        }
    }
}
