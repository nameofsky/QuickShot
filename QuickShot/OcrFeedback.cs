using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace QuickShot;

public static class OcrFeedback
{
	public static void Show(string message, bool isError = false)
	{
		message = Localization.Text(message);
		Border border = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(29, 38, 50)),
			BorderBrush = new SolidColorBrush(isError ? Color.FromRgb(226, 104, 104) : Color.FromRgb(90, 205, 150)),
			BorderThickness = new Thickness(1),
			Padding = new Thickness(16, 11, 16, 11),
			CornerRadius = new CornerRadius(6),
			Child = new TextBlock
			{
				Text = message,
				Foreground = Brushes.White,
				FontSize = 14,
				TextWrapping = TextWrapping.Wrap,
				MaxWidth = 460
			}
		};

		Window toast = new Window
		{
			AllowsTransparency = true,
			Background = Brushes.Transparent,
			Content = border,
			Focusable = false,
			Height = Double.NaN,
			ShowActivated = false,
			ShowInTaskbar = false,
			SizeToContent = SizeToContent.WidthAndHeight,
			Topmost = true,
			WindowStyle = WindowStyle.None
		};
		toast.Loaded += delegate
		{
			Rect workArea = SystemParameters.WorkArea;
			toast.Left = workArea.Right - toast.ActualWidth - 22;
			toast.Top = workArea.Bottom - toast.ActualHeight - 22;
			DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(isError ? 5 : 3) };
			timer.Tick += delegate
			{
				timer.Stop();
				toast.Close();
			};
			timer.Start();
		};
		toast.Show();
	}
}
