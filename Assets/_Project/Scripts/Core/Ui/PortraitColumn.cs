using System;

namespace Template.Core.Ui
{
    /// <summary>
    /// The UI is laid out for phones and tablets, 9:21 up to 3:4. A screen wider than 3:4 (a desktop window) keeps a
    /// centred column of that shape, so layouts and the gameplay view stay the shapes that were tested on devices.
    /// </summary>
    public static class PortraitColumn
    {
        public const float MaxAspect = 3f / 4f;

        /// <summary>Left edge and width of the column, in whole pixels, for a screen of the given size.</summary>
        public static (int x, int width) For(int screenWidth, int screenHeight)
        {
            int width = Math.Min(screenWidth, (int)Math.Round(screenHeight * (double)MaxAspect, MidpointRounding.AwayFromZero));
            return ((screenWidth - width) / 2, width);
        }
    }
}
