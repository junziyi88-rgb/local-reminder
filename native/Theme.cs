using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LocalReminder
{
    internal static class Theme
    {
        public static readonly Brush Ink = Brush("#1D2927");
        public static readonly Brush Muted = Brush("#667572");
        public static readonly Brush Green = Brush("#255C53");
        public static readonly Brush GreenDark = Brush("#163F39");
        public static readonly Brush GreenLight = Brush("#DCEBE6");
        public static readonly Brush Canvas = Brush("#EEF2F3");
        public static readonly Brush Line = Brush("#D9E1E0");
        public static readonly Brush Danger = Brush("#B44949");

        public static Brush Brush(string value)
        {
            return (Brush)new BrushConverter().ConvertFromString(value);
        }

        public static Button Button(string text, bool primary)
        {
            Button button = new Button();
            button.Content = text;
            button.Padding = new Thickness(16, 9, 16, 9);
            button.Margin = new Thickness(4, 0, 0, 0);
            button.BorderThickness = new Thickness(0);
            button.FontWeight = FontWeights.SemiBold;
            button.Cursor = System.Windows.Input.Cursors.Hand;
            button.Foreground = primary ? Brushes.White : Ink;
            button.Background = primary ? Green : Brush("#E8EEEC");
            return button;
        }

        public static TextBlock Text(string value, double size, Brush color, FontWeight weight)
        {
            TextBlock block = new TextBlock();
            block.Text = value;
            block.FontSize = size;
            block.Foreground = color;
            block.FontWeight = weight;
            block.TextWrapping = TextWrapping.Wrap;
            return block;
        }
    }
}
