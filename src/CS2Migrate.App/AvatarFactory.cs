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

    private static ImageSource CreateGenerated(string seed, string displayName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var first = Color.FromRgb(
            (byte)(74 + hash[0] % 80),
            (byte)(64 + hash[1] % 90),
            (byte)(155 + hash[2] % 90));
        var second = Color.FromRgb(
            (byte)(25 + hash[3] % 75),
            (byte)(150 + hash[4] % 90),
            (byte)(150 + hash[5] % 90));

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRoundedRectangle(
                new LinearGradientBrush(first, second, new Point(0, 0), new Point(1, 1)),
                null,
                new Rect(0, 0, Size, Size),
                24,
                24);

            var accent = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255));
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 2; column++)
                {
                    if ((hash[6 + row * 2 + column] & 1) == 0)
                    {
                        continue;
                    }

                    var left = 12 + column * 18;
                    var top = 12 + row * 18;
                    context.DrawRoundedRectangle(accent, null, new Rect(left, top, 12, 12), 4, 4);
                    context.DrawRoundedRectangle(accent, null, new Rect(Size - left - 12, top, 12, 12), 4, 4);
                }
            }

            var initial = displayName.Trim().FirstOrDefault();
            var label = initial == default ? "?" : char.ToUpper(initial, CultureInfo.CurrentCulture).ToString();
            var text = new FormattedText(
                label,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                46,
                Brushes.White,
                1.0);
            context.DrawText(text, new Point((Size - text.Width) / 2, (Size - text.Height) / 2 - 2));
        }

        var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
