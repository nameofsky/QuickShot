using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenRecorderLib;

namespace QuickShot;

public class MainWindow : Window, IComponentConnector, IStyleConnector
{
	private sealed class SmoothScrollState
	{
		private readonly ScrollViewer _scrollViewer;

		public DispatcherTimer Timer { get; }

		public double TargetOffset { get; set; }

		public SmoothScrollState(ScrollViewer scrollViewer)
		{
			_scrollViewer = scrollViewer;
			Timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(10.0)
			};
			Timer.Tick += Tick;
		}

		private void Tick(object? sender, EventArgs e)
		{
			double num = TargetOffset - _scrollViewer.VerticalOffset;
			if (Math.Abs(num) < 0.4)
			{
				_scrollViewer.ScrollToVerticalOffset(TargetOffset);
				Timer.Stop();
			}
			else
			{
				_scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + num * 0.42);
			}
		}
	}

	private readonly UserSettings _settings = SettingsStore.Load();

	private readonly NotifyIcon _trayIcon;

	private HwndSource? _source;

	private VideoRecordingService? _recording;

	private VideoRecordingWindow? _recordingWindow;

	private string? _lastOpenedVideo;

	private DateTime _lastOpenedVideoAt;

	private string? _lastOpenedImage;

	private DateTime _lastOpenedImageAt;

	private FrameworkElement? _contextTarget;

	private const int CaptureHotkeyId = 9001;

	private const int VideoHotkeyId = 9002;

	private const int OcrHotkeyId = 9003;

	internal MediaElement BackgroundVideo;

	internal System.Windows.Controls.Button RecentNav;

	internal System.Windows.Controls.Button VideoNav;

	internal System.Windows.Controls.Button SettingsNav;

	private System.Windows.Controls.Button? _languageNav;

	private Grid? _languagePage;

	private System.Windows.Controls.ComboBox? _languageBox;

	private bool _settingLanguageSelection;

	internal Grid CapturePage;

	internal Grid RecentPage;

	internal ItemsControl RecentList;

	internal Grid VideosPage;

	internal ItemsControl VideoList;

	internal Grid SettingsPage;

	internal System.Windows.Controls.ComboBox OutputModeBox;

	internal System.Windows.Controls.ComboBox FormatBox;

	internal System.Windows.Controls.TextBox SaveRootBox;

	internal System.Windows.Controls.CheckBox DateFolderBox;

	internal System.Windows.Controls.ComboBox VideoModeBox;

	internal System.Windows.Controls.CheckBox RememberRegionBox;

	internal System.Windows.Controls.ComboBox VideoResolutionBox;

	internal Slider VideoFramerateSlider;

	internal TextBlock VideoFramerateValueText;

	internal System.Windows.Controls.ComboBox VideoBitrateBox;

	internal System.Windows.Controls.TextBox VideoSaveRootBox;

	internal System.Windows.Controls.CheckBox OpenVideoFolderBox;

	internal System.Windows.Controls.CheckBox DefaultMicrophoneBox;

	internal System.Windows.Controls.ComboBox MicrophoneBox;

	internal TextBlock VideoShortcutHint;

	internal System.Windows.Controls.TextBox VideoShortcutBox;

	internal System.Windows.Controls.ComboBox ThemeBox;

	internal TextBlock ShortcutHint;

	internal System.Windows.Controls.TextBox ShortcutBox;

	private System.Windows.Controls.TextBox? _ocrShortcutBox;

	private TextBlock? _ocrShortcutHint;

	private bool _ocrShortcutSettingAdded;

	private System.Windows.Controls.CheckBox? _gifRecordingBox;

	private System.Windows.Controls.CheckBox? _gifHotkeyStopBox;

	private System.Windows.Controls.TextBox? _gifStopShortcutBox;

	private TextBlock? _gifStopShortcutHint;

	private Slider? _gifFramerateSlider;

	private TextBlock? _gifFramerateValueText;

	private bool _gifSettingsAdded;

	private System.Windows.Controls.Image? _backgroundImage;

	private System.Windows.Controls.Button? _backgroundChooseButton;

	private TextBlock? _backgroundPathText;

	private Slider? _backgroundBlurSlider;

	private TextBlock? _backgroundBlurValueText;

	private bool _backgroundSettingsAdded;

	private bool _contentLoaded;

	private Uri? _backgroundVideoSource;

	private SmoothScrollState? _settingsScrollState;

	private readonly DispatcherTimer _framerateSaveTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(240.0)
	};

	public MainWindow()
	{
		InitializeComponent();
		Localization.SetLanguage(_settings.Language);
		AddLanguagePage();
		_backgroundVideoSource = BackgroundVideo.Source;
		EnsureImageFormats();
		RepairComboBoxHeaders();
		PolishFramerateSlider();
		_framerateSaveTimer.Tick += delegate
		{
			_framerateSaveTimer.Stop();
			SettingsStore.Save(_settings);
		};
		base.Loaded += delegate
		{
			ApplyBackgroundSettings();
		};
		_trayIcon = CreateTrayIcon();
		Localization.LanguageChanged += delegate
		{
			ApplyLocalizedUi();
		};
		ApplyLocalizedUi();
		ScreenshotStore.Saved += OnScreenshotSaved;
		LoadSettings();
		base.Loaded += delegate
		{
			AddOcrShortcutSetting();
			AddGifRecordingSettings();
			AddBackgroundSettings();
			ApplyBackgroundSettings();
			ApplyLocalizedUi();
			RegisterHotkeys();
			EnableSmoothScrolling();
			PolishScrollBars();
		};
		base.SourceInitialized += delegate
		{
			_source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
			_source?.AddHook(WndProc);
		};
	}

	private NotifyIcon CreateTrayIcon()
	{
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add("打开 QuickShot", null, delegate
		{
			ShowFromTray();
		});
		contextMenuStrip.Items.Add("开始截图", null, delegate
		{
			BeginCapture();
		});
		contextMenuStrip.Items.Add("文字识别", null, delegate
		{
			BeginOcr();
		});
		contextMenuStrip.Items.Add("开始录制", null, delegate
		{
			BeginVideoRecording();
		});
		contextMenuStrip.Items.Add("退出", null, delegate
		{
			UnregisterHotkeys();
			_trayIcon.Dispose();
			System.Windows.Application.Current.Shutdown();
		});
		string text = Path.Combine(AppContext.BaseDirectory, "app-icon.ico");
		NotifyIcon notifyIcon = new NotifyIcon();
		notifyIcon.Icon = (File.Exists(text) ? new Icon(text) : SystemIcons.Application);
		notifyIcon.Text = "QuickShot";
		notifyIcon.Visible = true;
		notifyIcon.ContextMenuStrip = contextMenuStrip;
		notifyIcon.MouseClick += delegate(object? _, System.Windows.Forms.MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				base.Dispatcher.BeginInvoke(new Action(ShowFromTray));
			}
		};
		notifyIcon.DoubleClick += delegate
		{
			ShowFromTray();
		};
		return notifyIcon;
	}

	private void AddLanguagePage()
	{
		if (_languagePage == null && SettingsNav.Parent is System.Windows.Controls.Panel panel && SettingsPage.Parent is System.Windows.Controls.Panel panel2)
		{
			_languageNav = new System.Windows.Controls.Button
			{
				Content = Localization.Text("语言"),
				Style = SettingsNav.Style,
				Margin = SettingsNav.Margin,
				Padding = SettingsNav.Padding,
				HorizontalContentAlignment = SettingsNav.HorizontalContentAlignment,
				VerticalContentAlignment = SettingsNav.VerticalContentAlignment,
				Height = SettingsNav.Height,
				MinHeight = SettingsNav.MinHeight
			};
			_languageNav.Click += ShowLanguage;
			int num = panel.Children.IndexOf(SettingsNav);
			panel.Children.Insert(num + 1, _languageNav);
			TextBlock textBlock = new TextBlock
			{
				Text = "语言",
				FontSize = 26.0,
				FontWeight = FontWeights.SemiBold
			};
			textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
			TextBlock textBlock2 = new TextBlock
			{
				Text = "选择 QuickShot 的显示语言",
				Margin = new Thickness(0.0, 8.0, 0.0, 24.0)
			};
			textBlock2.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
			_languageBox = new System.Windows.Controls.ComboBox
			{
				Style = ThemeBox.Style,
				Width = ((!double.IsNaN(ThemeBox.Width)) ? ThemeBox.Width : 240.0),
				HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
				Tag = "QuickShot.DoNotLocalize"
			};
			_languageBox.Items.Add("简体中文");
			_languageBox.Items.Add("English");
			_languageBox.Items.Clear();
			_languageBox.Items.Add(new ComboBoxItem
			{
				Content = "简体中文",
				Tag = "zh-CN"
			});
			_languageBox.Items.Add(new ComboBoxItem
			{
				Content = "English",
				Tag = "en-US"
			});
			SetLanguageSelection(_settings.Language);
			_languageBox.SelectionChanged += LanguageChanged;
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(textBlock);
			stackPanel.Children.Add(textBlock2);
			stackPanel.Children.Add(_languageBox);
			Border border = new Border
			{
				Padding = new Thickness(28.0),
				CornerRadius = new CornerRadius(6.0),
				Child = stackPanel,
				MaxWidth = 620.0,
				BorderThickness = new Thickness(1.0)
			};
			border.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
			border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
			StackPanel stackPanel2 = new StackPanel
			{
				Margin = new Thickness(44.0, 38.0, 44.0, 44.0)
			};
			stackPanel2.Children.Add(border);
			_languagePage = new Grid
			{
				Visibility = Visibility.Collapsed
			};
			_languagePage.Children.Add(stackPanel2);
			panel2.Children.Add(_languagePage);
		}
	}

	private void SetLanguageSelection(string? language)
	{
		if (_languageBox == null)
		{
			return;
		}
		_settingLanguageSelection = true;
		try
		{
			string selectedLanguage = (string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN");
			ComboBoxItem comboBoxItem = _languageBox.Items.OfType<ComboBoxItem>().FirstOrDefault((ComboBoxItem item) => string.Equals(item.Tag as string, selectedLanguage, StringComparison.OrdinalIgnoreCase));
			if (comboBoxItem != null)
			{
				_languageBox.SelectedItem = comboBoxItem;
			}
		}
		finally
		{
			_settingLanguageSelection = false;
		}
	}

	private void ShowLanguage(object sender, RoutedEventArgs e)
	{
		if (_languagePage != null)
		{
			ShowPage(_languagePage);
		}
	}

	private void LanguageChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (!_settingLanguageSelection && base.IsLoaded && _languageBox?.SelectedItem is ComboBoxItem comboBoxItem)
		{
			_settings.Language = (string.Equals(comboBoxItem.Tag as string, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN");
			Localization.SetLanguage(_settings.Language);
			ApplyLocalizedUi();
			SettingsStore.Save(_settings);
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				SetLanguageSelection(_settings.Language);
			}, DispatcherPriority.Render);
		}
	}

	private void ApplyLocalizedUi()
	{
		Localization.Apply(this);
		if (_trayIcon.ContextMenuStrip != null)
		{
			string[] array = new string[5] { "打开 QuickShot", "开始截图", "文字识别", "开始录制", "退出" };
			for (int i = 0; i < array.Length && i < _trayIcon.ContextMenuStrip.Items.Count; i++)
			{
				_trayIcon.ContextMenuStrip.Items[i].Text = Localization.Text(array[i]);
			}
		}
	}

	private void LoadSettings()
	{
		string value = ((_settings.OutputMode == "CopyOnly" || _settings.OutputMode.Contains("仅复制")) ? "CopyOnly" : ((_settings.OutputMode == "SaveOnly" || _settings.OutputMode.Contains("仅保存")) ? "SaveOnly" : "SaveAndCopy"));
		SelectByTag(OutputModeBox, value);
		Select(FormatBox, _settings.ImageFormat);
		SaveRootBox.Text = _settings.SaveRoot;
		DateFolderBox.IsChecked = _settings.UseDateFolderForExport;
		ShortcutBox.Text = _settings.Shortcut;
		Select(VideoModeBox, _settings.VideoCaptureMode);
		RememberRegionBox.IsChecked = _settings.RememberVideoRegion;
		VideoFramerateSlider.Value = Math.Clamp(_settings.VideoFramerate, 30, 144);
		VideoFramerateValueText.Text = $"{(int)VideoFramerateSlider.Value} fps";
		SelectByTag(VideoBitrateBox, _settings.VideoBitrateKbps.ToString());
		Select(VideoResolutionBox, (_settings.VideoWidth == 1920) ? "1920 x 1080" : ((_settings.VideoWidth == 1280) ? "1280 x 720" : "原始分辨率"));
		VideoSaveRootBox.Text = _settings.VideoSaveRoot;
		OpenVideoFolderBox.IsChecked = _settings.OpenVideoFolderAfterRecording;
		DefaultMicrophoneBox.IsChecked = _settings.MicrophoneEnabledByDefault;
		VideoShortcutBox.Text = _settings.VideoShortcut;
		SelectByTag(ThemeBox, (_settings.Theme == "Light") ? "Light" : ((_settings.Theme == "System") ? "System" : "Dark"));
		LoadMicrophones();
		ApplyTheme(_settings.Theme);
		ShowRecent(null, null);
	}

	private void AddBackgroundSettings()
	{
		if (_backgroundSettingsAdded || ThemeBox == null)
		{
			return;
		}

		FrameworkElement? anchor = FindSettingRow(ThemeBox);
		if (anchor?.Parent is not System.Windows.Controls.Panel panel)
		{
			return;
		}

		_backgroundChooseButton = new System.Windows.Controls.Button
		{
			Content = Localization.Text("选择文件"),
			Style = SettingsNav.Style,
			Width = 112,
			Height = 38,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right
		};
		_backgroundChooseButton.Click += ChooseBackground;
		_backgroundPathText = new TextBlock
		{
			Text = BackgroundDisplayName(),
			Width = 240,
			Margin = new Thickness(0, 7, 0, 0),
			TextTrimming = TextTrimming.CharacterEllipsis,
			TextAlignment = TextAlignment.Right
		};
		_backgroundPathText.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
		StackPanel backgroundRight = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
		backgroundRight.Children.Add(_backgroundChooseButton);
		backgroundRight.Children.Add(_backgroundPathText);
		TextBlock backgroundTitle = new TextBlock { Text = "自定义背景", FontWeight = FontWeights.SemiBold };
		backgroundTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
		TextBlock backgroundDescription = new TextBlock { Text = "选择图片或视频，文件将复制到背景目录", Margin = new Thickness(0, 4, 0, 0) };
		backgroundDescription.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
		StackPanel backgroundText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		backgroundText.Children.Add(backgroundTitle);
		backgroundText.Children.Add(backgroundDescription);
		Grid backgroundRow = CreateSettingRow(anchor, backgroundText, backgroundRight);

		_backgroundBlurSlider = new Slider
		{
			Minimum = 0,
			Maximum = 100,
			Value = Math.Clamp(_settings.BackgroundBlurPercent, 0, 100),
			Width = 240,
			Style = VideoFramerateSlider.Style,
			IsSnapToTickEnabled = true,
			TickFrequency = 1,
			SmallChange = 1,
			LargeChange = 5
		};
		_backgroundBlurSlider.ValueChanged += BackgroundBlurChanged;
		_backgroundBlurValueText = new TextBlock
		{
			Text = $"{(int)_backgroundBlurSlider.Value}%",
			Width = 54,
			Margin = new Thickness(12, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		_backgroundBlurValueText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
		StackPanel blurRight = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
		blurRight.Children.Add(_backgroundBlurSlider);
		blurRight.Children.Add(_backgroundBlurValueText);
		TextBlock blurTitle = new TextBlock { Text = "界面磨砂程度", FontWeight = FontWeights.SemiBold };
		blurTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
		TextBlock blurDescription = new TextBlock { Text = "范围 0-100%，默认 25%", Margin = new Thickness(0, 4, 0, 0) };
		blurDescription.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
		StackPanel blurText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		blurText.Children.Add(blurTitle);
		blurText.Children.Add(blurDescription);
		Grid blurRow = CreateSettingRow(anchor, blurText, blurRight);

		int index = panel.Children.IndexOf(anchor);
		panel.Children.Insert(index + 1, backgroundRow);
		panel.Children.Insert(index + 2, blurRow);
		_backgroundSettingsAdded = true;
	}

	private void ChooseBackground(object? sender, RoutedEventArgs e)
	{
		using OpenFileDialog dialog = new OpenFileDialog
		{
			Title = Localization.Text("选择自定义背景"),
			Filter = Localization.Text("图片和视频|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.mp4;*.wmv;*.avi;*.mov|图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|视频|*.mp4;*.wmv;*.avi;*.mov"),
			CheckFileExists = true,
			Multiselect = false
		};
		if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
		{
			return;
		}

		try
		{
			string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
			if (!IsBackgroundFile(extension))
			{
				throw new InvalidOperationException("请选择支持的图片或视频文件。");
			}
			string directory = BackgroundDirectory();
			Directory.CreateDirectory(directory);
			string destination = Path.Combine(directory, $"背景_{DateTime.Now:yyyyMMddHHmmssfff}{extension}");
			File.Copy(dialog.FileName, destination, overwrite: false);
			_settings.BackgroundPath = destination;
			SettingsStore.Save(_settings);
			ApplyBackgroundSettings();
			if (_backgroundPathText != null)
			{
				_backgroundPathText.Text = BackgroundDisplayName();
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(Localization.Text("无法设置自定义背景：" + ex.Message), "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void BackgroundBlurChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!base.IsLoaded || _backgroundBlurSlider == null)
		{
			return;
		}
		int value = (int)Math.Round(_backgroundBlurSlider.Value);
		_settings.BackgroundBlurPercent = value;
		if (_backgroundBlurValueText != null)
		{
			_backgroundBlurValueText.Text = $"{value}%";
		}
		ApplyBackgroundBlur();
		_framerateSaveTimer.Stop();
		_framerateSaveTimer.Start();
	}

	private static bool IsBackgroundFile(string extension)
	{
		return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".mp4" or ".wmv" or ".avi" or ".mov";
	}

	private static bool IsBackgroundVideo(string extension)
	{
		return extension is ".mp4" or ".wmv" or ".avi" or ".mov";
	}

	private static string BackgroundDirectory()
	{
		return Path.Combine(AppContext.BaseDirectory, "背景");
	}

	private string BackgroundDisplayName()
	{
		return Localization.Text(string.IsNullOrWhiteSpace(_settings.BackgroundPath) ? "当前：未选择" : "当前：" + Path.GetFileName(_settings.BackgroundPath));
	}

	private void ApplyBackgroundSettings()
	{
		EnsureBackgroundImageLayer();
		ApplyTheme(_settings.Theme);
		ApplyBackgroundBlur();
		string path = _settings.BackgroundPath;
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			BackgroundVideo.Stop();
			BackgroundVideo.Close();
			BackgroundVideo.Source = null;
			BackgroundVideo.Visibility = Visibility.Collapsed;
			if (_backgroundImage != null)
			{
				_backgroundImage.Source = null;
				_backgroundImage.Visibility = Visibility.Collapsed;
			}
			return;
		}

		if (IsBackgroundVideo(Path.GetExtension(path).ToLowerInvariant()))
		{
			if (_backgroundImage != null)
			{
				_backgroundImage.Source = null;
				_backgroundImage.Visibility = Visibility.Collapsed;
			}
			BackgroundVideo.Visibility = Visibility.Visible;
			Uri source = new Uri(path, UriKind.Absolute);
			if (BackgroundVideo.Source == null || !Uri.Compare(BackgroundVideo.Source, source, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase).Equals(0))
			{
				BackgroundVideo.Stop();
				BackgroundVideo.Close();
				BackgroundVideo.Source = source;
			}
			_backgroundVideoSource = source;
			BackgroundVideo.Play();
		}
		else
		{
			BackgroundVideo.Stop();
			BackgroundVideo.Close();
			BackgroundVideo.Source = null;
			BackgroundVideo.Visibility = Visibility.Collapsed;
			if (_backgroundImage != null)
			{
				BitmapImage image = new BitmapImage();
				image.BeginInit();
				image.CacheOption = BitmapCacheOption.OnLoad;
				image.UriSource = new Uri(path, UriKind.Absolute);
				image.EndInit();
				image.Freeze();
				_backgroundImage.Source = image;
				_backgroundImage.Visibility = Visibility.Visible;
			}
		}
	}

	private void EnsureBackgroundImageLayer()
	{
		if (_backgroundImage != null)
		{
			return;
		}
		if (BackgroundVideo.Parent is not Grid root)
		{
			return;
		}

		int rowCount = Math.Max(1, root.RowDefinitions.Count);
		int columnCount = Math.Max(1, root.ColumnDefinitions.Count);
		root.Children.Remove(BackgroundVideo);
		Grid.SetRow(BackgroundVideo, 0);
		Grid.SetColumn(BackgroundVideo, 0);
		Grid.SetRowSpan(BackgroundVideo, rowCount);
		Grid.SetColumnSpan(BackgroundVideo, columnCount);
		root.Children.Insert(0, BackgroundVideo);

		_backgroundImage = new System.Windows.Controls.Image
		{
			Stretch = Stretch.UniformToFill,
			IsHitTestVisible = false,
			Visibility = Visibility.Collapsed
		};
		Grid.SetRow(_backgroundImage, 0);
		Grid.SetColumn(_backgroundImage, 0);
		Grid.SetRowSpan(_backgroundImage, rowCount);
		Grid.SetColumnSpan(_backgroundImage, columnCount);
		root.Children.Insert(0, _backgroundImage);
	}

	private void ApplyBackgroundBlur()
	{
		int percent = Math.Clamp(_settings.BackgroundBlurPercent, 0, 100);
		BlurEffect? effect = percent == 0 ? null : new BlurEffect { Radius = percent * 0.3, RenderingBias = RenderingBias.Performance };
		BackgroundVideo.Effect = effect;
		if (_backgroundImage != null)
		{
			_backgroundImage.Effect = percent == 0 ? null : new BlurEffect { Radius = percent * 0.3, RenderingBias = RenderingBias.Performance };
		}
	}
	private void AddGifRecordingSettings()
	{
		if (_gifSettingsAdded || DateFolderBox == null)
		{
			return;
		}

		FrameworkElement? anchor = FindSettingRow(DateFolderBox);
		if (anchor?.Parent is not System.Windows.Controls.Panel panel)
		{
			return;
		}

		_gifRecordingBox = new System.Windows.Controls.CheckBox
		{
			Content = "GIF 动图录制",
			IsChecked = _settings.GifRecordingEnabled,
			Style = DateFolderBox.Style,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};
		_gifRecordingBox.Click += GifRecordingSettingChanged;

		TextBlock toggleTitle = new TextBlock { Text = "GIF 是否录制动图", FontWeight = FontWeights.SemiBold };
		toggleTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
		TextBlock toggleDescription = new TextBlock { Text = "框选后开始录制，再次点击屏幕结束", Margin = new Thickness(0, 4, 0, 0) };
		toggleDescription.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
		StackPanel toggleText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		toggleText.Children.Add(toggleTitle);
		toggleText.Children.Add(toggleDescription);
		Grid toggleRow = CreateSettingRow(anchor, toggleText, _gifRecordingBox);

		_gifFramerateSlider = new Slider
		{
			Minimum = 1,
			Maximum = 25,
			Value = Math.Clamp(_settings.GifFramerate, 1, 25),
			Width = 240,
			Style = VideoFramerateSlider.Style,
			IsSnapToTickEnabled = true,
			TickFrequency = 1,
			SmallChange = 1,
			LargeChange = 5,
			IsEnabled = _settings.GifRecordingEnabled
		};
		_gifFramerateSlider.ValueChanged += GifFramerateChanged;
		_gifFramerateValueText = new TextBlock
		{
			Text = $"{(int)_gifFramerateSlider.Value} fps",
			Width = 54,
			Margin = new Thickness(12, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		_gifFramerateValueText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
		StackPanel sliderPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
		sliderPanel.Children.Add(_gifFramerateSlider);
		sliderPanel.Children.Add(_gifFramerateValueText);
		TextBlock fpsTitle = new TextBlock { Text = "GIF 动图每秒帧数", FontWeight = FontWeights.SemiBold };
		fpsTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
		TextBlock fpsDescription = new TextBlock { Text = "范围 1-25 fps", Margin = new Thickness(0, 4, 0, 0) };
		fpsDescription.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
		StackPanel fpsText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		fpsText.Children.Add(fpsTitle);
		fpsText.Children.Add(fpsDescription);
		Grid fpsRow = CreateSettingRow(anchor, fpsText, sliderPanel);

		_gifHotkeyStopBox = new System.Windows.Controls.CheckBox
		{
			Content = "热键停止",
			IsChecked = _settings.GifHotkeyStopEnabled,
			Style = DateFolderBox.Style,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			IsEnabled = _settings.GifRecordingEnabled
		};
		_gifHotkeyStopBox.Click += GifHotkeyStopSettingChanged;

		TextBlock hotkeyTitle = new TextBlock { Text = "GIF 热键停止录制", FontWeight = FontWeights.SemiBold };
		hotkeyTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
		TextBlock hotkeyDescription = new TextBlock { Text = "开启后录制中点击不再结束，用下方热键停止录制", Margin = new Thickness(0, 4, 0, 0) };
		hotkeyDescription.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
		StackPanel hotkeyText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		hotkeyText.Children.Add(hotkeyTitle);
		hotkeyText.Children.Add(hotkeyDescription);
		Grid hotkeyRow = CreateSettingRow(anchor, hotkeyText, _gifHotkeyStopBox);

		_gifStopShortcutBox = new System.Windows.Controls.TextBox
		{
			Text = _settings.GifStopShortcut,
			Style = ShortcutBox.Style,
			Width = ((!double.IsNaN(ShortcutBox.Width)) ? ShortcutBox.Width : 240.0),
			Height = ((!double.IsNaN(ShortcutBox.Height)) ? ShortcutBox.Height : 54.0),
			Margin = ShortcutBox.Margin,
			VerticalAlignment = ShortcutBox.VerticalAlignment,
			VerticalContentAlignment = ShortcutBox.VerticalContentAlignment,
			ToolTip = "按下 Ctrl、Shift 或 Alt 加任意按键以设置；按 Delete 恢复默认（Ctrl + Shift + G）",
			IsEnabled = _settings.GifRecordingEnabled && _settings.GifHotkeyStopEnabled
		};
		_gifStopShortcutBox.PreviewKeyDown += GifStopShortcutKeyDown;
		_gifStopShortcutHint = new TextBlock
		{
			Text = "按下组合键以设置 GIF 停止热键",
			Style = ShortcutHint.Style,
			Margin = ShortcutHint.Margin
		};
		_gifStopShortcutHint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
		StackPanel stopShortcutText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
		TextBlock stopShortcutTitle = new TextBlock { Text = "GIF 停止热键", FontWeight = FontWeights.SemiBold };
		stopShortcutTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
		stopShortcutText.Children.Add(stopShortcutTitle);
		stopShortcutText.Children.Add(_gifStopShortcutHint);
		Grid stopShortcutRow = CreateSettingRow(anchor, stopShortcutText, _gifStopShortcutBox);

		int index = panel.Children.IndexOf(anchor);
		panel.Children.Insert(index + 1, toggleRow);
		panel.Children.Insert(index + 2, hotkeyRow);
		panel.Children.Insert(index + 3, stopShortcutRow);
		panel.Children.Insert(index + 4, fpsRow);
		_gifSettingsAdded = true;
	}

	private static FrameworkElement? FindSettingRow(FrameworkElement control)
	{
		for (DependencyObject parent = VisualTreeHelper.GetParent(control); parent != null; parent = VisualTreeHelper.GetParent(parent))
		{
			if (parent is Grid grid && grid.Parent is StackPanel)
			{
				return grid;
			}
		}
		return null;
	}

	private static Grid CreateSettingRow(FrameworkElement anchor, FrameworkElement left, FrameworkElement right)
	{
		Grid row = new Grid { Margin = anchor.Margin, MinHeight = anchor.MinHeight };
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		Grid.SetColumn(right, 1);
		row.Children.Add(left);
		row.Children.Add(right);
		return row;
	}

	private void GifRecordingSettingChanged(object? sender, RoutedEventArgs e)
	{
		if (!base.IsLoaded || _gifRecordingBox == null)
		{
			return;
		}
		_settings.GifRecordingEnabled = _gifRecordingBox.IsChecked == true;
		if (_gifFramerateSlider != null)
		{
			_gifFramerateSlider.IsEnabled = _settings.GifRecordingEnabled;
		}
		if (_gifHotkeyStopBox != null)
		{
			_gifHotkeyStopBox.IsEnabled = _settings.GifRecordingEnabled;
		}
		if (_gifStopShortcutBox != null)
		{
			_gifStopShortcutBox.IsEnabled = _settings.GifRecordingEnabled && _settings.GifHotkeyStopEnabled;
		}
		SettingsStore.Save(_settings);
	}

	private void GifHotkeyStopSettingChanged(object? sender, RoutedEventArgs e)
	{
		if (!base.IsLoaded || _gifHotkeyStopBox == null)
		{
			return;
		}
		_settings.GifHotkeyStopEnabled = _gifHotkeyStopBox.IsChecked == true;
		if (_gifStopShortcutBox != null)
		{
			_gifStopShortcutBox.IsEnabled = _settings.GifRecordingEnabled && _settings.GifHotkeyStopEnabled;
		}
		SettingsStore.Save(_settings);
	}

	private void GifStopShortcutKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (_gifStopShortcutBox == null)
		{
			return;
		}
		Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
		switch (key)
		{
		case Key.Back:
		case Key.Delete:
			_settings.GifStopShortcut = "Ctrl + Shift + G";
			_gifStopShortcutBox.Text = _settings.GifStopShortcut;
			_gifStopShortcutHint?.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
			if (_gifStopShortcutHint != null)
			{
				_gifStopShortcutHint.Text = Localization.Text("已恢复默认热键");
			}
			SettingsStore.Save(_settings);
			e.Handled = true;
			return;
		case Key.LeftShift:
		case Key.RightShift:
		case Key.LeftCtrl:
		case Key.RightCtrl:
		case Key.LeftAlt:
		case Key.RightAlt:
			e.Handled = true;
			return;
		}
		List<string> list = new List<string>();
		if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None)
		{
			list.Add("Ctrl");
		}
		if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.None)
		{
			list.Add("Shift");
		}
		if ((Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.None)
		{
			list.Add("Alt");
		}
		if (list.Count == 0)
		{
			_gifStopShortcutHint?.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
			if (_gifStopShortcutHint != null)
			{
				_gifStopShortcutHint.Text = Localization.Text("快捷键至少需要 Ctrl、Shift 或 Alt（Tab 不能作为修饰键）");
			}
		}
		else
		{
			list.Add(key.ToString());
			string combo = string.Join(" + ", list);
			_settings.GifStopShortcut = combo;
			_gifStopShortcutBox.Text = combo;
			if (_gifStopShortcutHint != null)
			{
				_gifStopShortcutHint.Text = Localization.Text($"已设置为 {combo}");
			}
			SettingsStore.Save(_settings);
		}
		e.Handled = true;
	}

	private void GifFramerateChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!base.IsLoaded || _gifFramerateSlider == null)
		{
			return;
		}
		int value = (int)Math.Round(_gifFramerateSlider.Value);
		if (_gifFramerateValueText != null)
		{
			_gifFramerateValueText.Text = $"{value} fps";
		}
		_settings.GifFramerate = value;
		_framerateSaveTimer.Stop();
		_framerateSaveTimer.Start();
	}
	private void AddOcrShortcutSetting()
	{
		if (!_ocrShortcutSettingAdded)
		{
			FrameworkElement frameworkElement = FindShortcutSettingRow();
			if (frameworkElement?.Parent is System.Windows.Controls.Panel panel)
			{
				TextBlock textBlock = new TextBlock
				{
					Text = "文字识别快捷键",
					FontWeight = FontWeights.SemiBold
				};
				textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
				_ocrShortcutBox = new System.Windows.Controls.TextBox
				{
					Text = _settings.OcrShortcut,
					Style = ShortcutBox.Style,
					Width = ((!double.IsNaN(ShortcutBox.Width)) ? ShortcutBox.Width : 240.0),
					Height = ((!double.IsNaN(ShortcutBox.Height)) ? ShortcutBox.Height : 54.0),
					Margin = ShortcutBox.Margin,
					VerticalAlignment = ShortcutBox.VerticalAlignment,
					VerticalContentAlignment = ShortcutBox.VerticalContentAlignment,
					ToolTip = "按下 Ctrl、Shift 或 Alt 加任意按键以设置；按 Delete 清空"
				};
				_ocrShortcutBox.PreviewKeyDown += OcrShortcutKeyDown;
				_ocrShortcutHint = new TextBlock
				{
					Text = "按下组合键以设置文字识别快捷键",
					Style = ShortcutHint.Style,
					Margin = ShortcutHint.Margin
				};
				StackPanel stackPanel = new StackPanel
				{
					VerticalAlignment = VerticalAlignment.Center
				};
				stackPanel.Children.Add(textBlock);
				stackPanel.Children.Add(_ocrShortcutHint);
				Grid grid = new Grid
				{
					Margin = frameworkElement.Margin,
					MinHeight = frameworkElement.MinHeight
				};
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1.0, GridUnitType.Star)
				});
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = GridLength.Auto
				});
				Grid.SetColumn(_ocrShortcutBox, 1);
				grid.Children.Add(stackPanel);
				grid.Children.Add(_ocrShortcutBox);
				int num = panel.Children.IndexOf(frameworkElement);
				panel.Children.Insert(num + 1, grid);
				_ocrShortcutSettingAdded = true;
			}
		}
	}

	private FrameworkElement? FindShortcutSettingRow()
	{
		for (DependencyObject parent = VisualTreeHelper.GetParent(ShortcutBox); parent != null; parent = VisualTreeHelper.GetParent(parent))
		{
			if (parent is Grid grid && grid.Parent is StackPanel)
			{
				return grid;
			}
		}
		return null;
	}

	private void EnsureImageFormats()
	{
		string[] array = new string[3] { "BMP", "TIFF", "GIF" };
		foreach (string format in array)
		{
			if (!FormatBox.Items.OfType<ComboBoxItem>().Any((ComboBoxItem item) => item.Content?.ToString() == format))
			{
				FormatBox.Items.Add(new ComboBoxItem
				{
					Content = format
				});
			}
		}
	}

	private void RepairComboBoxHeaders()
	{
		ControlTemplate template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"{x:Type ComboBox}\">\n  <Grid SnapsToDevicePixels=\"True\">\n    <ToggleButton x:Name=\"DropButton\" Focusable=\"False\" IsChecked=\"{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}\">\n      <ToggleButton.Template><ControlTemplate TargetType=\"{x:Type ToggleButton}\">\n        <Border x:Name=\"Chrome\" Background=\"{DynamicResource SurfaceBrush}\" BorderBrush=\"{DynamicResource BorderBrush}\" BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"12,0\">\n          <Grid>\n            <ContentPresenter x:Name=\"SelectedContent\" Tag=\"QuickShot.DoNotLocalize\" Margin=\"0,0,28,0\" VerticalAlignment=\"Center\"\n                              Content=\"{Binding SelectedItem.Content, RelativeSource={RelativeSource AncestorType={x:Type ComboBox}}}\" TextElement.Foreground=\"{DynamicResource TextBrush}\" />\n            <Path HorizontalAlignment=\"Right\" VerticalAlignment=\"Center\" Stroke=\"{DynamicResource MutedBrush}\" StrokeThickness=\"1.5\" StrokeStartLineCap=\"Round\" StrokeEndLineCap=\"Round\" Data=\"M 0 0 L 4 4 L 8 0\"/>\n          </Grid>\n        </Border>\n        <ControlTemplate.Triggers>\n          <Trigger Property=\"IsChecked\" Value=\"True\"><Setter TargetName=\"Chrome\" Property=\"BorderBrush\" Value=\"{DynamicResource AccentBrush}\"/></Trigger>\n          <Trigger Property=\"IsEnabled\" Value=\"False\"><Setter TargetName=\"Chrome\" Property=\"Opacity\" Value=\"0.55\"/></Trigger>\n        </ControlTemplate.Triggers>\n      </ControlTemplate></ToggleButton.Template>\n    </ToggleButton>\n    <Popup x:Name=\"Popup\" Placement=\"Bottom\" AllowsTransparency=\"True\" Focusable=\"False\" IsOpen=\"{TemplateBinding IsDropDownOpen}\" PopupAnimation=\"Slide\">\n      <Border Margin=\"0,4,0,0\" MinWidth=\"{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}\" Background=\"{DynamicResource SurfaceBrush}\" BorderBrush=\"{DynamicResource BorderBrush}\" BorderThickness=\"1\" CornerRadius=\"8\">\n        <ScrollViewer CanContentScroll=\"True\" MaxHeight=\"300\" Padding=\"4\"><ItemsPresenter/></ScrollViewer>\n      </Border>\n    </Popup>\n  </Grid>\n</ControlTemplate>");
		Style itemStyle = (Style)XamlReader.Parse("<Style xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"{x:Type ComboBoxItem}\">\n  <Setter Property=\"Foreground\" Value=\"{DynamicResource ComboBoxItemBrush}\"/>\n  <Setter Property=\"Padding\" Value=\"10,8\"/>\n  <Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\"/>\n  <Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"{x:Type ComboBoxItem}\">\n    <Border x:Name=\"ItemChrome\" Background=\"Transparent\" CornerRadius=\"6\" Padding=\"{TemplateBinding Padding}\">\n      <ContentPresenter Content=\"{TemplateBinding Content}\" ContentTemplate=\"{TemplateBinding ContentTemplate}\" ContentStringFormat=\"{TemplateBinding ContentStringFormat}\" TextElement.Foreground=\"{TemplateBinding Foreground}\" HorizontalAlignment=\"{TemplateBinding HorizontalContentAlignment}\" VerticalAlignment=\"{TemplateBinding VerticalContentAlignment}\"/>\n    </Border>\n    <ControlTemplate.Triggers>\n      <Trigger Property=\"IsHighlighted\" Value=\"True\"><Setter TargetName=\"ItemChrome\" Property=\"Background\" Value=\"{DynamicResource ComboBoxHoverBrush}\"/></Trigger>\n      <Trigger Property=\"IsSelected\" Value=\"True\"><Setter TargetName=\"ItemChrome\" Property=\"Background\" Value=\"{DynamicResource ComboBoxSelectedBrush}\"/></Trigger>\n      <Trigger Property=\"IsEnabled\" Value=\"False\"><Setter Property=\"Opacity\" Value=\"0.55\"/></Trigger>\n    </ControlTemplate.Triggers>\n  </ControlTemplate></Setter.Value></Setter>\n</Style>");		System.Windows.Controls.ComboBox[] array = new System.Windows.Controls.ComboBox[6] { OutputModeBox, FormatBox, VideoModeBox, VideoResolutionBox, VideoBitrateBox, ThemeBox };
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Template = template;
			array[i].ItemContainerStyle = itemStyle;
		}
	}

	private void LoadMicrophones()
	{
		try
		{
			List<AudioDevice> systemAudioDevices = Recorder.GetSystemAudioDevices(AudioDeviceSource.InputDevices);
			MicrophoneBox.ItemsSource = systemAudioDevices;
			MicrophoneBox.DisplayMemberPath = "FriendlyName";
			MicrophoneBox.SelectedValuePath = "DeviceName";
			MicrophoneBox.SelectedValue = _settings.MicrophoneDevice;
		}
		catch
		{
			MicrophoneBox.ItemsSource = Array.Empty<AudioDevice>();
		}
	}

	private static void Select(System.Windows.Controls.ComboBox combo, string value)
	{
		combo.SelectedItem = combo.Items.Cast<ComboBoxItem>().FirstOrDefault((ComboBoxItem i) => string.Equals(i.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) ?? combo.Items[0];
	}

	private static void SelectByTag(System.Windows.Controls.ComboBox combo, string value)
	{
		combo.SelectedItem = combo.Items.Cast<ComboBoxItem>().FirstOrDefault((ComboBoxItem i) => i.Tag?.ToString() == value) ?? combo.Items[0];
	}

	private void RegisterHotkeys()
	{
		nint handle = new WindowInteropHelper(this).Handle;
		UnregisterHotkeys();
		RegisterShortcut(handle, 9001, _settings.Shortcut);
		if (!string.IsNullOrWhiteSpace(_settings.VideoShortcut))
		{
			RegisterShortcut(handle, 9002, _settings.VideoShortcut);
		}
		if (!string.IsNullOrWhiteSpace(_settings.OcrShortcut))
		{
			RegisterShortcut(handle, 9003, _settings.OcrShortcut);
		}
	}

	private void UnregisterHotkeys()
	{
		nint handle = new WindowInteropHelper(this).Handle;
		NativeWindow.UnregisterHotKey(handle, 9001);
		NativeWindow.UnregisterHotKey(handle, 9002);
		NativeWindow.UnregisterHotKey(handle, 9003);
	}

	private static bool RegisterShortcut(nint handle, int id, string shortcut)
	{
		if (string.IsNullOrWhiteSpace(shortcut))
		{
			return true;
		}
		var (modifiers, vk) = ParseShortcut(shortcut);
		return NativeWindow.RegisterHotKey(handle, id, modifiers, vk);
	}

	internal static (uint Modifiers, uint Key) ParseShortcut(string shortcut)
	{
		uint num = 0u;
		uint item = 65u;
		string[] array = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (string text in array)
		{
			Key result;
			if (text.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
			{
				num |= 2;
			}
			else if (text.Equals("Shift", StringComparison.OrdinalIgnoreCase))
			{
				num |= 4;
			}
			else if (text.Equals("Alt", StringComparison.OrdinalIgnoreCase))
			{
				num |= 1;
			}
			else if (Enum.TryParse<Key>(text, ignoreCase: true, out result))
			{
				item = (uint)KeyInterop.VirtualKeyFromKey(result);
			}
		}
		return (Modifiers: num, Key: item);
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg != 786)
		{
			return IntPtr.Zero;
		}
		nint num = wParam;
		if (((IntPtr)num).ToInt32() == 9001)
		{
			BeginCapture();
		}
		else
		{
			num = wParam;
			if (((IntPtr)num).ToInt32() == 9002)
			{
				BeginVideoRecording();
			}
			else
			{
				num = wParam;
				if (((IntPtr)num).ToInt32() != 9003)
				{
					return IntPtr.Zero;
				}
				BeginOcr();
			}
		}
		handled = true;
		return IntPtr.Zero;
	}

	private void BeginCapture()
	{
		new CaptureOverlay(_settings).Show();
	}

	private void BeginOcr()
	{
		new OcrOverlay().Show();
	}

	private void StartCapture(object sender, RoutedEventArgs e)
	{
		BeginCapture();
	}

	private void StartVideo(object sender, RoutedEventArgs e)
	{
		BeginVideoRecording();
	}

	private void BeginVideoRecording()
	{
		if (_recording == null)
		{
			if (_settings.VideoCaptureMode == "指定区域")
			{
				new RegionSelectionOverlay(_settings, StartVideoWithRegion).Show();
			}
			else
			{
				StartVideoWithRegion(null);
			}
		}
	}

	private void StartVideoWithRegion(ScreenRect? region)
	{
		if (region != null && _settings.RememberVideoRegion)
		{
			_settings.VideoRegionLeft = (int)region.Left;
			_settings.VideoRegionTop = (int)region.Top;
			_settings.VideoRegionWidth = (int)region.Width;
			_settings.VideoRegionHeight = (int)region.Height;
			SettingsStore.Save(_settings);
		}
		try
		{
			_recording = new VideoRecordingService();
			_recording.Completed += RecordingCompleted;
			_recording.Failed += RecordingFailed;
			_recordingWindow = new VideoRecordingWindow(_recording, delegate
			{
				StartActualRecording(region);
			});
			_recordingWindow.Closed += delegate
			{
				RecordingStatus? recordingStatus = _recording?.Status;
				if ((recordingStatus.HasValue && (uint)(recordingStatus.GetValueOrDefault() - 2) <= 1u) ? true : false)
				{
					_recording.Stop();
				}
				else
				{
					_recording?.Dispose();
					_recording = null;
				}
			};
			_recordingWindow.Show();
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(Localization.Text("无法开始录制：") + ex.Message, "QuickShot", MessageBoxButton.OK, MessageBoxImage.Hand);
			_recording?.Dispose();
			_recording = null;
		}
	}

	private void StartActualRecording(ScreenRect? region)
	{
		try
		{
			_recording?.Start(_settings, region);
		}
		catch (Exception ex)
		{
			_recordingWindow?.Close();
			_recording?.Dispose();
			_recording = null;
			System.Windows.MessageBox.Show(Localization.Text("无法开始录制：") + ex.Message, "QuickShot", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void RecordingCompleted(object? sender, string path)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			_recordingWindow?.Close();
			_recording?.Dispose();
			_recording = null;
			ShowVideos(null, null);
			if (_settings.OpenVideoFolderAfterRecording)
			{
				OpenFolder(Path.GetDirectoryName(path));
			}
		});
	}

	private void RecordingFailed(object? sender, string error)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			_recordingWindow?.Close();
			System.Windows.MessageBox.Show(error, "QuickShot", MessageBoxButton.OK, MessageBoxImage.Hand);
			_recording?.Dispose();
			_recording = null;
		});
	}

	private void ShowFromTray()
	{
		Show();
		base.WindowState = WindowState.Normal;
		ResumeBackgroundVideo();
		base.Topmost = true;
		base.Topmost = false;
		Activate();
		InvalidateVisual();
		UpdateLayout();
	}

	internal void RestoreFromSecondInstance()
	{
		ShowFromTray();
	}

	private void TitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
		{
			try
			{
				DragMove();
			}
			catch (InvalidOperationException)
			{
			}
		}
	}

	private void StopTitleBarDrag(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
	}

	private void BackgroundVideoEnded(object sender, RoutedEventArgs e)
	{
		if (base.IsVisible)
		{
			BackgroundVideo.Position = TimeSpan.Zero;
			BackgroundVideo.Play();
		}
	}

	private void MinimizeWindow(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void ToggleMaximizeWindow(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void CloseWindow(object sender, RoutedEventArgs e)
	{
		Close();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		e.Cancel = true;
		SuspendBackgroundVideo();
		Hide();
	}

	private void SuspendBackgroundVideo()
	{
		if (BackgroundVideo.Source != null)
		{
			_backgroundVideoSource = BackgroundVideo.Source;
		}
		BackgroundVideo.Stop();
		BackgroundVideo.Close();
		BackgroundVideo.Source = null;
	}

	private void ResumeBackgroundVideo()
	{
		if (BackgroundVideo.Source == null && _backgroundVideoSource != null)
		{
			BackgroundVideo.Source = _backgroundVideoSource;
		}
		BackgroundVideo.Play();
	}

	private void ShowCapture(object sender, RoutedEventArgs e)
	{
		ShowPage(CapturePage);
	}

	private void ShowRecent(object sender, RoutedEventArgs e)
	{
		ShowPage(RecentPage);
		RecentList.ItemsSource = ScreenshotStore.Recent(_settings.SaveRoot);
	}

	private void ShowVideos(object sender, RoutedEventArgs e)
	{
		ShowPage(VideosPage);
		VideoList.ItemsSource = VideoStore.Recent(VideoStore.Root(_settings));
	}

	private void ShowSettings(object sender, RoutedEventArgs e)
	{
		ShowPage(SettingsPage);
	}

	private void ShowPage(UIElement page)
	{
		CapturePage.Visibility = ((page != CapturePage) ? Visibility.Collapsed : Visibility.Visible);
		RecentPage.Visibility = ((page != RecentPage) ? Visibility.Collapsed : Visibility.Visible);
		VideosPage.Visibility = ((page != VideosPage) ? Visibility.Collapsed : Visibility.Visible);
		SettingsPage.Visibility = ((page != SettingsPage) ? Visibility.Collapsed : Visibility.Visible);
		if (_languagePage != null)
		{
			_languagePage.Visibility = ((page != _languagePage) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void OpenScreenshotFolder(object sender, RoutedEventArgs e)
	{
		OpenFolder(Path.Combine(AppContext.BaseDirectory, "截图归类"));
	}

	private void OpenVideoFolder(object sender, RoutedEventArgs e)
	{
		OpenFolder(VideoStore.Root(_settings));
	}

	private static void OpenFolder(string path)
	{
		Directory.CreateDirectory(path);
		Process.Start(new ProcessStartInfo("explorer.exe", path)
		{
			UseShellExecute = true
		});
	}

	private void OpenImage(object sender, MouseButtonEventArgs e)
	{
		if (!((sender as FrameworkElement)?.Tag is string text) || !File.Exists(text) || (string.Equals(_lastOpenedImage, text, StringComparison.OrdinalIgnoreCase) && DateTime.UtcNow - _lastOpenedImageAt < TimeSpan.FromSeconds(2.0)))
		{
			return;
		}
		_lastOpenedImage = text;
		_lastOpenedImageAt = DateTime.UtcNow;
		e.Handled = true;
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = text,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(Localization.Text("无法打开图片：") + ex.Message, "QuickShot", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void OpenVideo(object sender, MouseButtonEventArgs e)
	{
		if (!((sender as FrameworkElement)?.Tag is string text) || !File.Exists(text) || (string.Equals(_lastOpenedVideo, text, StringComparison.OrdinalIgnoreCase) && DateTime.UtcNow - _lastOpenedVideoAt < TimeSpan.FromSeconds(2.0)))
		{
			return;
		}
		_lastOpenedVideo = text;
		_lastOpenedVideoAt = DateTime.UtcNow;
		e.Handled = true;
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = text,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(Localization.Text("无法打开视频：") + ex.Message, "QuickShot", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void CaptureContextTarget(object sender, ContextMenuEventArgs e)
	{
		_contextTarget = sender as FrameworkElement;
	}

	private void OnScreenshotSaved()
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (RecentPage.Visibility == Visibility.Visible)
			{
				RecentList.ItemsSource = ScreenshotStore.Recent(_settings.SaveRoot);
			}
		});
	}

	private void ChooseFolder(object sender, RoutedEventArgs e)
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			SelectedPath = _settings.SaveRoot
		};
		if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			SaveRootBox.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private void ChooseVideoFolder(object sender, RoutedEventArgs e)
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			SelectedPath = VideoStore.Root(_settings)
		};
		if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			VideoSaveRootBox.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private void SettingChanged(object? sender, RoutedEventArgs e)
	{
		if (base.IsLoaded)
		{
			_settings.OutputMode = (OutputModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "SaveAndCopy";
			_settings.ImageFormat = (FormatBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PNG";
			_settings.SaveRoot = SaveRootBox.Text;
			SettingsStore.Save(_settings);
		}
	}

	private void DateFolderChanged(object sender, RoutedEventArgs e)
	{
		if (base.IsLoaded)
		{
			_settings.UseDateFolderForExport = DateFolderBox.IsChecked == true;
			SettingsStore.Save(_settings);
		}
	}

	private void ThemeChanged(object sender, RoutedEventArgs e)
	{
		if (base.IsLoaded)
		{
			_settings.Theme = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Dark";
			ApplyTheme(_settings.Theme);
			SettingsStore.Save(_settings);
		}
	}

	private void ShortcutKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		UpdateShortcutFromKey(e, video: false);
	}

	private void VideoShortcutKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		UpdateShortcutFromKey(e, video: true);
	}

	private void OcrShortcutKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
		switch (key)
		{
		case Key.Back:
		case Key.Delete:
			ClearOcrShortcut();
			e.Handled = true;
			return;
		case Key.LeftShift:
		case Key.RightShift:
		case Key.LeftCtrl:
		case Key.RightCtrl:
		case Key.LeftAlt:
		case Key.RightAlt:
			e.Handled = true;
			return;
		}
		List<string> list = new List<string>();
		if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None)
		{
			list.Add("Ctrl");
		}
		if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.None)
		{
			list.Add("Shift");
		}
		if ((Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.None)
		{
			list.Add("Alt");
		}
		if (list.Count == 0)
		{
			SetOcrShortcutHint("快捷键至少需要 Ctrl、Shift 或 Alt");
		}
		else
		{
			list.Add(key.ToString());
			SetOcrShortcut(string.Join(" + ", list));
		}
		e.Handled = true;
	}

	private void UpdateShortcutFromKey(System.Windows.Input.KeyEventArgs e, bool video)
	{
		Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
		if (key == Key.Back || key == Key.Delete)
		{
			ClearShortcut(video);
			e.Handled = true;
			return;
		}
		if ((uint)(key - 116) <= 5u)
		{
			e.Handled = true;
			return;
		}
		List<string> list = new List<string>();
		if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.None)
		{
			list.Add("Ctrl");
		}
		if ((Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.None)
		{
			list.Add("Shift");
		}
		if ((Keyboard.Modifiers & ModifierKeys.Alt) != ModifierKeys.None)
		{
			list.Add("Alt");
		}
		if (list.Count == 0)
		{
			SetShortcutHint(video, "快捷键至少需要 Ctrl、Shift 或 Alt");
			e.Handled = true;
		}
		else
		{
			list.Add(key.ToString());
			SetShortcut(string.Join(" + ", list), video);
			e.Handled = true;
		}
	}

	private void ClearShortcut(bool video)
	{
		NativeWindow.UnregisterHotKey(new WindowInteropHelper(this).Handle, video ? 9002 : 9001);
		if (video)
		{
			_settings.VideoShortcut = "";
		}
		else
		{
			_settings.Shortcut = "";
		}
		SetShortcutBox(video, "");
		SetShortcutHint(video, "快捷键已清空");
		SettingsStore.Save(_settings);
	}

	private void SetShortcut(string shortcut, bool video)
	{
		if ((!video && string.Equals(shortcut, _settings.VideoShortcut, StringComparison.OrdinalIgnoreCase)) || (video && string.Equals(shortcut, _settings.Shortcut, StringComparison.OrdinalIgnoreCase)) || string.Equals(shortcut, _settings.OcrShortcut, StringComparison.OrdinalIgnoreCase))
		{
			AppDialog appDialog = new AppDialog("快捷键不能重复", "截图、文字识别和视频录制必须使用不同的快捷键，请重新设置。", "重新设置");
			appDialog.Owner = this;
			appDialog.ShowDialog();
			SetShortcutBox(video, video ? _settings.VideoShortcut : _settings.Shortcut);
			return;
		}
		nint handle = new WindowInteropHelper(this).Handle;
		int id = (video ? 9002 : 9001);
		string text = (video ? _settings.VideoShortcut : _settings.Shortcut);
		NativeWindow.UnregisterHotKey(handle, id);
		if (!RegisterShortcut(handle, id, shortcut))
		{
			RegisterShortcut(handle, id, text);
			SetShortcutBox(video, text);
			SetShortcutHint(video, "该快捷键已被其他程序占用，已保留原设置");
			return;
		}
		if (video)
		{
			_settings.VideoShortcut = shortcut;
		}
		else
		{
			_settings.Shortcut = shortcut;
		}
		SetShortcutBox(video, shortcut);
		SetShortcutHint(video, "快捷键已更新");
		SettingsStore.Save(_settings);
	}

	private void ClearOcrShortcut()
	{
		NativeWindow.UnregisterHotKey(new WindowInteropHelper(this).Handle, 9003);
		_settings.OcrShortcut = "";
		if (_ocrShortcutBox != null)
		{
			_ocrShortcutBox.Text = "";
		}
		SetOcrShortcutHint("快捷键已清空");
		SettingsStore.Save(_settings);
	}

	private void SetOcrShortcut(string shortcut)
	{
		if (string.Equals(shortcut, _settings.Shortcut, StringComparison.OrdinalIgnoreCase) || string.Equals(shortcut, _settings.VideoShortcut, StringComparison.OrdinalIgnoreCase))
		{
			AppDialog appDialog = new AppDialog("快捷键不能重复", "截图、文字识别和视频录制必须使用不同的快捷键，请重新设置。", "重新设置");
			appDialog.Owner = this;
			appDialog.ShowDialog();
			if (_ocrShortcutBox != null)
			{
				_ocrShortcutBox.Text = _settings.OcrShortcut;
			}
			return;
		}
		nint handle = new WindowInteropHelper(this).Handle;
		string ocrShortcut = _settings.OcrShortcut;
		NativeWindow.UnregisterHotKey(handle, 9003);
		if (!RegisterShortcut(handle, 9003, shortcut))
		{
			RegisterShortcut(handle, 9003, ocrShortcut);
			if (_ocrShortcutBox != null)
			{
				_ocrShortcutBox.Text = ocrShortcut;
			}
			SetOcrShortcutHint("该快捷键已被其他程序占用，已保留原设置");
			return;
		}
		_settings.OcrShortcut = shortcut;
		if (_ocrShortcutBox != null)
		{
			_ocrShortcutBox.Text = shortcut;
		}
		SetOcrShortcutHint("快捷键已更新");
		SettingsStore.Save(_settings);
	}

	private void SetOcrShortcutHint(string value)
	{
		if (_ocrShortcutHint != null)
		{
			_ocrShortcutHint.Text = value;
		}
	}

	private void SetShortcutBox(bool video, string value)
	{
		if (video)
		{
			VideoShortcutBox.Text = value;
		}
		else
		{
			ShortcutBox.Text = value;
		}
	}

	private void SetShortcutHint(bool video, string value)
	{
		if (video)
		{
			VideoShortcutHint.Text = value;
		}
		else
		{
			ShortcutHint.Text = value;
		}
	}

	private void VideoFramerateChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (base.IsLoaded)
		{
			int num = (int)Math.Round(VideoFramerateSlider.Value);
			VideoFramerateValueText.Text = $"{num} fps";
			_settings.VideoFramerate = num;
			_framerateSaveTimer.Stop();
			_framerateSaveTimer.Start();
		}
	}

	private void VideoSettingChanged(object? sender, RoutedEventArgs e)
	{
		if (!base.IsLoaded)
		{
			return;
		}
		_settings.VideoCaptureMode = (VideoModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "全屏";
		_settings.VideoCaptureMode = Localization.Chinese(_settings.VideoCaptureMode);
		_settings.RememberVideoRegion = RememberRegionBox.IsChecked == true;
		_settings.VideoFramerate = (int)VideoFramerateSlider.Value;
		_settings.VideoBitrateKbps = int.Parse((VideoBitrateBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "12000");
		string text = (VideoResolutionBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
		UserSettings settings = _settings;
		UserSettings settings2 = _settings;
		if (!(text == "1920 x 1080"))
		{
			if (!(text == "1280 x 720"))
			{
				settings.VideoWidth = 0;
				settings2.VideoHeight = 0;
			}
			else
			{
				settings.VideoWidth = 1280;
				settings2.VideoHeight = 720;
			}
		}
		else
		{
			settings.VideoWidth = 1920;
			settings2.VideoHeight = 1080;
		}
		_settings.VideoSaveRoot = VideoSaveRootBox.Text;
		_settings.OpenVideoFolderAfterRecording = OpenVideoFolderBox.IsChecked == true;
		_settings.MicrophoneEnabledByDefault = DefaultMicrophoneBox.IsChecked == true;
		_settings.MicrophoneDevice = MicrophoneBox.SelectedValue?.ToString() ?? "";
		SettingsStore.Save(_settings);
	}

	private void RenameImage(object sender, RoutedEventArgs e)
	{
		RenameFromContextMenu(sender, video: false);
	}

	private void RenameVideo(object sender, RoutedEventArgs e)
	{
		RenameFromContextMenu(sender, video: true);
	}

	private void DeleteImage(object sender, RoutedEventArgs e)
	{
		DeleteFromContextMenu(video: false);
	}

	private void DeleteVideo(object sender, RoutedEventArgs e)
	{
		DeleteFromContextMenu(video: true);
	}

	private void DeleteFromContextMenu(bool video)
	{
		if (!(_contextTarget?.Tag is string path) || !File.Exists(path))
		{
			return;
		}
		string title = Localization.Text(video ? "删除视频" : "删除截图");
		string message = Localization.Text("确定删除“" + Path.GetFileName(path) + "”吗？此操作无法撤销。");
		if (!AppDialog.Confirm(this, title, message, Localization.Text("删除"), destructive: true))
		{
			return;
		}
		try
		{
			File.Delete(path);
			if (video)
			{
				ShowVideos(null, null);
			}
			else
			{
				ShowRecent(null, null);
			}
		}
		catch (Exception ex)
		{
			AppDialog appDialog = new AppDialog(Localization.Text("删除失败"), ex.Message, Localization.Text("知道了"));
			appDialog.Owner = this;
			appDialog.ShowDialog();
		}
	}

	private void RenameFromContextMenu(object sender, bool video)
	{
		if (!(_contextTarget?.Tag is string text) || !File.Exists(text))
		{
			System.Windows.MessageBox.Show(Localization.Text("找不到要重命名的文件。"), "QuickShot", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
		RenameDialog renameDialog = new RenameDialog(fileNameWithoutExtension)
		{
			Owner = this
		};
		if (renameDialog.ShowDialog() != true)
		{
			return;
		}
		string text2 = renameDialog.NewName;
		if (string.IsNullOrWhiteSpace(text2) || text2 == fileNameWithoutExtension)
		{
			return;
		}
		string extension = Path.GetExtension(text);
		if (!string.Equals(Path.GetExtension(text2), extension, StringComparison.OrdinalIgnoreCase))
		{
			text2 += extension;
		}
		string text3 = Path.Combine(Path.GetDirectoryName(text), text2);
		if (File.Exists(text3))
		{
			System.Windows.MessageBox.Show(Localization.Text("目标文件名已经存在。"), "QuickShot", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		try
		{
			File.Move(text, text3);
			if (video)
			{
				ShowVideos(null, null);
			}
			else
			{
				ShowRecent(null, null);
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(Localization.Text("重命名失败：") + ex.Message, "QuickShot", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		if (_framerateSaveTimer.IsEnabled)
		{
			_framerateSaveTimer.Stop();
			SettingsStore.Save(_settings);
		}
		ScreenshotStore.Saved -= OnScreenshotSaved;
		UnregisterHotkeys();
		_recording?.Dispose();
		_trayIcon.Dispose();
		base.OnClosed(e);
	}

	private void PolishFramerateSlider()
	{
		VideoFramerateSlider.IsSnapToTickEnabled = false;
		VideoFramerateSlider.SmallChange = 0.1;
		VideoFramerateSlider.LargeChange = 5.0;
		VideoFramerateSlider.TickFrequency = 0.0;
		VideoFramerateSlider.Height = 34.0;
		try
		{
			VideoFramerateSlider.Style = (Style)XamlReader.Parse("<Style xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"{x:Type Slider}\">\n  <Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"{x:Type Slider}\">\n    <Grid Margin=\"0,7\"><Track x:Name=\"PART_Track\" Height=\"6\" VerticalAlignment=\"Center\">\n      <Track.DecreaseRepeatButton><RepeatButton Command=\"Slider.DecreaseLarge\" IsTabStop=\"False\"><RepeatButton.Template><ControlTemplate TargetType=\"{x:Type RepeatButton}\"><Border Background=\"#4A90D9\" CornerRadius=\"3\"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>\n      <Track.Thumb><Thumb Width=\"18\" Height=\"18\"><Thumb.Template><ControlTemplate TargetType=\"{x:Type Thumb}\"><Grid SnapsToDevicePixels=\"True\"><Ellipse Fill=\"#4A90D9\"/><Ellipse Width=\"6\" Height=\"6\" Fill=\"#FFFFFF\"/><Grid.Effect><DropShadowEffect BlurRadius=\"4\" ShadowDepth=\"1\" Opacity=\"0.22\"/></Grid.Effect></Grid></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>\n      <Track.IncreaseRepeatButton><RepeatButton Command=\"Slider.IncreaseLarge\" IsTabStop=\"False\"><RepeatButton.Template><ControlTemplate TargetType=\"{x:Type RepeatButton}\"><Border Background=\"#D7DFE8\" CornerRadius=\"3\"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>\n    </Track></Grid>\n  </ControlTemplate></Setter.Value></Setter>\n</Style>");
		}
		catch
		{
		}
	}

	private void EnableSmoothScrolling()
	{
		ScrollViewer scrollViewer = FindVisualChildren<ScrollViewer>(SettingsPage).FirstOrDefault();
		if (scrollViewer != null)
		{
			_settingsScrollState = new SmoothScrollState(scrollViewer);
			scrollViewer.MouseWheel += SmoothScrollMouseWheel;
		}
	}

	private void PolishScrollBars()
	{
		try
		{
			Style style = (Style)XamlReader.Parse("<Style xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"{x:Type ScrollBar}\">\n  <Setter Property=\"Width\" Value=\"12\"/>\n  <Setter Property=\"Background\" Value=\"Transparent\"/>\n  <Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"{x:Type ScrollBar}\">\n    <Border Background=\"Transparent\" Padding=\"3,2\"><Track x:Name=\"PART_Track\" IsDirectionReversed=\"True\">\n      <Track.DecreaseRepeatButton><RepeatButton IsTabStop=\"False\" Background=\"Transparent\"><RepeatButton.Template><ControlTemplate TargetType=\"{x:Type RepeatButton}\"><Border Background=\"Transparent\"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>\n      <Track.Thumb><Thumb MinHeight=\"30\" Cursor=\"Hand\"><Thumb.Template><ControlTemplate TargetType=\"{x:Type Thumb}\"><Border Background=\"#8A7C8FA3\" CornerRadius=\"4\"/><ControlTemplate.Triggers><Trigger Property=\"IsMouseOver\" Value=\"True\"><Setter Property=\"Background\" Value=\"#4A90D9\"/></Trigger><Trigger Property=\"IsDragging\" Value=\"True\"><Setter Property=\"Background\" Value=\"#367BBF\"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>\n      <Track.IncreaseRepeatButton><RepeatButton IsTabStop=\"False\" Background=\"Transparent\"><RepeatButton.Template><ControlTemplate TargetType=\"{x:Type RepeatButton}\"><Border Background=\"Transparent\"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>\n    </Track></Border>\n  </ControlTemplate></Setter.Value></Setter>\n</Style>");
			foreach (System.Windows.Controls.Primitives.ScrollBar item in FindVisualChildren<System.Windows.Controls.Primitives.ScrollBar>(this))
			{
				if (item.Orientation == System.Windows.Controls.Orientation.Vertical)
				{
					item.Style = style;
				}
			}
		}
		catch
		{
		}
	}

	private void SmoothScrollMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer && _settingsScrollState != null && !(scrollViewer.ScrollableHeight <= 0.0))
		{
			e.Handled = true;
			SmoothScrollState settingsScrollState = _settingsScrollState;
			double num = (settingsScrollState.Timer.IsEnabled ? settingsScrollState.TargetOffset : scrollViewer.VerticalOffset);
			settingsScrollState.TargetOffset = Math.Clamp(num - (double)e.Delta / 120.0 * 78.0, 0.0, scrollViewer.ScrollableHeight);
			settingsScrollState.Timer.Start();
		}
	}

	private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T val)
			{
				yield return val;
			}
			foreach (T item in FindVisualChildren<T>(child))
			{
				yield return item;
			}
		}
	}

	private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
	{
		for (DependencyObject dependencyObject = child; dependencyObject != null; dependencyObject = VisualTreeHelper.GetParent(dependencyObject))
		{
			if (dependencyObject is T result)
			{
				return result;
			}
		}
		return null;
	}

	private void ApplyTheme(string theme)
	{
		bool flag = theme == "Light" || (theme == "System" && !SystemParameters.HighContrast && SystemParameters.WindowGlassColor.R > 128);

		bool hasBackground = !string.IsNullOrWhiteSpace(_settings.BackgroundPath) && File.Exists(_settings.BackgroundPath);
		base.Resources["WindowBrush"] = Brush(hasBackground ? (flag ? "#4DF6F6F5" : "#4D151515") : (flag ? "#F6F6F5" : "#151515"));
		base.Resources["SurfaceBrush"] = Brush(flag ? "#FFFFFF" : "#202020");
		base.Resources["SurfaceAltBrush"] = Brush(flag ? "#EEF3F5" : "#29343A");
		base.Resources["BorderBrush"] = Brush(flag ? "#DFDFDC" : "#393939");
		base.Resources["TextBrush"] = Brush(flag ? "#1C1C1C" : "#F5F5F5");
		base.Resources["MutedBrush"] = Brush(flag ? "#666662" : "#A1A1AA");
		base.Resources["ComboBoxItemBrush"] = Brush(flag ? "#1C1C1C" : "#F5F5F5");
		base.Resources["ComboBoxHoverBrush"] = Brush(flag ? "#E7E7E4" : "#181818");
		base.Resources["ComboBoxSelectedBrush"] = Brush(flag ? "#D9D9D5" : "#333333");
		base.Resources["BackdropVeilBrush"] = Brush(flag ? "#BFF3F6F8" : "#B9161C22");
		base.Resources["SidebarBrush"] = Brush(flag ? "#F2FFFFFF" : "#E6141E25");
		base.Resources["TitleBarBrush"] = Brush(flag ? "#F2FFFFFF" : "#E6141E25");
		base.Resources["AmbientBrush"] = Brush(flag ? "#CFE6F8" : "#3F7FA8");
		base.Resources["AmbientBrush2"] = Brush(flag ? "#E8DFF7" : "#557098");
	}

	private static SolidColorBrush Brush(string value)
	{
		return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QuickShot;component/mainwindow.xaml", UriKind.Relative);
			System.Windows.Application.LoadComponent(this, resourceLocator);
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
			((Border)target).MouseLeftButtonDown += TitleBarMouseLeftButtonDown;
			break;
		case 2:
			((System.Windows.Controls.Button)target).Click += MinimizeWindow;
			((System.Windows.Controls.Button)target).MouseLeftButtonDown += StopTitleBarDrag;
			break;
		case 3:
			((System.Windows.Controls.Button)target).Click += ToggleMaximizeWindow;
			((System.Windows.Controls.Button)target).MouseLeftButtonDown += StopTitleBarDrag;
			break;
		case 4:
			((System.Windows.Controls.Button)target).Click += CloseWindow;
			((System.Windows.Controls.Button)target).MouseLeftButtonDown += StopTitleBarDrag;
			break;
		case 5:
			BackgroundVideo = (MediaElement)target;
			BackgroundVideo.MediaEnded += BackgroundVideoEnded;
			break;
		case 6:
			((System.Windows.Controls.Button)target).Click += ShowCapture;
			break;
		case 7:
			RecentNav = (System.Windows.Controls.Button)target;
			RecentNav.Click += ShowRecent;
			break;
		case 8:
			VideoNav = (System.Windows.Controls.Button)target;
			VideoNav.Click += ShowVideos;
			break;
		case 9:
			SettingsNav = (System.Windows.Controls.Button)target;
			SettingsNav.Click += ShowSettings;
			break;
		case 10:
			CapturePage = (Grid)target;
			break;
		case 11:
			((System.Windows.Controls.Button)target).Click += StartCapture;
			break;
		case 12:
			((System.Windows.Controls.Button)target).Click += StartVideo;
			break;
		case 13:
			RecentPage = (Grid)target;
			break;
		case 14:
			((System.Windows.Controls.Button)target).Click += OpenScreenshotFolder;
			break;
		case 15:
			RecentList = (ItemsControl)target;
			break;
		case 19:
			VideosPage = (Grid)target;
			break;
		case 20:
			((System.Windows.Controls.Button)target).Click += OpenVideoFolder;
			break;
		case 21:
			VideoList = (ItemsControl)target;
			break;
		case 25:
			SettingsPage = (Grid)target;
			break;
		case 26:
			OutputModeBox = (System.Windows.Controls.ComboBox)target;
			OutputModeBox.SelectionChanged += SettingChanged;
			break;
		case 27:
			FormatBox = (System.Windows.Controls.ComboBox)target;
			FormatBox.SelectionChanged += SettingChanged;
			break;
		case 28:
			((System.Windows.Controls.Button)target).Click += ChooseFolder;
			break;
		case 29:
			SaveRootBox = (System.Windows.Controls.TextBox)target;
			SaveRootBox.TextChanged += SettingChanged;
			break;
		case 30:
			DateFolderBox = (System.Windows.Controls.CheckBox)target;
			DateFolderBox.Click += DateFolderChanged;
			break;
		case 31:
			VideoModeBox = (System.Windows.Controls.ComboBox)target;
			VideoModeBox.SelectionChanged += VideoSettingChanged;
			break;
		case 32:
			RememberRegionBox = (System.Windows.Controls.CheckBox)target;
			RememberRegionBox.Click += VideoSettingChanged;
			break;
		case 33:
			VideoResolutionBox = (System.Windows.Controls.ComboBox)target;
			VideoResolutionBox.SelectionChanged += VideoSettingChanged;
			break;
		case 34:
			VideoFramerateSlider = (Slider)target;
			VideoFramerateSlider.ValueChanged += VideoFramerateChanged;
			break;
		case 35:
			VideoFramerateValueText = (TextBlock)target;
			break;
		case 36:
			VideoBitrateBox = (System.Windows.Controls.ComboBox)target;
			VideoBitrateBox.SelectionChanged += VideoSettingChanged;
			break;
		case 37:
			((System.Windows.Controls.Button)target).Click += ChooseVideoFolder;
			break;
		case 38:
			VideoSaveRootBox = (System.Windows.Controls.TextBox)target;
			VideoSaveRootBox.TextChanged += VideoSettingChanged;
			break;
		case 39:
			OpenVideoFolderBox = (System.Windows.Controls.CheckBox)target;
			OpenVideoFolderBox.Click += VideoSettingChanged;
			break;
		case 40:
			DefaultMicrophoneBox = (System.Windows.Controls.CheckBox)target;
			DefaultMicrophoneBox.Click += VideoSettingChanged;
			break;
		case 41:
			MicrophoneBox = (System.Windows.Controls.ComboBox)target;
			MicrophoneBox.SelectionChanged += VideoSettingChanged;
			break;
		case 42:
			VideoShortcutHint = (TextBlock)target;
			break;
		case 43:
			VideoShortcutBox = (System.Windows.Controls.TextBox)target;
			VideoShortcutBox.PreviewKeyDown += VideoShortcutKeyDown;
			break;
		case 44:
			ThemeBox = (System.Windows.Controls.ComboBox)target;
			ThemeBox.SelectionChanged += ThemeChanged;
			break;
		case 45:
			ShortcutHint = (TextBlock)target;
			break;
		case 46:
			ShortcutBox = (System.Windows.Controls.TextBox)target;
			ShortcutBox.PreviewKeyDown += ShortcutKeyDown;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 16:
			((System.Windows.Controls.Button)target).MouseDoubleClick += OpenImage;
			((System.Windows.Controls.Button)target).ContextMenuOpening += CaptureContextTarget;
			break;
		case 17:
			((MenuItem)target).Click += RenameImage;
			break;
		case 18:
			((MenuItem)target).Click += DeleteImage;
			break;
		case 22:
			((System.Windows.Controls.Button)target).MouseDoubleClick += OpenVideo;
			((System.Windows.Controls.Button)target).ContextMenuOpening += CaptureContextTarget;
			break;
		case 23:
			((MenuItem)target).Click += RenameVideo;
			break;
		case 24:
			((MenuItem)target).Click += DeleteVideo;
			break;
		case 19:
		case 20:
		case 21:
			break;
		}
	}
}
