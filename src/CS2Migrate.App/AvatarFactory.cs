using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CS2Migrate.Core.Models;

namespace CS2Migrate.App;

internal static class AvatarFactory
{
    private const int Size = 128;

    public static ImageSource Create(SteamAccount account)
    {
        if (!string.IsNullOrWhiteSpace(account.AvatarPath))
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.UriSource = new Uri(account.AvatarPath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception exception) when (exception is
                IOException or
                UnauthorizedAccessException or
                NotSupportedException or
                UriFormatException or
                ArgumentException)
            {
                // Fall through to a deterministic offline avatar.
            }
        }

        return CreateGenerated(account.SteamId64.ToString(CultureInfo.InvariantCulture), account.DisplayName);
    }

    /// <summary>Windows draws accountless users as a flat coloured disc with their initial.</summary>
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x0F, 0x6C, 0xBD),
        Color.FromRgb(0x11, 0x5E, 0xA3),
        Color.FromRgb(0x5C, 0x2E, 0x91),
        Color.FromRgb(0x87, 0x64, 0xB8),
        Color.FromRgb(0x98, 0x6F, 0x0B),
        Color.FromRgb(0xA4, 0x26, 0x2C),
        Color.FromRgb(0x0E, 0x70, 0x0E),
        Color.FromRgb(0x00, 0x7A, 0x7A)
    ];

    private static ImageSource CreateGenerated(string seed, string displayName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var fill = new SolidColorBrush(Palette[hash[0] % Palette.Length]);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawEllipse(fill, null, new Point(Size / 2d, Size / 2d), Size / 2d, Size / 2d);

            var initial = displayName.Trim().FirstOrDefault();
            var label = initial == default ? "?" : char.ToUpper(initial, CultureInfo.CurrentCulture).ToString();
            var text = new FormattedText(
                label,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    new FontFamily("Segoe UI Variable Text, Segoe UI"),
                    FontStyles.Normal,
                    FontWeights.SemiBold,
                    FontStretches.Normal),
                52,
                Brushes.White,
                1.0);
            context.DrawText(text, new Point((Size - text.Width) / 2, (Size - text.Height) / 2));
        }

        var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
