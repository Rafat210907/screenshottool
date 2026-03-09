// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — OverlayWindow.xaml.cs (Code-Behind)           ║
// ║  Handles all 4 snip modes: Rectangle, Freeform, Window,        ║
// ║  Fullscreen. Manages mouse input, selection drawing, and        ║
// ║  triggers screen capture when selection is complete.            ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenshotTool.Helpers;
using ScreenshotTool.Services;

// ─── Disambiguate WPF types from WinForms ─────────────────────────
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Cursors = System.Windows.Input.Cursors;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ScreenshotTool.Views;

// ─── Snip Mode Enum ───────────────────────────────────────────────
public enum SnipMode
{
    Rectangle,
    Freeform,
    Window,
    Fullscreen
}

public partial class OverlayWindow : Window
{
    // ─── Dependencies ─────────────────────────────────────────────
    private readonly ScreenCaptureService _captureService;
    private readonly WindowEnumerationService _windowEnumService;

    // ─── State: Current Mode & Selection ──────────────────────────
    private SnipMode _currentMode = SnipMode.Rectangle;
    private bool _isSelecting;
    private Point _startPoint;

    // ─── State: Freeform Points ───────────────────────────────────
    private readonly PointCollection _freeformPoints = new();

    // ─── State: Window Snip ───────────────────────────────────────
    private List<WindowInfo>? _windowList;
    private WindowInfo? _hoveredWindow;

    // ─── Result: The captured region in physical pixels ───────────
    public System.Drawing.Bitmap? CapturedBitmap { get; private set; }
    public bool WasCancelled { get; private set; } = true;

    // ─── Ink Color (from settings) ────────────────────────────────
    private Brush _inkBrush = Brushes.Red;

    // ─── Frozen Background ────────────────────────────────────────
    private System.Drawing.Bitmap? _frozenScreen;

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Constructor                                               ║
    // ╚════════════════════════════════════════════════════════════╝
    public OverlayWindow(ScreenCaptureService captureService,
                         WindowEnumerationService windowEnumService,
                         string inkColorHex,
                         System.Drawing.Bitmap? frozenScreen,
                         SnipMode defaultMode = SnipMode.Rectangle)
    {
        InitializeComponent();

        _captureService = captureService;
        _windowEnumService = windowEnumService;
        _currentMode = defaultMode;
        _frozenScreen = frozenScreen;

        // Set the background to the frozen screen, creating the illusion of a frozen screen
        if (_frozenScreen != null)
        {
            this.Background = new ImageBrush(BitmapHelper.ToBitmapSource(_frozenScreen));
        }

        // Parse ink color from hex string
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(inkColorHex);
            _inkBrush = new SolidColorBrush(color);
        }
        catch { _inkBrush = Brushes.Red; }

        // Apply ink color to selection shapes
        SelectionBorder.Stroke = _inkBrush;
        FreeformLine.Stroke = _inkBrush;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // ─── Size to cover ALL monitors (virtual screen) ──────────
        int vsLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vsTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int vsWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int vsHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        // Convert physical pixels → WPF device-independent units
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        Left = vsLeft * dpiX;
        Top = vsTop * dpiY;
        Width = vsWidth * dpiX;
        Height = vsHeight * dpiY;

        // ─── Set the full-screen geometry for the dark overlay ─────
        FullScreenGeometry.Rect = new Rect(0, 0, Width, Height);

        // ─── Center toolbar on PRIMARY monitor (not virtual screen center) ─
        PositionToolbarOnPrimaryMonitor(vsLeft, dpiX);
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Window Loaded — Ready to use                              ║
    // ╚════════════════════════════════════════════════════════════╝
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // ─── Highlight the active toolbar button ───────────────────
        UpdateToolbarHighlight();

        // ─── Pre-enumerate windows if in Window mode ───────────────
        if (_currentMode == SnipMode.Window)
        {
            _windowList = _windowEnumService.GetVisibleWindows();
        }

