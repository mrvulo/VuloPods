using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using VuloPods.Core.Models;

namespace VuloPods.App;

/// <summary>Zeichnet den Akku-Prozentwert als Tray-Icon (transparenter Hintergrund, fette Zahl, goldener Balken unten).</summary>
public static class TrayIconRenderer
{
    private const int SM_CXSMICON = 49;
    private const int BarHeight = 2;
    private static readonly Color Gold = Color.FromArgb(0xD4, 0xA0, 0x17);
    private static readonly Color Low = Color.FromArgb(0xFF, 0x45, 0x3A);
    private static readonly Color Charging = Color.FromArgb(0x30, 0xD1, 0x58);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>Breite des kleinen Icons für das System-DPI; Fallback 16.</summary>
    public static int GetSmallIconSize()
    {
        try
        {
            var size = GetSystemMetricsForDpi(SM_CXSMICON, GetDpiForSystem());
            return size > 0 ? size : 16;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return 16;
        }
    }

    /// <summary>Der Aufrufer besitzt das zurückgegebene Icon und muss es freigeben.</summary>
    public static Icon Render(TrayBadge badge, bool lightTaskbar, int sizePx)
    {
        var textColor = badge.Level switch
        {
            BadgeLevel.Low => Low,
            BadgeLevel.Charging => Charging,
            _ => lightTaskbar ? Color.FromArgb(0x1E, 0x1E, 0x22) : Color.White,
        };

        using var bitmap = new Bitmap(sizePx, sizePx, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var barHeight = Math.Min(BarHeight, sizePx);
            var textArea = new RectangleF(0, 0, sizePx, sizePx - barHeight);
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };

            // Größte Schrift, bei der der Text höchstens 95 % der Breite belegt.
            var fontSize = sizePx * 0.9f;
            var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            try
            {
                while (fontSize > 4f && g.MeasureString(badge.Text, font, int.MaxValue, format).Width > sizePx * 0.95f)
                {
                    fontSize -= 0.5f;
                    font.Dispose();
                    font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                }

                using var brush = new SolidBrush(textColor);
                g.DrawString(badge.Text, font, brush, textArea, format);
            }
            finally
            {
                font.Dispose();
            }

            using var bar = new SolidBrush(Gold);
            g.FillRectangle(bar, 0, sizePx - barHeight, sizePx, barHeight);
        }

        // GetHicon liefert ein Handle, das wir besitzen: klonen (Klon besitzt sein eigenes Handle) und das Original zerstören.
        var hIcon = bitmap.GetHicon();
        try
        {
            using var wrapper = Icon.FromHandle(hIcon);
            return (Icon)wrapper.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
