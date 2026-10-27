using System;
using System.Drawing;

namespace LocalReminder
{
    internal static class NotificationPlacement
    {
        internal static Rectangle CalculateCorner(Rectangle workArea, double scale, int ordinal)
        {
            int width = Math.Max(1, (int)Math.Round(360 * scale));
            int height = Math.Max(1, (int)Math.Round(126 * scale));
            int margin = Math.Max(1, (int)Math.Round(18 * scale));
            int gap = Math.Max(1, (int)Math.Round(10 * scale));
            int maximumRows = Math.Max(1, (workArea.Height - margin * 2) / (height + gap));
            int row = ordinal % maximumRows;
            int column = ordinal / maximumRows;
            int x = Math.Max(
                workArea.Left + margin,
                workArea.Right - width - margin - column * (width + gap));
            int y = workArea.Top + margin + row * (height + gap);
            return new Rectangle(x, y, width, height);
        }
    }
}