        // ─── Fullscreen mode: capture immediately ──────────────────
        if (_currentMode == SnipMode.Fullscreen)
        {
            CaptureFullscreen();
        }
    }

    // ─── Position Toolbar Centered on Primary Monitor ─────────────
    private void PositionToolbarOnPrimaryMonitor(int vsLeft, double dpiScale)
    {
        var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
        if (primaryScreen == null) return;

        double primaryLeftWpf = (primaryScreen.Bounds.Left - vsLeft) * dpiScale;
        double primaryWidthWpf = primaryScreen.Bounds.Width * dpiScale;
        double centerX = primaryLeftWpf + primaryWidthWpf / 2;

        // Use Dispatcher to position after toolbar has been measured
        Dispatcher.BeginInvoke(new Action(() =>
        {
            double marginLeft = centerX - Toolbar.ActualWidth / 2;
            Toolbar.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            Toolbar.Margin = new Thickness(Math.Max(0, marginLeft), 12, 0, 0);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Keyboard: Escape to cancel                                ║
    // ╚════════════════════════════════════════════════════════════╝
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            WasCancelled = true;
            Close();
        }
        base.OnKeyDown(e);
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Toolbar Button Handlers                                   ║
    // ╚════════════════════════════════════════════════════════════╝
    private void BtnRectangle_Click(object sender, RoutedEventArgs e)
    {
        SetMode(SnipMode.Rectangle);
    }

    private void BtnFreeform_Click(object sender, RoutedEventArgs e)
    {
        SetMode(SnipMode.Freeform);
    }

    private void BtnWindow_Click(object sender, RoutedEventArgs e)
    {
        SetMode(SnipMode.Window);
        _windowList = _windowEnumService.GetVisibleWindows();
    }

    private void BtnFullscreen_Click(object sender, RoutedEventArgs e)
    {
        CaptureFullscreen();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        WasCancelled = true;
        Close();
    }

    // ─── Set Active Mode & Update UI ──────────────────────────────
    private void SetMode(SnipMode mode)
    {
        _currentMode = mode;
        ResetSelection();
        UpdateToolbarHighlight();

        // Change cursor based on mode
        Cursor = mode == SnipMode.Window ? Cursors.Hand : Cursors.Cross;
    }

    // ─── Highlight Active Toolbar Button ──────────────────────────
    private void UpdateToolbarHighlight()
    {
        var activeBg = new SolidColorBrush(Color.FromArgb(0xFF, 0x44, 0x88, 0xFF));
        var normalBg = Brushes.Transparent;

        BtnRectangle.Background = _currentMode == SnipMode.Rectangle ? activeBg : normalBg;
        BtnFreeform.Background = _currentMode == SnipMode.Freeform ? activeBg : normalBg;
        BtnWindow.Background = _currentMode == SnipMode.Window ? activeBg : normalBg;
        BtnFullscreen.Background = normalBg; // Never stays active
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Mouse Handlers — Selection Logic                          ║
    // ╚════════════════════════════════════════════════════════════╝

    // ─── Mouse Down: Start selection ──────────────────────────────
    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Ignore clicks on the toolbar
        if (IsClickOnToolbar(e)) return;

        var pos = e.GetPosition(OverlayCanvas);

        switch (_currentMode)
        {
            case SnipMode.Rectangle:
                _isSelecting = true;
                _startPoint = pos;
                SelectionBorder.Visibility = Visibility.Visible;
                OverlayCanvas.CaptureMouse();
                break;

            case SnipMode.Freeform:
                _isSelecting = true;
                _freeformPoints.Clear();
                _freeformPoints.Add(pos);
                FreeformLine.Points = _freeformPoints;
                FreeformLine.Visibility = Visibility.Visible;
                OverlayCanvas.CaptureMouse();
                break;

            case SnipMode.Window:
                if (_hoveredWindow != null)
                {
                    CaptureWindowRegion(_hoveredWindow);
                }
                break;
        }
    }

    // ─── Mouse Move: Update selection ─────────────────────────────
    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(OverlayCanvas);

        switch (_currentMode)
        {
            case SnipMode.Rectangle when _isSelecting:
                UpdateRectangleSelection(pos);
                break;

            case SnipMode.Freeform when _isSelecting:
                _freeformPoints.Add(pos);
                break;

            case SnipMode.Window:
                UpdateWindowHighlight(pos);
                break;
        }
    }

    // ─── Mouse Up: Complete selection ─────────────────────────────
    private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;

        OverlayCanvas.ReleaseMouseCapture();
        _isSelecting = false;

        var pos = e.GetPosition(OverlayCanvas);

        switch (_currentMode)
        {
            case SnipMode.Rectangle:
                CaptureRectangleRegion(pos);
                break;

            case SnipMode.Freeform:
                CaptureFreeformRegion();
                break;
        }
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Rectangle Selection Logic                                 ║
    // ╚════════════════════════════════════════════════════════════╝
    private void UpdateRectangleSelection(Point currentPos)
    {
        double x = Math.Min(_startPoint.X, currentPos.X);
        double y = Math.Min(_startPoint.Y, currentPos.Y);
        double w = Math.Abs(currentPos.X - _startPoint.X);
        double h = Math.Abs(currentPos.Y - _startPoint.Y);

        // Update selection border position & size
        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = w;
        SelectionBorder.Height = h;

        // Update the "hole" in the dark overlay
        SelectionGeometry.Rect = new Rect(x, y, w, h);
    }

    // ─── Capture the Rectangle ────────────────────────────────────
    private void CaptureRectangleRegion(Point endPos)
    {
        double x = Math.Min(_startPoint.X, endPos.X);
        double y = Math.Min(_startPoint.Y, endPos.Y);
        double w = Math.Abs(endPos.X - _startPoint.X);
        double h = Math.Abs(endPos.Y - _startPoint.Y);

        // Minimum size check (ignore tiny accidental clicks)
        if (w < 5 || h < 5) return;

        // Convert WPF units → physical pixels
        var source = PresentationSource.FromVisual(this);
        double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        int vsLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vsTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);

        int px = (int)(x * scaleX) + vsLeft;
        int py = (int)(y * scaleY) + vsTop;
        int pw = (int)(w * scaleX);
        int ph = (int)(h * scaleY);

        // Hide overlay, capture, close
        Hide();
        
        // Use frozen screen if we have it, otherwise fallback to sleep & capture
        if (_frozenScreen == null)
        {
            System.Threading.Thread.Sleep(150); // Wait for overlay to disappear
        }
        
        CapturedBitmap = _captureService.CaptureRegion(px, py, pw, ph, _frozenScreen);
        WasCancelled = false;
        Close();
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Freeform Selection Logic                                  ║
    // ╚════════════════════════════════════════════════════════════╝
    private void CaptureFreeformRegion()
    {
        if (_freeformPoints.Count < 3) return;

        // Convert WPF units → physical pixels
        var source = PresentationSource.FromVisual(this);
        double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        int vsLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vsTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);

        var physicalPoints = _freeformPoints
            .Select(p => new System.Drawing.Point(
                (int)(p.X * scaleX) + vsLeft,
                (int)(p.Y * scaleY) + vsTop))
            .ToArray();

        Hide();
        if (_frozenScreen == null)
        {
            System.Threading.Thread.Sleep(150);
        }
        CapturedBitmap = _captureService.CaptureFreeform(physicalPoints, _frozenScreen);
        WasCancelled = false;
        Close();
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Window Snip Logic                                         ║
    // ╚════════════════════════════════════════════════════════════╝
    private void UpdateWindowHighlight(Point mousePos)
    {
        if (_windowList == null) return;

        // Convert WPF coords → physical pixels
        var source = PresentationSource.FromVisual(this);
        double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        double invScaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double invScaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        int vsLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vsTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);

        int physX = (int)(mousePos.X * scaleX) + vsLeft;
        int physY = (int)(mousePos.Y * scaleY) + vsTop;

        var window = _windowEnumService.FindWindowAtPoint(_windowList, physX, physY);

        if (window != null && window != _hoveredWindow)
        {
            _hoveredWindow = window;
            WindowHighlight.Visibility = Visibility.Visible;

            // Convert physical bounds → WPF units, offset by virtual screen origin
            double wpfX = (window.Bounds.X - vsLeft) * invScaleX;
            double wpfY = (window.Bounds.Y - vsTop) * invScaleY;
            double wpfW = window.Bounds.Width * invScaleX;
            double wpfH = window.Bounds.Height * invScaleY;

            Canvas.SetLeft(WindowHighlight, wpfX);
            Canvas.SetTop(WindowHighlight, wpfY);
            WindowHighlight.Width = wpfW;
            WindowHighlight.Height = wpfH;

            // Also update the clear "hole" in the overlay
            SelectionGeometry.Rect = new Rect(wpfX, wpfY, wpfW, wpfH);
        }
        else if (window == null)
        {
            _hoveredWindow = null;
            WindowHighlight.Visibility = Visibility.Collapsed;
            SelectionGeometry.Rect = new Rect(0, 0, 0, 0);
        }
    }

    // ─── Capture the Highlighted Window ───────────────────────────
    private void CaptureWindowRegion(WindowInfo window)
    {
        Hide();
        if (_frozenScreen == null)
        {
            System.Threading.Thread.Sleep(150);
        }
        CapturedBitmap = _captureService.CaptureRegion(
            window.Bounds.X, window.Bounds.Y,
            window.Bounds.Width, window.Bounds.Height, _frozenScreen);
        WasCancelled = false;
        Close();
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Fullscreen Capture                                        ║
    // ╚════════════════════════════════════════════════════════════╝
    private void CaptureFullscreen()
    {
        Hide();
        if (_frozenScreen == null)
        {
            System.Threading.Thread.Sleep(150);
        }
        CapturedBitmap = _frozenScreen != null ? (System.Drawing.Bitmap)_frozenScreen.Clone() : _captureService.CaptureFullScreen();
        WasCancelled = false;
        Close();
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Helpers                                                   ║
    // ╚════════════════════════════════════════════════════════════╝

    // ─── Check if click is on the toolbar ─────────────────────────
    private bool IsClickOnToolbar(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(Toolbar);
        return pos.X >= 0 && pos.Y >= 0 &&
               pos.X <= Toolbar.ActualWidth &&
               pos.Y <= Toolbar.ActualHeight;
    }

    // ─── Reset all selection visuals ──────────────────────────────
    private void ResetSelection()
    {
        _isSelecting = false;
        _freeformPoints.Clear();
        _hoveredWindow = null;

        SelectionBorder.Visibility = Visibility.Collapsed;
        FreeformLine.Visibility = Visibility.Collapsed;
        WindowHighlight.Visibility = Visibility.Collapsed;
        SelectionGeometry.Rect = new Rect(0, 0, 0, 0);
    }
}
