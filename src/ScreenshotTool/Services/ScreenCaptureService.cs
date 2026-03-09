// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — ScreenCaptureService                          ║
// ║  Captures screen regions using GDI+ Graphics.CopyFromScreen.    ║
// ║  Supports full virtual screen, specific rectangles, and         ║
// ║  freeform clipping with GraphicsPath.                           ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.Drawing;
using System.Drawing.Drawing2D;
using ScreenshotTool.Helpers;

namespace ScreenshotTool.Services;

public class ScreenCaptureService
{
    // ─── Capture a Rectangular Region ─────────────────────────────
    /// <summary>
    /// Capture a rectangular area of the screen at physical pixel coordinates.
    /// </summary>
    public Bitmap CaptureRegion(int x, int y, int width, int height, Bitmap? baseCapture = null)
    {
        var bitmap = new Bitmap(width, height);
        using var g = Graphics.FromImage(bitmap);
        
        if (baseCapture != null)
        {
            // Crop from the frozen screen
            int screenLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            int screenTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            int sourceX = x - screenLeft;
            int sourceY = y - screenTop;
            g.DrawImage(baseCapture, 
                new Rectangle(0, 0, width, height), 
                new Rectangle(sourceX, sourceY, width, height), 
                GraphicsUnit.Pixel);
        }
        else
        {
            // Fallback: capture live screen
            g.CopyFromScreen(x, y, 0, 0, new Size(width, height));
        }
        
        return bitmap;
    }

    // ─── Capture the Entire Virtual Screen ────────────────────────
    /// <summary>
    /// Capture all monitors (entire virtual screen).
    /// </summary>
    public Bitmap CaptureFullScreen()
    {
        int left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        return CaptureRegion(left, top, width, height);
    }

    // ─── Capture the Primary Monitor ──────────────────────────────
    /// <summary>
    /// Capture only the primary monitor.
    /// </summary>
    public Bitmap CapturePrimaryScreen()
    {
        var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        return CaptureRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    // ─── Capture with Freeform Clipping ───────────────────────────
    /// <summary>
    /// Capture a freeform region. Takes a full-screen capture and clips it
    /// to the polygon defined by the point array. Pixels outside the shape
    /// are transparent (PNG-safe).
    /// </summary>
    public Bitmap CaptureFreeform(Point[] freeformPoints, Bitmap? baseCapture = null)
    {
        // 1) Capture the full virtual screen OR use the provided frozen screen
        Bitmap fullCapture = baseCapture ?? CaptureFullScreen();
        try
        {

        // 2) Calculate bounding box of the freeform shape
        int minX = freeformPoints.Min(p => p.X);
        int minY = freeformPoints.Min(p => p.Y);
        int maxX = freeformPoints.Max(p => p.X);
        int maxY = freeformPoints.Max(p => p.Y);
        int width = maxX - minX;
        int height = maxY - minY;

        if (width <= 0 || height <= 0) return fullCapture;

        // 3) Translate points relative to bounding box origin
        var translatedPoints = freeformPoints
            .Select(p => new Point(p.X - minX, p.Y - minY))
            .ToArray();

        // 4) Create result bitmap and clip to freeform path
        var result = new Bitmap(width, height);
        using var g = Graphics.FromImage(result);
        g.Clear(Color.Transparent);

        using var path = new GraphicsPath();
        path.AddPolygon(translatedPoints);
        g.SetClip(path);

        // 5) Draw the relevant portion of the full capture
        int screenLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int screenTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        g.DrawImage(fullCapture,
            new Rectangle(0, 0, width, height),
            new Rectangle(minX - screenLeft, minY - screenTop, width, height),
            GraphicsUnit.Pixel);

        return result;
        }
        finally
        {
            // Dispose the full capture only if we created it locally
            if (baseCapture == null)
            {
                fullCapture.Dispose();
            }
        }
    }
}
