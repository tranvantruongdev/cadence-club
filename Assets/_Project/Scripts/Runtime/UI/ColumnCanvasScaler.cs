using UnityEngine;
using UnityEngine.UI;

namespace Template.UI
{
    /// <summary>
    /// Scale With Screen Size measured on the portrait column (<see cref="PortraitFrame"/>) instead of the whole window,
    /// so a wide desktop window does not blow the UI up by its height. Plain CanvasScaler whenever the frame is off.
    /// </summary>
    public sealed class ColumnCanvasScaler : CanvasScaler
    {
        protected override void HandleScaleWithScreenSize()
        {
            if (!PortraitFrame.Enabled || screenMatchMode != ScreenMatchMode.MatchWidthOrHeight)
            {
                base.HandleScaleWithScreenSize();
                return;
            }

            var column = PortraitFrame.Column;
            float logWidth = Mathf.Log(column.width / referenceResolution.x, 2f);
            float logHeight = Mathf.Log(column.height / referenceResolution.y, 2f);
            SetScaleFactor(Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight)));
            SetReferencePixelsPerUnit(referencePixelsPerUnit);
        }
    }
}
