using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace QuickShot;

public class RenameDialog : Window, IComponentConnector
{
	internal TextBox NameBox;

	private bool _contentLoaded;

	public string NewName => NameBox.Text.Trim();

	public RenameDialog(string currentName)
	{
		InitializeComponent();
		Localization.Apply(this);
		NameBox.Text = currentName;
		base.Loaded += delegate
		{
			NameBox.Focus();
			NameBox.SelectAll();
		};
	}

	private void Confirm(object sender, RoutedEventArgs e)
	{
		base.DialogResult = true;
	}

	private void Cancel(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}

	private void NameBoxKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			base.DialogResult = true;
		}
		if (e.Key == Key.Escape)
		{
			base.DialogResult = false;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/QuickShot;component/renamedialog.xaml", UriKind.Relative);
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
			NameBox = (TextBox)target;
			NameBox.KeyDown += NameBoxKeyDown;
			break;
		case 2:
			((Button)target).Click += Confirm;
			break;
		case 3:
			((Button)target).Click += Cancel;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
