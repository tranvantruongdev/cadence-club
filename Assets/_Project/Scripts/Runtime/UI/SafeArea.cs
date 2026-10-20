using UnityEngine;

namespace Template.UI
{
    /// <summary>Keeps a RectTransform inside the screen's safe area (notches, punch holes, rounded corners).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastArea;
        private Vector2Int _lastSize;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastArea || Screen.width != _lastSize.x || Screen.height != _lastSize.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            var area = Screen.safeArea;
            _lastArea = area;
            _lastSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            // A wide desktop window keeps the UI in the portrait column (PortraitFrame); on phones the column is the screen.
            var column = PortraitFrame.Column;
            var min = new Vector2(Mathf.Max(area.xMin, column.xMin), area.yMin);
            var max = new Vector2(Mathf.Min(area.xMax, column.xMax), area.yMax);
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
