using System.Windows.Media;
using System.Windows.Media.Imaging;
using AiUsageBar.Core;

namespace AiUsageBar;

public static class ProviderIcons
{
    public static ImageSource? Load(string providerId, bool light, int size)
    {
        var path = ProviderIconFiles.Resolve(providerId, light);
        if (path is null)
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.DecodePixelWidth = Math.Max(12, size);
            image.DecodePixelHeight = Math.Max(12, size);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
