using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Threading;

namespace QuickShot;

public class CaptureOverlay : Window, IComponentConnector
{
    private readonly UserSettings _settings;
    private readonly DispatcherTimer _tracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35.0) };
    private readonly DispatcherTimer _gifTimer = new DispatcherTimer();
    private readonly List<Bitmap> _gifFrames = new List<Bitmap>();
    private NativeWindow.RECT _currentRect;
    private nint _currentWindow;
    private System.Windows.Point? _dragStart;
    private bool _gifRecording;
    private int _gifX;
    private int _gifY;
    private int _gifWidth;
    private int _gifHeight;
    private string _gifName = "区域截图";

    internal Canvas Canvas;
    internal Border Highlight;
    internal Border Selection;
    private bool _contentLoaded;

    public CaptureOverlay(UserSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Loaded += delegate
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            _tracker.Tick += delegate { TrackWindow(); };
            _tracker.Start();
            Focus();
        };
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        MouseRightButtonDown += delegate { CancelCapture(); };
        KeyDown += delegate(object _, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelCapture();
            }
        };
        Closed += delegate
        {
            _tracker.Stop();
            _gifTimer.Stop();
            DisposeGifFrames();
        };
    }

    private void TrackWindow()
    {
        if (_dragStart.HasValue || _gifRecording)
        {
            return;
        }

        System.Drawing.Point pos = System.Windows.Forms.Cursor.Position;
        nint hwnd = NativeWindow.TopWindowAt(pos.X, pos.Y, new WindowInteropHelper(this).Handle);
        if (hwnd == IntPtr.Zero || !NativeWindow.GetVisibleWindowRect(hwnd, out NativeWindow.RECT rect))
        {
            Highlight.Visibility = Visibility.Collapsed;
            return;
        }

        _currentWindow = hwnd;
        _currentRect = rect;
        Highlight.Visibility = Visibility.Visible;
        Canvas.SetLeft(Highlight, rect.Left - Left);
        Canvas.SetTop(Highlight, rect.Top - Top);
        Highlight.Width = Math.Max(0, rect.Right - rect.Left);
        Highlight.Height = Math.Max(0, rect.Bottom - rect.Top);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_gifRecording)
        {
            FinishGifRecording();
            return;
        }

        _dragStart = e.GetPosition(this);
        CaptureMouse();
        Selection.Visibility = Visibility.Visible;
        Highlight.Visibility = Visibility.Collapsed;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragStart.HasValue && e.LeftButton == MouseButtonState.Pressed)
        {
            System.Windows.Point current = e.GetPosition(this);
            double x = Math.Min(current.X, _dragStart.Value.X);
            double y = Math.Min(current.Y, _dragStart.Value.Y);
            Canvas.SetLeft(Selection, x);
            Canvas.SetTop(Selection, y);
            Selection.Width = Math.Abs(current.X - _dragStart.Value.X);
            Selection.Height = Math.Abs(current.Y - _dragStart.Value.Y);
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragStart.HasValue)
        {
            return;
        }

        System.Windows.Point end = e.GetPosition(this);
        double distance = Math.Abs(end.X - _dragStart.Value.X) + Math.Abs(end.Y - _dragStart.Value.Y);
        ReleaseMouseCapture();
        _dragStart = null;

        if (distance < 8.0 && _currentWindow != IntPtr.Zero)
        {
            CompleteSelection(_currentRect.Left, _currentRect.Top, _currentRect.Right - _currentRect.Left, _currentRect.Bottom - _currentRect.Top, NativeWindow.Title(_currentWindow));
            return;
        }

        CompleteSelection((int)(Left + Canvas.GetLeft(Selection)), (int)(Top + Canvas.GetTop(Selection)), (int)Selection.Width, (int)Selection.Height, "区域截图");
    }

    private void CompleteSelection(int x, int y, int width, int height, string name)
    {
        if (width < 2 || height < 2)
        {
            Close();
            return;
        }

        if (_settings.ImageFormat == "GIF" && _settings.GifRecordingEnabled)
        {
            StartGifRecording(x, y, width, height, name);
            return;
        }

        Capture(x, y, width, height, name);
    }

    private void StartGifRecording(int x, int y, int width, int height, string name)
    {
        _gifRecording = true;
        _gifX = x;
        _gifY = y;
        _gifWidth = width;
        _gifHeight = height;
        _gifName = name;
        _tracker.Stop();
        _gifTimer.Interval = TimeSpan.FromSeconds(1.0 / Math.Clamp(_settings.GifFramerate, 1, 25));
        _gifTimer.Tick += delegate { CaptureGifFrame(); };
        Opacity = 0.01;
        CaptureGifFrame();
        _gifTimer.Start();
}
    private void CaptureGifFrame()
    {
        try
        {
            Bitmap bitmap = new Bitmap(_gifWidth, _gifHeight, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(_gifX, _gifY, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
            _gifFrames.Add(bitmap);
        }
        catch
        {
        }
    }

    private void FinishGifRecording()
    {
        _gifTimer.Stop();
        _gifRecording = false;
        Hide();
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
        {
            try
            {
                ScreenshotStore.SaveGifCopies(_gifFrames, _settings, _gifName);
            }
            finally
            {
                Close();
            }
        }));
    }

    private void Capture(int x, int y, int width, int height, string name)
    {
        Hide();
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
        {
            using Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
            }
            ScreenshotStore.SaveCopies(bitmap, _settings, name);
            string outputMode = _settings.OutputMode;
            bool copyToClipboard = outputMode == "SaveAndCopy" || outputMode == "CopyOnly" || outputMode.Contains("保存并复制") || outputMode.Contains("仅复制");
            if (copyToClipboard)
            {
                using Bitmap copy = (Bitmap)bitmap.Clone();
                System.Windows.Clipboard.SetImage(BitmapInterop.ToSource(copy));
            }
            Close();
        }));
    }

    private void CancelCapture()
    {
        _dragStart = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
        Close();
    }

    private void DisposeGifFrames()
    {
        foreach (Bitmap frame in _gifFrames)
        {
            frame.Dispose();
        }
        _gifFrames.Clear();
    }

    [DebuggerNonUserCode]
    [GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
    public void InitializeComponent()
    {
        if (!_contentLoaded)
        {
            _contentLoaded = true;
            Uri resourceLocater = new Uri("/QuickShot;component/captureoverlay.xaml", UriKind.Relative);
            System.Windows.Application.LoadComponent(this, resourceLocater);
        }
    }

    [DebuggerNonUserCode]
    [GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    void IComponentConnector.Connect(int connectionId, object target)
    {
        switch (connectionId)
        {
            case 1:
                Canvas = (Canvas)target;
                break;
            case 2:
                Highlight = (Border)target;
                break;
            case 3:
                Selection = (Border)target;
                break;
            default:
                _contentLoaded = true;
                break;
        }
    }
}
