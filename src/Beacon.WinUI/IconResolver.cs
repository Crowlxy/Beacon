using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Beacon.Contracts;
using Beacon.Platform.Windows;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;

namespace Beacon.WinUI;

internal sealed class IconResolver
{
    private readonly ConcurrentDictionary<IconDescriptor, Task<ImageSource?>> _cache = new();

    public Task<ImageSource?> ResolveAsync(IconDescriptor descriptor) =>
        _cache.GetOrAdd(descriptor, ResolveCoreAsync);

    private static async Task<ImageSource?> ResolveCoreAsync(IconDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Value) ||
            descriptor.Source is IconSource.None or IconSource.FluentGlyph or IconSource.ProviderIcon) return null;
        try
        {
            if (descriptor.Source == IconSource.UriOrDataUri) return new BitmapImage(new Uri(descriptor.Value));
            if (descriptor.Source == IconSource.FileShellIcon) return await ResolveShellIconAsync(descriptor.Value);
            if (descriptor.Source == IconSource.FileThumbnail) return await ResolveShellThumbnailAsync(descriptor.Value);
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(descriptor.Value));
            if (descriptor.Source == IconSource.FilePath)
            {
                using var stream = await file.OpenReadAsync();
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                return bitmap;
            }
            return await ResolveShellIconAsync(descriptor.Value);
        }
        catch (Exception exception) when (IsExpectedFailure(exception)) { return null; }
    }

    private static async Task<ImageSource> ResolveShellIconAsync(string path)
    {
        var pixels = await Task.Run(() => ShellIconService.GetIcon(path));
        var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
        using var stream = bitmap.PixelBuffer.AsStream();
        await stream.WriteAsync(pixels.BgraPixels);
        bitmap.Invalidate();
        return bitmap;
    }

    private static async Task<ImageSource> ResolveShellThumbnailAsync(string path)
    {
        var pixels = await Task.Run(() => ShellIconService.GetThumbnail(path));
        var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
        using var stream = bitmap.PixelBuffer.AsStream();
        await stream.WriteAsync(pixels.BgraPixels);
        bitmap.Invalidate();
        return bitmap;
    }

    private static bool IsExpectedFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or COMException or Win32Exception;
}
