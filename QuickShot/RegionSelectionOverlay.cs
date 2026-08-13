using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using ScreenRecorderLib;

namespace QuickShot;

public class RegionSelectionOverlay : Window, IComponentConnector
{
	private readonly UserSettings _settings;

	private readonly Action<ScreenRect?> _completed;

	private System.Windows.Point? _start;

	internal Canvas Canvas;

	internal Border Selection;

	private bool _contentLoaded;

	public RegionSelectionOverlay(UserSettings settings, Action<ScreenRect?> completed)
	{
		_settings = settings;
		_completed = completed;
		InitializeComponent();
		Localization.Apply(this);
		base.Loaded += OnLoaded;
		base.MouseLeftButtonDown += OnDown;
		base.MouseMove += OnMove;
		base.MouseLeftButtonUp += OnUp;
		base.KeyDown += delegate(object _, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Cancel();
			}
		};
	}

	private void OnLoaded(object? sender, RoutedEventArgs e)
	{
		base.Left = SystemParameters.VirtualScreenLeft;
		base.Top = SystemParameters.VirtualScreenTop;
		base.Width = SystemParameters.VirtualScreenWidth;
		base.Height = SystemParameters.VirtualScreenHeight;
		if (_settings.RememberVideoRegion && _settings.VideoRegionWidth > 1)
		{
			Draw(new Rect((double)_settings.VideoRegionLeft - base.Left, (double)_settings.VideoRegionTop - base.Top, _settings.VideoRegionWidth, _settings.VideoRegionHeight));
		}
		Focus();
	}

	private void OnDown(object sender, MouseButtonEventArgs e)
	{
		_start = e.GetPosition(this);
		CaptureMouse();
		Draw(new Rect(_start.Value, new System.Windows.Size(0.0, 0.0)));
	}

	private void OnMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		System.Windows.Point? start = _start;
		if (start.HasValue && e.LeftButton == MouseButtonState.Pressed)
		{
			System.Windows.Point end = e.GetPosition(this);
			Draw(Normalize(_start.Value, end));
		}
	}

	private void OnUp(object sender, MouseButtonEventArgs e)
	{
		System.Windows.Point? start = _start;
		if (start.HasValue)
		{
			Rect rect = Normalize(_start.Value, e.GetPosition(this));
			ReleaseMouseCapture();
			if (rect.Width < 8.0 || rect.Height < 8.0)
			{
				Cancel();
				return;
			}
			Rectangle primary = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
			ScreenRect selected = new ScreenRect(rect.Left + base.Left - (double)primary.Left, rect.Top + base.Top - (double)primary.Top, rect.Right + base.Left - (double)primary.Left, rect.Bottom + base.Top - (double)primary.Top);
			Close();
			_completed(selected);
		}
	}

	private static Rect Normalize(System.Windows.Point a, System.Windows.Point b)
	{
		return new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
	}

	private void Draw(Rect rect)
	{
		Selection.Visibility = Visibility.Visible;
		Canvas.SetLeft(Selection, rect.Left);
		Canvas.SetTop(Selection, rect.Top);
		Selection.Width = rect.Width;
		Selection.Height = rect.Height;
	}

	private void Cancel()
	{
		if (base.IsMouseCaptured)
		{
			ReleaseMouseCapture();
		}
		Close();
		_completed(null);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/QuickShot;component/regionselectionoverlay.xaml", UriKind.Relative);
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
			Selection = (Border)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
