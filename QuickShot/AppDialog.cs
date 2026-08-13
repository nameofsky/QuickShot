using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace QuickShot;

public class AppDialog : Window, IComponentConnector
{
	internal TextBlock TitleText;

	internal TextBlock MessageText;

	internal Button ConfirmButton;

	private bool _contentLoaded;

	public AppDialog(string title, string message, string confirmText = "确认", bool destructive = false)
	{
		InitializeComponent();
		Localization.Apply(this);
		TitleText.Text = Localization.Text(title);
		MessageText.Text = Localization.Text(message);
		ConfirmButton.Content = Localization.Text(confirmText);
		if (destructive)
		{
			ConfirmButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C2410C"));
			ConfirmButton.Foreground = Brushes.White;
		}
		base.Loaded += delegate
		{
			ConfirmButton.Focus();
		};
	}

	public static bool Confirm(Window owner, string title, string message, string confirmText = "确认", bool destructive = false)
	{
		return new AppDialog(title, message, confirmText, destructive)
		{
			Owner = owner
		}.ShowDialog() == true;
	}

	private void Confirm(object sender, RoutedEventArgs e)
	{
		base.DialogResult = true;
	}

	private void Cancel(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			base.DialogResult = false;
		}
		base.OnKeyDown(e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/QuickShot;component/appdialog.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocater);
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
			TitleText = (TextBlock)target;
			break;
		case 2:
			MessageText = (TextBlock)target;
			break;
		case 3:
			((Button)target).Click += Cancel;
			break;
		case 4:
			ConfirmButton = (Button)target;
			ConfirmButton.Click += Confirm;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
