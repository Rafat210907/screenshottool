// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — BitmapHelper                                  ║
// ║  Utility methods for bitmap conversion, saving, and filename    ║
// ║  generation. Bridges System.Drawing ↔ WPF BitmapSource.         ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Helpers;

public static class BitmapHelper
{
    /// <summary>
    /// Convert System.Drawing.Bitmap to WPF BitmapSource.
    /// </summary>
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    // ─── Save to Disk (PNG / JPG / BMP) ─────────────────────────────
    /// <summary>
    /// Save a bitmap in the specified format with quality settings.
    /// </summary>
    public static void SaveBitmap(Bitmap bitmap, string filePath, string format, int jpegQuality = 90)
    {
        switch (format.ToLowerInvariant())
        {
            case "jpg":
            case "jpeg":
                var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
                if (jpegEncoder != null)
                {
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)jpegQuality);
                    bitmap.Save(filePath, jpegEncoder, encoderParams);
                }
                else
                {
                    bitmap.Save(filePath, ImageFormat.Jpeg);
                }
                break;

            case "bmp":
                bitmap.Save(filePath, ImageFormat.Bmp);
                break;

            case "png":
            default:
                bitmap.Save(filePath, ImageFormat.Png);
                break;
        }
    }

    // ─── Filename Generation ────────────────────────────────────────
    /// <summary>
    /// Generate a filename for a screenshot: "Screenshot YYYY-MM-DD HHMMSS"
    /// </summary>
    public static string GenerateFileName(string format)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        var ext = format.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "jpg",
            "bmp" => "bmp",
            _ => "png"
        };
        return $"Screenshot {timestamp}.{ext}";
    }

    // ─── Internal: Codec Lookup ─────────────────────────────────────
    private static ImageCodecInfo? GetEncoder(ImageFormat format)
    {
        return ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(e => e.FormatID == format.Guid);
    }
}
