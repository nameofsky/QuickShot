using System;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickShot;

public static class NativeWindow
{
	public struct POINT
	{
		public int X;

		public int Y;
	}

	public struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	public const uint GA_ROOT = 2u;

	public const uint GW_HWNDNEXT = 2u;

	private const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9u;

	public const int WM_HOTKEY = 786;

	public const uint WDA_EXCLUDEFROMCAPTURE = 17u;

	[DllImport("user32.dll")]
	public static extern nint WindowFromPoint(POINT point);

	[DllImport("user32.dll")]
	public static extern nint GetTopWindow(nint hwnd);

	[DllImport("user32.dll")]
	public static extern nint GetWindow(nint hwnd, uint command);

	[DllImport("user32.dll")]
	public static extern bool IsWindowVisible(nint hwnd);

	[DllImport("user32.dll")]
	public static extern nint GetAncestor(nint hwnd, uint flags);

	[DllImport("user32.dll")]
	public static extern bool GetWindowRect(nint hwnd, out RECT rect);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out RECT rect, int size);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetWindowText(nint hwnd, StringBuilder text, int maxCount);

	[DllImport("user32.dll")]
	public static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint vk);

	[DllImport("user32.dll")]
	public static extern bool UnregisterHotKey(nint hWnd, int id);

	[DllImport("user32.dll")]
	public static extern bool SetWindowDisplayAffinity(nint hWnd, uint affinity);

	public static string Title(nint hwnd)
	{
		StringBuilder text = new StringBuilder(512);
		GetWindowText(hwnd, text, text.Capacity);
		return text.ToString();
	}

	public static bool GetVisibleWindowRect(nint hwnd, out RECT rect)
	{
		if (DwmGetWindowAttribute(hwnd, 9u, out rect, Marshal.SizeOf<RECT>()) == 0)
		{
			return true;
		}
		return GetWindowRect(hwnd, out rect);
	}

	public static nint TopWindowAt(int x, int y, nint excluded, nint secondExcluded = 0)
	{
		for (nint hwnd = GetTopWindow(IntPtr.Zero); hwnd != IntPtr.Zero; hwnd = GetWindow(hwnd, 2u))
		{
			if (hwnd != excluded && hwnd != secondExcluded && IsWindowVisible(hwnd) && GetVisibleWindowRect(hwnd, out var rect) && x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom)
			{
				return hwnd;
			}
		}
		return IntPtr.Zero;
	}
}
