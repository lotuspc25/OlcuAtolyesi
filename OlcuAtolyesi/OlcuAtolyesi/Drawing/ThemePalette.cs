using Microsoft.Maui.Graphics;

namespace OlcuAtolyesi.Drawing;

internal readonly record struct ThemePalette(
    Color Background,
    Color Surface,
    Color Primary,
    Color Accent,
    Color Text,
    Color Muted,
    Color Border,
    Color Warning)
{
    public static ThemePalette Current
    {
        get
        {
            var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            return dark
                ? new ThemePalette(
                    Color.FromArgb("#10181A"),
                    Color.FromArgb("#182326"),
                    Color.FromArgb("#59C4CF"),
                    Color.FromArgb("#8EDCE4"),
                    Color.FromArgb("#F2F7F7"),
                    Color.FromArgb("#B3C4C7"),
                    Color.FromArgb("#3D5054"),
                    Color.FromArgb("#F6C75B"))
                : new ThemePalette(
                    Color.FromArgb("#F5F8F8"),
                    Colors.White,
                    Color.FromArgb("#087F8C"),
                    Color.FromArgb("#0AA4B5"),
                    Color.FromArgb("#172124"),
                    Color.FromArgb("#52666A"),
                    Color.FromArgb("#CBD8DA"),
                    Color.FromArgb("#9A6500"));
        }
    }
}
