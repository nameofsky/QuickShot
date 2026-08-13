using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuickShot;

public sealed class OcrOverlay : Window
{
	private readonly Canvas _canvas = new Canvas { Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 10, 18, 28)) };
	private readonly Border _highlight = new Border
	{
		BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 176, 255)),
		BorderThickness = new Thickness(2),
		Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(35, 88, 176, 255)),
		IsHitTestVisible = false
	};
	private readonly Border _selection = new Border
	{
		BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(90, 205, 150)),
		BorderThickness = new Thickness(2),
		Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 90, 205, 150)),
		Visibility = Visibility.Collapsed,
		IsHitTestVisible = false
	};

	private readonly DispatcherTimer _tracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
	private NativeWindow.RECT _currentRect;
	private nint _currentWindow;
	private System.Windows.Point? _dragStart;

	public OcrOverlay()
	{
		WindowStyle = WindowStyle.None;
		AllowsTransparency = true;
		Background = System.Windows.Media.Brushes.Transparent;
		Topmost = true;
		ShowInTaskbar = false;
		Content = _canvas;
		_canvas.Children.Add(_highlight);
		_canvas.Children.Add(_selection);

		Loaded += OnLoaded;
		Closed += delegate { _tracker.Stop(); };
		MouseLeftButtonDown += OnMouseDown;
		MouseMove += OnMouseMove;
		MouseLeftButtonUp += OnMouseUp;
		MouseRightButtonDown += delegate { Cancel(); };
		KeyDown += delegate(object? _, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Cancel();
			}
		};
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		Left = SystemParameters.VirtualScreenLeft;
		Top = SystemParameters.VirtualScreenTop;
		Width = SystemParameters.VirtualScreenWidth;
		Height = SystemParameters.VirtualScreenHeight;
		_tracker.Tick += delegate { TrackWindow(); };
		_tracker.Start();
		Focus();
	}

	private void TrackWindow()
	{
		if (_dragStart.HasValue)
		{
			return;
		}

		System.Drawing.Point position = System.Windows.Forms.Cursor.Position;
		nint handle = NativeWindow.TopWindowAt(position.X, position.Y, new WindowInteropHelper(this).Handle);
		if (handle == IntPtr.Zero || !NativeWindow.GetVisibleWindowRect(handle, out NativeWindow.RECT rect))
		{
			_highlight.Visibility = Visibility.Collapsed;
			return;
		}

		_currentWindow = handle;
		_currentRect = rect;
		_highlight.Visibility = Visibility.Visible;
		Canvas.SetLeft(_highlight, rect.Left - Left);
		Canvas.SetTop(_highlight, rect.Top - Top);
		_highlight.Width = Math.Max(0, rect.Right - rect.Left);
		_highlight.Height = Math.Max(0, rect.Bottom - rect.Top);
	}

	private void OnMouseDown(object sender, MouseButtonEventArgs e)
	{
		_dragStart = e.GetPosition(this);
		CaptureMouse();
		_selection.Visibility = Visibility.Visible;
		_highlight.Visibility = Visibility.Collapsed;
	}

	private void OnMouseMove(object sender, MouseEventArgs e)
	{
		if (!_dragStart.HasValue || e.LeftButton != MouseButtonState.Pressed)
		{
			return;
		}

		System.Windows.Point current = e.GetPosition(this);
		double left = Math.Min(current.X, _dragStart.Value.X);
		double top = Math.Min(current.Y, _dragStart.Value.Y);
		Canvas.SetLeft(_selection, left);
		Canvas.SetTop(_selection, top);
		_selection.Width = Math.Abs(current.X - _dragStart.Value.X);
		_selection.Height = Math.Abs(current.Y - _dragStart.Value.Y);
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
		if (distance < 8 && _currentWindow != IntPtr.Zero)
		{
			Recognize(_currentRect.Left, _currentRect.Top, _currentRect.Right - _currentRect.Left, _currentRect.Bottom - _currentRect.Top);
		}
		else
		{
			Recognize((int)(Left + Canvas.GetLeft(_selection)), (int)(Top + Canvas.GetTop(_selection)), (int)_selection.Width, (int)_selection.Height);
		}
	}

	private void Recognize(int x, int y, int width, int height)
	{
		if (width < 2 || height < 2)
		{
			Close();
			return;
		}

		Hide();
		Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(async () =>
		{
			string feedback;
			bool isError = false;
			try
			{
				using Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
				using (Graphics graphics = Graphics.FromImage(bitmap))
				{
					graphics.CopyFromScreen(x, y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
				}

				OcrRecognitionResult result = await OcrService.RecognizeAsync(bitmap);
				switch (result.Status)
				{
					case OcrStatus.Success:
						if (await TryCopyTextAsync(result.Text))
						{
							feedback = $"已识别并复制到剪贴板（{result.Text.Length} 个字符）";
							string engine = string.IsNullOrWhiteSpace(result.Engine) ? "OCR" : result.Engine;
							feedback = Localization.OcrResultSummary(engine, result.ElapsedMilliseconds, result.Text.Length);
						}
						else
						{
							feedback = "识别成功，但剪贴板正被其他程序占用，未能复制。";
							isError = true;
						}
						break;
					case OcrStatus.NoRecognizer:
						feedback = "离线文字识别无法启动：" + (result.ErrorMessage ?? "缺少 OCR 语言包。");
						isError = true;
						break;
					case OcrStatus.NoText:
						feedback = "未识别到可复制的文字，原剪贴板内容未改变。";
						isError = true;
						break;
					default:
						feedback = "文字识别失败：" + (result.ErrorMessage ?? "未知错误");
						isError = true;
						break;
				}
			}
			catch (Exception ex)
			{
				feedback = "文字识别失败：" + ex.Message;
				isError = true;
			}
			finally
			{
				Close();
			}

			OcrFeedback.Show(feedback, isError);
		}));
	}

	private static async System.Threading.Tasks.Task<bool> TryCopyTextAsync(string text)
	{
		for (int attempt = 0; attempt < 3; attempt++)
		{
			try
			{
				System.Windows.Clipboard.SetText(text);
				return true;
			}
			catch
			{
				if (attempt < 2)
				{
					await System.Threading.Tasks.Task.Delay(120);
				}
			}
		}
		return false;
	}

	private void Cancel()
	{
		_dragStart = null;
		if (IsMouseCaptured)
		{
			ReleaseMouseCapture();
		}
		Close();
	}
}
