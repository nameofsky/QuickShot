using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace QuickShot;

public static class BitmapInterop
{
	public static BitmapSource ToSource(Bitmap bitmap)
	{
		nint handle = bitmap.GetHbitmap();
		try
		{
			return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bitmap.Width, bitmap.Height));
		}
		finally
		{
			DeleteObject(handle);
		}
	}

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(nint handle);
}
