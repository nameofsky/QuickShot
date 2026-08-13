using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace QuickShot;

public class VideoRecordingWindow : Window, IComponentConnector
{
	private readonly VideoRecordingService _service;

	private readonly Action _startRecording;

	private readonly Stopwatch _timer = new Stopwatch();

	private readonly DispatcherTimer _clock = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(250.0)
	};

	internal Ellipse StatusDot;

	internal TextBlock TitleText;

	internal TextBlock TimerText;

	internal Button MicrophoneButton;

	internal TextBlock LevelText;

	internal ProgressBar LevelBar;

	internal Button StartButton;

	internal Button PauseButton;

	internal Button StopButton;

	private bool _contentLoaded;

	public VideoRecordingWindow(VideoRecordingService service, Action startRecording)
	{
		_service = service;
		_startRecording = startRecording;
		InitializeComponent();
		Localization.Apply(this);
		Localization.LanguageChanged += OnLanguageChanged;
		_service.AudioLevelChanged += OnAudioLevel;
		_service.StatusChanged += OnStatusChanged;
		_clock.Tick += delegate
		{
			TimerText.Text = _timer.Elapsed.ToString("mm\\:ss");
		};
		base.SourceInitialized += delegate
		{
			ExcludeFromCapture();
		};
		base.Loaded += delegate
		{
			base.Top = SystemParameters.WorkArea.Top + 8.0;
			base.Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - base.Width) / 2.0;
			UpdateMicrophoneButton();
		};
		base.Closed += delegate
		{
			_clock.Stop();
			Localization.LanguageChanged -= OnLanguageChanged;
			_service.AudioLevelChanged -= OnAudioLevel;
			_service.StatusChanged -= OnStatusChanged;
		};
	}

	private void OnLanguageChanged()
	{
		if (base.Dispatcher.HasShutdownStarted || base.Dispatcher.HasShutdownFinished)
		{
			return;
		}

		if (base.Dispatcher.CheckAccess())
		{
			Localization.Apply(this);
		}
		else
		{
			base.Dispatcher.BeginInvoke(new Action(() => Localization.Apply(this)));
		}
	}

	private void StartRecording(object sender, RoutedEventArgs e)
	{
		StartButton.IsEnabled = false;
		PauseButton.IsEnabled = true;
		StopButton.IsEnabled = true;
		MicrophoneButton.IsEnabled = true;
		TitleText.Text = Localization.Text("正在录制");
		_startRecording();
		UpdateMicrophoneButton();
	}

	private async void ToggleMicrophone(object sender, RoutedEventArgs e)
	{
		bool targetEnabled = !_service.MicrophoneEnabled;
		MicrophoneButton.IsEnabled = false;
		MicrophoneButton.Content = Localization.Text(targetEnabled ? "麦克风：开启中" : "麦克风：关闭中");
		bool changed = await _service.SetMicrophoneEnabledAsync(targetEnabled);
		UpdateMicrophoneButton();
		Button microphoneButton = MicrophoneButton;
		RecordingStatus status = _service.Status;
		bool isEnabled = (uint)(status - 2) <= 1u;
		microphoneButton.IsEnabled = isEnabled;
		if (!changed)
		{
			AppDialog appDialog = new AppDialog(Localization.Text("麦克风切换失败"), Localization.Text("当前录制引擎未能切换麦克风状态。"), Localization.Text("知道了"));
			appDialog.Owner = this;
			appDialog.ShowDialog();
		}
	}

	private void TogglePause(object sender, RoutedEventArgs e)
	{
		if (_service.Status == RecordingStatus.Paused)
		{
			_service.Resume();
			PauseButton.Content = Localization.Text("暂停");
		}
		else
		{
			_service.Pause();
			PauseButton.Content = Localization.Text("恢复");
		}
	}

	private void StopRecording(object sender, RoutedEventArgs e)
	{
		if (_service.Status == RecordingStatus.Idle)
		{
			Close();
			return;
		}
		_service.Stop();
		PauseButton.IsEnabled = false;
		MicrophoneButton.IsEnabled = false;
		StopButton.IsEnabled = false;
	}

	private void OnAudioLevel(object? sender, float level)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (!_service.MicrophoneEnabled)
			{
				level = -60f;
			}
			LevelText.Text = $"{level:0} dB";
			LevelBar.Value = Math.Clamp(level + 60f, 0f, 60f);
		});
	}

	private void OnStatusChanged(object? sender, RecordingStatus status)
	{
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (status == RecordingStatus.Paused)
			{
				_timer.Stop();
				PauseButton.Content = Localization.Text("恢复");
			}
			if (status == RecordingStatus.Recording)
			{
				_timer.Start();
				_clock.Start();
				PauseButton.Content = Localization.Text("暂停");
			}
			if (status == RecordingStatus.Finalizing)
			{
				_timer.Stop();
				_clock.Stop();
				PauseButton.IsEnabled = false;
				MicrophoneButton.IsEnabled = false;
				StopButton.IsEnabled = false;
			}
		});
	}

	private void UpdateMicrophoneButton()
	{
		MicrophoneButton.Content = Localization.Text(_service.MicrophoneEnabled ? "麦克风：开" : "麦克风：关");
		MicrophoneButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_service.MicrophoneEnabled ? "#F59E0B" : "#52525B"));
		MicrophoneButton.Foreground = new SolidColorBrush(Colors.White);
	}

	private void ExcludeFromCapture()
	{
		try
		{
			NativeWindow.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 17u);
		}
		catch
		{
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/QuickShot;component/videorecordingwindow.xaml", UriKind.Relative);
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
			StatusDot = (Ellipse)target;
			break;
		case 2:
			TitleText = (TextBlock)target;
			break;
		case 3:
			TimerText = (TextBlock)target;
			break;
		case 4:
			MicrophoneButton = (Button)target;
			MicrophoneButton.Click += ToggleMicrophone;
			break;
		case 5:
			LevelText = (TextBlock)target;
			break;
		case 6:
			LevelBar = (ProgressBar)target;
			break;
		case 7:
			StartButton = (Button)target;
			StartButton.Click += StartRecording;
			break;
		case 8:
			PauseButton = (Button)target;
			PauseButton.Click += TogglePause;
			break;
		case 9:
			StopButton = (Button)target;
			StopButton.Click += StopRecording;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
