using System;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using ScreenRecorderLib;

namespace QuickShot;

public sealed class VideoRecordingService : IDisposable
{
	private Recorder? _recorder;

	private AudioLevelMonitor? _levelMonitor;

	private string _microphoneDevice = "";

	private bool _microphoneEnabled;

	private bool _initialAudioConfigured;

	private readonly object _microphoneLock = new object();

	public RecordingStatus Status { get; private set; }

	public bool MicrophoneEnabled => _microphoneEnabled;

	public event EventHandler<RecordingStatus>? StatusChanged;

	public event EventHandler<string>? Completed;

	public event EventHandler<string>? Failed;

	public event EventHandler<float>? AudioLevelChanged;

	public void Start(UserSettings settings, ScreenRect? region)
	{
		if (_recorder != null)
		{
			throw new InvalidOperationException(Localization.Text("录制已经在进行中。"));
		}
		RecorderOptions options = RecorderOptions.DefaultMainMonitor;
		options.OutputOptions.RecorderMode = RecorderMode.Video;
		options.VideoEncoderOptions.Framerate = Math.Clamp(settings.VideoFramerate, 30, 144);
		options.VideoEncoderOptions.Bitrate = Math.Clamp(settings.VideoBitrateKbps, 1000, 100000) * 1000;
		options.VideoEncoderOptions.IsFixedFramerate = true;
		options.VideoEncoderOptions.IsHardwareEncodingEnabled = true;
		options.VideoEncoderOptions.IsMp4FastStartEnabled = true;
		options.VideoEncoderOptions.Encoder = new H264VideoEncoder
		{
			BitrateMode = H264BitrateControlMode.Quality,
			EncoderProfile = H264Profile.High
		};
		options.AudioOptions.IsAudioEnabled = !string.IsNullOrWhiteSpace(settings.MicrophoneDevice);
		options.AudioOptions.AudioInputDevice = settings.MicrophoneDevice;
		options.AudioOptions.AudioOutputDevice = null;
		options.AudioOptions.Bitrate = AudioBitrate.bitrate_192kbps;
		options.AudioOptions.Channels = AudioChannels.Stereo;
		DisplayRecordingSource monitor = (DisplayRecordingSource)options.SourceOptions.RecordingSources[0];
		monitor.IsCursorCaptureEnabled = true;
		if (region != null)
		{
			monitor.SourceRect = region;
		}
		if (settings.VideoWidth > 0 && settings.VideoHeight > 0)
		{
			options.OutputOptions.OutputFrameSize = new ScreenSize(settings.VideoWidth, settings.VideoHeight);
		}
		_microphoneDevice = settings.MicrophoneDevice;
		_microphoneEnabled = settings.MicrophoneEnabledByDefault && !string.IsNullOrWhiteSpace(_microphoneDevice);
		PrepareLevelMonitor();
		_recorder = Recorder.CreateRecorder(options);
		_recorder.OnStatusChanged += RecorderStatusChanged;
		_recorder.OnRecordingComplete += RecorderCompleted;
		_recorder.OnRecordingFailed += RecorderFailed;
		_recorder.Record(VideoStore.CreatePath(settings));
		SetStatus(RecordingStatus.Recording);
	}

	public void Pause()
	{
		if (Status == RecordingStatus.Recording)
		{
			_recorder?.Pause();
			StopLevelMonitor();
			SetStatus(RecordingStatus.Paused);
		}
	}

	public void Resume()
	{
		if (Status == RecordingStatus.Paused)
		{
			_recorder?.Resume();
			if (_microphoneEnabled)
			{
				StartLevelMonitor();
			}
			SetStatus(RecordingStatus.Recording);
		}
	}

	public void Stop()
	{
		if (_recorder != null)
		{
			SetStatus(RecordingStatus.Finalizing);
			_recorder.Stop();
		}
	}

	public Task<bool> SetMicrophoneEnabledAsync(bool enabled)
	{
		return Task.Run(delegate
		{
			lock (_microphoneLock)
			{
				if (_recorder != null && (Status == RecordingStatus.Recording || Status == RecordingStatus.Paused))
				{
					try
					{
						_recorder.GetDynamicOptionsBuilder().SetDynamicAudioOptions(new DynamicAudioOptions
						{
							IsInputDeviceEnabled = enabled
						}).Apply();
						_microphoneEnabled = enabled;
						if (enabled)
						{
							StartLevelMonitor();
						}
						else
						{
							StopLevelMonitor();
						}
						return true;
					}
					catch
					{
						return false;
					}
				}
				return false;
			}
		});
	}

