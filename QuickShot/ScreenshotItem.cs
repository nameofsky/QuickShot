using System.Windows.Media;

namespace QuickShot;

public sealed record ScreenshotItem(string FilePath, string Day, string FileName, ImageSource? Thumbnail);
