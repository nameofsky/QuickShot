using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace QuickShot;

public sealed class AudioLevelMonitor : IDisposable
{
	private readonly WasapiCapture _capture;

	private bool _running;

	public event EventHandler<float>? LevelChanged;

	public AudioLevelMonitor(MMDevice device)
	{
		_capture = new WasapiCapture(device);
		_capture.DataAvailable += OnData;
	}

	public void Start()
	{
		if (!_running)
		{
			_capture.StartRecording();
			_running = true;
		}
	}

	public void Stop()
	{
		if (_running)
		{
			_capture.StopRecording();
			_running = false;
		}
	}

	private void OnData(object? sender, WaveInEventArgs e)
	{
		if (!_running || e.BytesRecorded == 0)
		{
			return;
		}
		WaveFormat format = _capture.WaveFormat;
		double sum = 0.0;
		int samples = e.BytesRecorded / format.BlockAlign;
		for (int i = 0; i < samples; i++)
		{
			for (int channel = 0; channel < format.Channels; channel++)
			{
				int offset = i * format.BlockAlign + channel * (format.BitsPerSample / 8);
				float sample = ((format.BitsPerSample == 32) ? BitConverter.ToSingle(e.Buffer, offset) : ((float)BitConverter.ToInt16(e.Buffer, offset) / 32768f));
				sum += (double)(sample * sample);
			}
		}
		double rms = Math.Sqrt(sum / (double)Math.Max(1, samples * format.Channels));
		double db = ((rms <= 1E-05) ? (-60.0) : (20.0 * Math.Log10(rms)));
		this.LevelChanged?.Invoke(this, (float)Math.Clamp(db, -60.0, 0.0));
	}

	public void Dispose()
	{
		try
		{
			Stop();
		}
		catch
		{
		}
		_capture.Dispose();
	}
}