	private void ApplyMicrophoneState()
	{
		_recorder?.GetDynamicOptionsBuilder().SetDynamicAudioOptions(new DynamicAudioOptions
		{
			IsInputDeviceEnabled = _microphoneEnabled
		}).Apply();
	}

	private void StartLevelMonitor()
	{
		PrepareLevelMonitor();
		try
		{
			_levelMonitor?.Start();
		}
		catch
		{
			this.AudioLevelChanged?.Invoke(this, -60f);
		}
	}

	private void PrepareLevelMonitor()
	{
		if (_levelMonitor != null || string.IsNullOrWhiteSpace(_microphoneDevice))
		{
			return;
		}
		try
		{
			MMDevice device = new MMDeviceEnumerator().GetDevice(_microphoneDevice);
			_levelMonitor = new AudioLevelMonitor(device);
			_levelMonitor.LevelChanged += delegate(object? _, float level)
			{
				this.AudioLevelChanged?.Invoke(this, level);
			};
		}
		catch
		{
			this.AudioLevelChanged?.Invoke(this, -60f);
		}
	}

	private void StopLevelMonitor()
	{
		try
		{
			_levelMonitor?.Stop();
		}
		catch
		{
		}
		this.AudioLevelChanged?.Invoke(this, -60f);
	}

	private void RecorderStatusChanged(object? sender, RecordingStatusEventArgs e)
	{
		RecordingStatus status = e.Status switch
		{
			RecorderStatus.Paused => RecordingStatus.Paused, 
			RecorderStatus.Recording => RecordingStatus.Recording, 
			RecorderStatus.Finishing => RecordingStatus.Finalizing, 
			_ => Status, 
		};
		if (status == RecordingStatus.Recording && !_initialAudioConfigured)
		{
			lock (_microphoneLock)
			{
				if (!_initialAudioConfigured)
				{
					try
					{
						ApplyMicrophoneState();
						if (_microphoneEnabled)
						{
							StartLevelMonitor();
						}
					}
					catch
					{
						_microphoneEnabled = false;
						StopLevelMonitor();
					}
					_initialAudioConfigured = true;
				}
			}
		}
		SetStatus(status);
	}

	private void RecorderCompleted(object? sender, RecordingCompleteEventArgs e)
	{
		string path = e.FilePath;
		(Recorder? Recorder, AudioLevelMonitor? Monitor) resources = DetachResources();
		SetStatus(RecordingStatus.Idle);
		this.Completed?.Invoke(this, path);
		Task.Run(delegate
		{
			DisposeResources(resources);
		});
	}

	private void RecorderFailed(object? sender, RecordingFailedEventArgs e)
	{
		string error = (string.IsNullOrWhiteSpace(e.Error) ? Localization.Text("视频录制失败。") : e.Error);
		(Recorder? Recorder, AudioLevelMonitor? Monitor) resources = DetachResources();
		SetStatus(RecordingStatus.Idle);
		this.Failed?.Invoke(this, error);
		Task.Run(delegate
		{
			DisposeResources(resources);
		});
	}

	private void SetStatus(RecordingStatus status)
	{
		Status = status;
		this.StatusChanged?.Invoke(this, status);
	}

	private (Recorder? Recorder, AudioLevelMonitor? Monitor) DetachResources()
	{
		Recorder recorder = _recorder;
		AudioLevelMonitor monitor = _levelMonitor;
		_recorder = null;
		_levelMonitor = null;
		if (recorder != null)
		{
			recorder.OnStatusChanged -= RecorderStatusChanged;
			recorder.OnRecordingComplete -= RecorderCompleted;
			recorder.OnRecordingFailed -= RecorderFailed;
		}
		return (Recorder: recorder, Monitor: monitor);
	}

	private static void DisposeResources((Recorder? Recorder, AudioLevelMonitor? Monitor) resources)
	{
		try
		{
			resources.Monitor?.Dispose();
		}
		catch
		{
		}
		try
		{
			resources.Recorder?.Dispose();
		}
		catch
		{
		}
	}

	private void CleanupRecorder()
	{
		DisposeResources(DetachResources());
	}

	public void Dispose()
	{
		if (_recorder != null)
		{
			try
			{
				_recorder.Stop();
			}
			catch
			{
			}
		}
		CleanupRecorder();
	}
}
