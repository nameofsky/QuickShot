using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace QuickShot;

public class App : Application
{
	private const string AppUserModelId = "QuickShot.Desktop";

	private const string RestoreEventName = "QuickShot.RestoreEvent.v41";

	private MainWindow? _mainWindow;

	private Mutex? _instanceMutex;
	private EventWaitHandle? _restoreEvent;
	private Thread? _restoreThread;
	private volatile bool _shuttingDown;

	private bool _contentLoaded;

	protected override void OnStartup(StartupEventArgs e)
	{
		SetCurrentProcessExplicitAppUserModelID("QuickShot.Desktop");
		base.OnStartup(e);
		_instanceMutex = new Mutex(initiallyOwned: true, "QuickShot.SingleInstance", out var createdNew);
		if (!createdNew)
		{
			SignalExistingWindow();
			Shutdown();
			return;
		}

		_restoreEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RestoreEventName);
		_restoreThread = new Thread(WaitForRestoreSignal) { IsBackground = true, Name = "QuickShot restore listener" };
		_restoreThread.Start();
		_mainWindow = new MainWindow { Title = "QuickShot" };
		_mainWindow.Show();
		OcrService.WarmUp();
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

	private static void SignalExistingWindow()
	{
		if (EventWaitHandle.TryOpenExisting(RestoreEventName, out EventWaitHandle? restoreEvent))
		{
			using (restoreEvent)
			{
				restoreEvent.Set();
			}
		}
	}

	private void WaitForRestoreSignal()
	{
		while (_restoreEvent?.WaitOne() == true)
		{
			if (_shuttingDown)
			{
				return;
			}

			Dispatcher.BeginInvoke(() => _mainWindow?.RestoreFromSecondInstance());
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_shuttingDown = true;
		_restoreEvent?.Set();
		_restoreEvent?.Dispose();
		_instanceMutex?.Dispose();
		base.OnExit(e);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/QuickShot;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocater);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.4.0")]
	public static void Main()
	{
		try
		{
			App app = new App();
			app.InitializeComponent();
			app.Run();
		}
		catch (Exception ex)
		{
			string logPath = Path.Combine(AppContext.BaseDirectory, "QuickShot-startup-error.log");
			try
			{
				File.WriteAllText(logPath, ex.ToString());
			}
			catch
			{
			}

			MessageBox.Show($"QuickShot 启动失败。\n\n错误日志：{logPath}", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}
}
