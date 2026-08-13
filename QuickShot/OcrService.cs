using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;
using Tesseract;

namespace QuickShot;

public enum OcrStatus
{
	Success,
	NoRecognizer,
	NoText,
	Failed
}

public sealed record OcrRecognitionResult(
	OcrStatus Status,
	string Text,
	float Confidence = 0,
	string? ErrorMessage = null,
	string Engine = "",
	long ElapsedMilliseconds = 0);

public static class OcrService
{
	private static readonly string[] RequiredTesseractLanguages =
	{
		"chi_sim",
		"chi_tra",
		"eng",
		"rus",
		"fra",
		"spa"
	};

	private const string TesseractLanguages = "chi_sim+chi_tra+eng+rus+fra+spa";
	private const string CodeTesseractLanguage = "eng";
	private static readonly (string Incorrect, string Correct)[] CommonChinesePhraseCorrections =
	{
		("文件夫", "文件夹")
	};
	private static readonly object PaddleLock = new();
	private static readonly object TesseractLock = new();
	private static readonly Dictionary<string, TesseractEngine> TesseractEngines = new();

	private static PaddleOcrAll? _paddleOcr;
	private static PaddleOcrAll? _paddleCodeOcr;
	private static string? _paddleError;
	private static string? _paddleCodeError;
	private static string? _tesseractError;

	public static Task<OcrRecognitionResult> RecognizeAsync(Bitmap bitmap)
	{
		return Task.Run(() => Recognize(bitmap));
	}

	public static void WarmUp()
	{
		_ = Task.Run(() =>
		{
			try
			{
				PaddleOcrAll? ocr = GetPaddleOcr();
				if (ocr == null)
				{
					return;
				}

				using Mat image = new Mat(72, 480, MatType.CV_8UC3, Scalar.White);
				Cv2.PutText(image, "QuickShot OCR", new OpenCvSharp.Point(12, 48), HersheyFonts.HersheySimplex, 1.1, Scalar.Black, 2);
				lock (PaddleLock)
				{
					ocr.Run(image);
				}

			}
			catch
			{
				// Recognition will report the initialization error when it is actually invoked.
			}
		});
	}

	private static OcrRecognitionResult Recognize(Bitmap bitmap)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		if (LooksLikeDarkCodeImage(bitmap))
		{
			OcrRecognitionResult codeResult = RecognizeCodeWithTesseract(bitmap);
			if (codeResult.Status == OcrStatus.Success && LooksLikeCode(codeResult.Text))
			{
				return Complete(codeResult with { Engine = "Tesseract Code" }, stopwatch);
			}
		}

		OcrRecognitionResult paddleResult = RecognizeWithPaddle(bitmap);
		if (paddleResult.Status == OcrStatus.Success)
		{
			if (LooksLikeCode(paddleResult.Text))
			{
				OcrRecognitionResult codeResult = RecognizeCodeWithTesseract(bitmap);
				if (codeResult.Status == OcrStatus.Success)
				{
					return Complete(codeResult with { Engine = "Tesseract Code" }, stopwatch);
				}
			}
			return Complete(paddleResult with { Text = CorrectCommonChinesePhrases(paddleResult.Text) }, stopwatch);
		}

		OcrRecognitionResult tesseractResult = RecognizeWithTesseract(bitmap);
		if (tesseractResult.Status == OcrStatus.Success)
		{
			return Complete(tesseractResult with { Text = CorrectCommonChinesePhrases(tesseractResult.Text) }, stopwatch);
		}

		return Complete(paddleResult.Status == OcrStatus.NoText ? paddleResult : tesseractResult, stopwatch);
	}

	private static OcrRecognitionResult Complete(OcrRecognitionResult result, Stopwatch stopwatch)
	{
		return result with { ElapsedMilliseconds = stopwatch.ElapsedMilliseconds };
	}

	private static OcrRecognitionResult RecognizeWithPaddle(Bitmap bitmap)
	{
		try
		{
			PaddleOcrAll? ocr = GetPaddleOcr();
			if (ocr == null)
			{
				return new OcrRecognitionResult(OcrStatus.NoRecognizer, "", 0, _paddleError);
			}

			using MemoryStream png = new MemoryStream();
			bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
			using Mat image = Cv2.ImDecode(png.ToArray(), ImreadModes.Color);
			PaddleOcrResult result;
			lock (PaddleLock)
			{
				result = ocr.Run(image);
			}

			string text = NormalizeText(FormatPaddleRegions(result.Regions));
			float confidence = !result.Regions.Any()
				? 0
				: (float)result.Regions.Average(region => region.Score * 100f);
			return string.IsNullOrWhiteSpace(text)
				? new OcrRecognitionResult(OcrStatus.NoText, "", confidence)
				: new OcrRecognitionResult(OcrStatus.Success, text, confidence, Engine: "Paddle Chinese V5");
		}
		catch (Exception ex)
		{
			_paddleError = ex.Message;
			return new OcrRecognitionResult(OcrStatus.Failed, "", 0, ex.Message);
		}
	}

	private static bool LooksLikeDarkCodeImage(Bitmap bitmap)
	{
		int step = Math.Max(4, (int)Math.Sqrt((double)bitmap.Width * bitmap.Height / 14_000));
		int sampled = 0;
		int dark = 0;
		int coloredText = 0;
		for (int y = 0; y < bitmap.Height; y += step)
		{
			for (int x = 0; x < bitmap.Width; x += step)
			{
				Color pixel = bitmap.GetPixel(x, y);
				int brightest = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
				int darkest = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
				if ((pixel.R + pixel.G + pixel.B) / 3 < 70)
				{
					dark++;
				}
				else if (brightest - darkest > 45 && brightest > 110)
				{
					coloredText++;
				}
				sampled++;
			}
		}

		return sampled > 0 && dark * 100 >= sampled * 42 && coloredText * 1_000 >= sampled * 5;
	}

	private static PaddleOcrAll? GetPaddleOcr()
	{
		lock (PaddleLock)
		{
			if (_paddleOcr != null || _paddleError != null)
			{
				return _paddleOcr;
			}

			try
			{
				_paddleOcr = new PaddleOcrAll(LocalFullModels.ChineseV5, PaddleDevice.Mkldnn())
				{
					AllowRotateDetection = false,
					Enable180Classification = false
				};
				_paddleOcr.Detector.MaxSize = null;
				return _paddleOcr;
			}
			catch (Exception ex)
			{
				_paddleError = ex.Message;
				return null;
			}
		}
	}

	private static OcrRecognitionResult RecognizeCodeWithPaddle(Bitmap bitmap)
	{
		try
		{
			PaddleOcrAll? ocr = GetCodePaddleOcr();
			if (ocr == null)
			{
				return new OcrRecognitionResult(OcrStatus.NoRecognizer, "", 0, _paddleCodeError);
			}

			using MemoryStream png = new MemoryStream();
			bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
			using Mat image = Cv2.ImDecode(png.ToArray(), ImreadModes.Color);
			PaddleOcrResult result;
			lock (PaddleLock)
			{
				result = ocr.Run(image);
			}

			string text = NormalizeText(FormatPaddleRegions(result.Regions, codeMode: true));
			float confidence = !result.Regions.Any()
				? 0
				: (float)result.Regions.Average(region => region.Score * 100f);
			return string.IsNullOrWhiteSpace(text)
				? new OcrRecognitionResult(OcrStatus.NoText, "", confidence)
				: new OcrRecognitionResult(OcrStatus.Success, text, confidence, Engine: "Paddle English V5");
		}
		catch (Exception ex)
		{
			_paddleCodeError = ex.Message;
			return new OcrRecognitionResult(OcrStatus.Failed, "", 0, ex.Message);
		}
	}

	private static PaddleOcrAll? GetCodePaddleOcr()
	{
		lock (PaddleLock)
		{
			if (_paddleCodeOcr != null || _paddleCodeError != null)
			{
				return _paddleCodeOcr;
			}

			try
			{
				_paddleCodeOcr = new PaddleOcrAll(LocalFullModels.EnglishV5, PaddleDevice.Mkldnn())
				{
					AllowRotateDetection = false,
					Enable180Classification = false
				};
				_paddleCodeOcr.Detector.MaxSize = null;
				return _paddleCodeOcr;
			}
			catch (Exception ex)
			{
				_paddleCodeError = ex.Message;
				return null;
			}
		}
	}

	private static OcrRecognitionResult RecognizeWithTesseract(Bitmap bitmap)
	{
		try
		{
			if (!HasTesseractLanguageData(out string? dataError))
			{
				return new OcrRecognitionResult(OcrStatus.NoRecognizer, "", 0, dataError);
			}

			using Bitmap prepared = PrepareForTesseract(bitmap);
			using MemoryStream png = new MemoryStream();
			prepared.Save(png, System.Drawing.Imaging.ImageFormat.Png);
			using Pix image = Pix.LoadFromMemory(png.ToArray());
			TesseractEngine? engine = GetTesseractEngine();
			if (engine == null)
			{
				return new OcrRecognitionResult(OcrStatus.NoRecognizer, "", 0, _tesseractError);
			}

			lock (TesseractLock)
			{
				using Page page = engine.Process(image, PageSegMode.Auto);
				string text = NormalizeText(page.GetText());
				float confidence = page.GetMeanConfidence() * 100f;
				return string.IsNullOrWhiteSpace(text)
					? new OcrRecognitionResult(OcrStatus.NoText, "", confidence)
					: new OcrRecognitionResult(OcrStatus.Success, text, confidence, Engine: "Tesseract");
			}
		}
		catch (Exception ex)
		{
			return new OcrRecognitionResult(OcrStatus.Failed, "", 0, ex.Message);
		}
	}

	private static OcrRecognitionResult RecognizeCodeWithTesseract(Bitmap bitmap)
	{
		try
		{
			string dataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
			if (!File.Exists(Path.Combine(dataPath, CodeTesseractLanguage + ".traineddata")))
			{
				return new OcrRecognitionResult(OcrStatus.NoRecognizer, "", 0, "缺少离线 OCR 语言包：eng");
			}

			using Bitmap prepared = PrepareForCodeTesseract(bitmap);
			using MemoryStream png = new MemoryStream();
			prepared.Save(png, System.Drawing.Imaging.ImageFormat.Png);
			using Pix image = Pix.LoadFromMemory(png.ToArray());
			TesseractEngine? engine = GetCodeTesseractEngine();
			if (engine == null)
			{
				return new OcrRecognitionResult(OcrStatus.NoRecognizer, "", 0, _tesseractError);
			}

			lock (TesseractLock)
			{
				using Page page = engine.Process(image, PageSegMode.SingleBlock);
				string text = NormalizeText(FormatTesseractLines(page, bitmap, GetCodeTesseractScale(bitmap)));
				float confidence = page.GetMeanConfidence() * 100f;
				return string.IsNullOrWhiteSpace(text)
					? new OcrRecognitionResult(OcrStatus.NoText, "", confidence)
					: new OcrRecognitionResult(OcrStatus.Success, text, confidence);
			}
		}
		catch (Exception ex)
		{
			return new OcrRecognitionResult(OcrStatus.Failed, "", 0, ex.Message);
		}
	}

	private static bool HasTesseractLanguageData(out string? error)
	{
		lock (TesseractLock)
		{
			if (_tesseractError != null)
			{
				error = _tesseractError;
				return false;
			}

			string dataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
			string[] missing = RequiredTesseractLanguages
				.Where(language => !File.Exists(Path.Combine(dataPath, language + ".traineddata")))
				.ToArray();
			if (missing.Length == 0)
			{
				error = null;
				return true;
			}

			_tesseractError = "缺少离线 OCR 语言包：" + string.Join("、", missing);
			error = _tesseractError;
			return false;
		}
	}

	private static TesseractEngine? GetTesseractEngine()
	{
		lock (TesseractLock)
		{
			if (TesseractEngines.TryGetValue(TesseractLanguages, out TesseractEngine? existing))
			{
				return existing;
			}

			try
			{
				string dataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
				TesseractEngine engine = new TesseractEngine(dataPath, TesseractLanguages, EngineMode.LstmOnly)
				{
					DefaultPageSegMode = PageSegMode.Auto
				};
				TesseractEngines.Add(TesseractLanguages, engine);
				return engine;
			}
			catch (Exception ex)
			{
				_tesseractError = ex.Message;
				return null;
			}
		}
	}

	private static TesseractEngine? GetCodeTesseractEngine()
	{
		lock (TesseractLock)
		{
			if (TesseractEngines.TryGetValue(CodeTesseractLanguage, out TesseractEngine? existing))
			{
				return existing;
			}

			try
			{
				string dataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
				TesseractEngine engine = new TesseractEngine(dataPath, CodeTesseractLanguage, EngineMode.LstmOnly)
				{
					DefaultPageSegMode = PageSegMode.SingleBlock
				};
				TesseractEngines.Add(CodeTesseractLanguage, engine);
				return engine;
			}
			catch (Exception ex)
			{
				_tesseractError = ex.Message;
				return null;
			}
		}
	}

	private static string NormalizeText(string? text)
	{
		string normalized = Regex.Replace(text?.Trim() ?? "", "(?<=[\\u3400-\\u9fff])[ \\t]+(?=[\\u3400-\\u9fff])", "")
			.Replace('（', '(')
			.Replace('）', ')');
		string[] lines = normalized.Split('\n');
		for (int index = 0; index < lines.Length; index++)
		{
			if (IsTechnicalLine(lines[index]))
			{
				lines[index] = lines[index]
					.Replace('\u201c', '\"')
					.Replace('\u201d', '\"')
					.Replace('\u2018', '\'')
					.Replace('\u2019', '\'');
				lines[index] = Regex.Replace(lines[index], "(?<=[A-Za-z0-9])\"{2,}", "\"");
				lines[index] = Regex.Replace(lines[index], "\\[model_providers\\s*\\.\\s*([A-Za-z0-9_-]+)\\]", "[model_providers.$1]");
				lines[index] = Regex.Replace(lines[index], "^(\\s*(?:model_context_window|model_auto_compact_token_limit)\\s*=\\s*)([0-9eE]+)\\s*$", match =>
					match.Groups[1].Value + match.Groups[2].Value.Replace('e', '0').Replace('E', '0'));
			}
		}
		string combined = string.Join("\n", lines);
		combined = Regex.Replace(combined, "(?m)^\\s*\\[model_providers\\s*\\.\\s*([A-Za-z0-9_-]+)\\]", "[model_providers.$1]");
		combined = Regex.Replace(combined, "(?m)^(\\s*wire_api\\s*=\\s*\")responses['\"]\\s*$", "$1responses\"");
		return combined;
	}

	private static string CorrectCommonChinesePhrases(string text)
	{
		if (string.IsNullOrEmpty(text) || LooksLikeCode(text))
		{
			return text;
		}

		string[] lines = text.Split('\n');
		for (int index = 0; index < lines.Length; index++)
		{
			if (IsProtectedFromPhraseCorrection(lines[index]))
			{
				continue;
			}

			foreach ((string incorrect, string correct) in CommonChinesePhraseCorrections)
			{
				lines[index] = lines[index].Replace(incorrect, correct, StringComparison.Ordinal);
			}
		}

		return string.Join("\n", lines);
	}

	private static bool IsProtectedFromPhraseCorrection(string line)
	{
		string trimmed = line.TrimStart();
		return IsTechnicalLine(line) ||
			line.Contains('\\') ||
			line.Contains('/') ||
			trimmed.StartsWith('#') ||
			trimmed.StartsWith("//", StringComparison.Ordinal) ||
			trimmed.StartsWith("/*", StringComparison.Ordinal) ||
			Regex.IsMatch(line, "(?i)\\b(?:https?|file)://|\\b(?:[a-z]:|res:|user:)\\S*") ||
			Regex.IsMatch(line, "[{};]|=>|:=|\\$[A-Za-z_]");
	}

	private static string FormatPaddleRegions(IEnumerable<PaddleOcrResultRegion> regions, bool codeMode = false)
	{
		if (codeMode)
		{
			return FormatCodePaddleRegions(regions);
		}

		PaddleOcrResultRegion[] ordered = regions
			.Where(region => !string.IsNullOrWhiteSpace(region.Text))
			.OrderBy(region => region.Rect.Center.Y)
			.ThenBy(region => region.Rect.Center.X)
			.ToArray();
		if (ordered.Length == 0)
		{
			return "";
		}

		List<List<PaddleOcrResultRegion>> lines = new();
		List<double> lineCenters = new();
		List<double> lineHeights = new();
		foreach (PaddleOcrResultRegion region in ordered)
		{
			double centerY = region.Rect.Center.Y;
			// Paddle returns a rotated rectangle; for a horizontal text line the long
			// side is the line width, so the short side is the actual glyph height.
			double height = Math.Max(1, Math.Min(region.Rect.Size.Width, region.Rect.Size.Height));
			if (lines.Count == 0 || Math.Abs(centerY - lineCenters[^1]) > Math.Max(height, lineHeights[^1]) * 0.7)
			{
				lines.Add(new List<PaddleOcrResultRegion>());
				lineCenters.Add(centerY);
				lineHeights.Add(height);
			}
			lines[^1].Add(region);
			int lastLine = lines.Count - 1;
			lineCenters[lastLine] = lines[lastLine].Average(item => item.Rect.Center.Y);
			lineHeights[lastLine] = Math.Max(lineHeights[lastLine], height);
		}

		double medianHeight = Median(lineHeights);
		double minimumLeft = ordered.Min(region => region.Rect.BoundingRect().Left);
		double characterWidth = Math.Max(1, medianHeight * 0.6);
		StringBuilder output = new StringBuilder();
		for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
		{
			if (lineIndex > 0)
			{
				double gap = lineCenters[lineIndex] - lineCenters[lineIndex - 1];
				int blankLines = Math.Max(0, (int)Math.Round(gap / Math.Max(1, medianHeight * 1.7)) - 1);
				output.Append('\n', blankLines + 1);
			}

			List<PaddleOcrResultRegion> line = lines[lineIndex]
				.OrderBy(region => region.Rect.BoundingRect().Left)
				.ToList();
			int indentation = Math.Max(0, (int)Math.Round((line.Min(region => region.Rect.BoundingRect().Left) - minimumLeft) / characterWidth));
			output.Append(' ', indentation);
			for (int regionIndex = 0; regionIndex < line.Count; regionIndex++)
			{
				if (regionIndex > 0)
				{
					int previousRight = line[regionIndex - 1].Rect.BoundingRect().Right;
					int currentLeft = line[regionIndex].Rect.BoundingRect().Left;
					bool hasVisibleGap = currentLeft - previousRight > medianHeight * 0.35;
					if (hasVisibleGap)
					{
						output.Append(' ');
					}
				}
				output.Append(line[regionIndex].Text.Trim());
			}
		}
		return output.ToString();
	}

	private static string FormatCodePaddleRegions(IEnumerable<PaddleOcrResultRegion> regions)
	{
		List<CodeRegion> ordered = regions
			.Where(region => !string.IsNullOrWhiteSpace(region.Text))
			.Select(region => new CodeRegion(region.Text.Trim(), region.Rect.BoundingRect(), region.Rect.Center.Y,
				Math.Max(1, Math.Min(region.Rect.Size.Width, region.Rect.Size.Height))))
			.OrderBy(region => region.CenterY)
			.ThenBy(region => region.Bounds.Left)
			.ToList();
		if (ordered.Count == 0)
		{
			return "";
		}

		List<CodeLine> lines = new();
		foreach (CodeRegion region in ordered)
		{
			if (lines.Count == 0 || Math.Abs(region.CenterY - lines[^1].CenterY) > Math.Max(region.Height, lines[^1].Height) * 0.65)
			{
				lines.Add(new CodeLine(region.CenterY, region.Height));
			}

			CodeLine line = lines[^1];
			line.Regions.Add(region);
			line.CenterY = line.Regions.Average(item => item.CenterY);
			line.Height = Math.Max(line.Height, region.Height);
		}

		bool hasLineNumberColumn = lines.Count(line => line.Regions.Count >= 2 &&
			Regex.IsMatch(line.Regions[0].Text, "^\\d+$") &&
			line.Regions[1].Bounds.Left - line.Regions[0].Bounds.Right > line.Height) >= Math.Max(2, lines.Count / 2);
		foreach (CodeLine line in lines)
		{
			line.Regions.Sort((left, right) => left.Bounds.Left.CompareTo(right.Bounds.Left));
			if (hasLineNumberColumn && line.Regions.Count >= 2 && Regex.IsMatch(line.Regions[0].Text, "^\\d+$"))
			{
				line.Regions.RemoveAt(0);
			}
			while (line.Regions.Count > 0 && IsEditorIndentMarker(line.Regions[0].Text))
			{
				line.Regions.RemoveAt(0);
			}
		}

		List<CodeLine> textLines = lines.Where(line => line.Regions.Count > 0).ToList();
		if (textLines.Count == 0)
		{
			return "";
		}

		double medianHeight = Median(textLines.Select(line => line.Height));
		int minimumLeft = textLines.Min(line => line.Regions[0].Bounds.Left);
		double indentWidth = EstimateIndentWidth(textLines, minimumLeft, medianHeight);
		double lineStride = EstimateLineStride(lines, medianHeight);
		StringBuilder output = new();
		double? previousTextCenter = null;
		foreach (CodeLine line in lines)
		{
			if (line.Regions.Count == 0)
			{
				continue;
			}

			if (previousTextCenter.HasValue)
			{
				output.Append('\n', Math.Max(1, (int)Math.Round((line.CenterY - previousTextCenter.Value) / lineStride)));
			}

			int indentation = indentWidth <= 1
				? 0
				: Math.Max(0, (int)Math.Round((line.Regions[0].Bounds.Left - minimumLeft) / indentWidth));
			output.Append('\t', indentation);
			AppendCodeLine(output, line.Regions, medianHeight);
			previousTextCenter = line.CenterY;
		}
		return NormalizeCodeSyntax(output.ToString());
	}

	private static bool IsEditorIndentMarker(string text)
	{
		return text.Trim() is ">" or "|" or ">|" or "|>" or "»";
	}

	private static double EstimateIndentWidth(IEnumerable<CodeLine> lines, int minimumLeft, double medianHeight)
	{
		double[] offsets = lines.Select(line => (double)(line.Regions[0].Bounds.Left - minimumLeft))
			.Where(offset => offset >= medianHeight)
			.OrderBy(offset => offset)
			.ToArray();
		return offsets.Length == 0 ? 0 : offsets[0];
	}

	private static double EstimateLineStride(IEnumerable<CodeLine> lines, double medianHeight)
	{
		CodeLine[] ordered = lines.OrderBy(line => line.CenterY).ToArray();
		double[] gaps = ordered.Skip(1).Select((line, index) => line.CenterY - ordered[index].CenterY)
			.Where(gap => gap >= medianHeight * 0.5)
			.OrderBy(gap => gap)
			.ToArray();
		return gaps.Length == 0 ? Math.Max(1, medianHeight * 1.6) : Median(gaps.Take(Math.Max(1, gaps.Length / 2)));
	}

	private static void AppendCodeLine(StringBuilder output, IReadOnlyList<CodeRegion> regions, double medianHeight)
	{
		double characterWidth = Median(regions.Select(region => Math.Max(1,
			(double)region.Bounds.Width / Math.Max(1, region.Text.Count(character => !char.IsWhiteSpace(character))))));
		characterWidth = Math.Clamp(characterWidth, Math.Max(1, medianHeight * 0.3), Math.Max(1, medianHeight));
		for (int index = 0; index < regions.Count; index++)
		{
			if (index > 0 && regions[index].Bounds.Left - regions[index - 1].Bounds.Right >= characterWidth * 0.42)
			{
				output.Append(' ');
			}
			output.Append(NormalizeCodeKeyword(regions[index].Text));
		}
	}

	private static string NormalizeCodeKeyword(string text)
	{
		return Regex.Replace(text, "(?<![A-Za-z0-9_])(?i:extends|class|const|var|func|if|elif|else|for|while|return|true|false|null|preload|onready)(?![A-Za-z0-9_])", match => match.Value.ToLowerInvariant());
	}

	private static string NormalizeCodeSyntax(string text)
	{
		string normalized = Regex.Replace(text, "\\s*-\\s*>\\s*", " -> ");
		normalized = Regex.Replace(normalized, "\\s*:\\s*=\\s*", " := ");
		normalized = Regex.Replace(normalized, "\\s*!\\s*=\\s*", " != ");
		normalized = Regex.Replace(normalized, "(?<![!<>=])\\s*=\\s*=\\s*(?![=])", " == ");
		return normalized;
	}

	private sealed class CodeLine(double centerY, double height)
	{
		public List<CodeRegion> Regions { get; } = new();
		public double CenterY { get; set; } = centerY;
		public double Height { get; set; } = height;
	}

	private readonly record struct CodeRegion(string Text, OpenCvSharp.Rect Bounds, double CenterY, double Height);

	private static double Median(IEnumerable<double> values)
	{
		double[] ordered = values.OrderBy(value => value).ToArray();
		if (ordered.Length == 0)
		{
			return 1;
		}
		int middle = ordered.Length / 2;
		return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2 : ordered[middle];
	}

	private static string FormatTesseractLines(Page page, Bitmap source, double scale)
	{
		List<TesseractCodeLine> lines = new();
		using ResultIterator iterator = page.GetIterator();
		iterator.Begin();
		do
		{
			string text = iterator.GetText(PageIteratorLevel.TextLine)?.Trim() ?? "";
			if (!string.IsNullOrWhiteSpace(text) && iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out Tesseract.Rect bounds))
			{
				lines.Add(new TesseractCodeLine(text, bounds.X1, bounds.Y1, Math.Max(1, bounds.Height)));
			}
		}
		while (iterator.Next(PageIteratorLevel.TextLine));

		if (lines.Count == 0)
		{
			return "";
		}

		List<(string Text, int Left, int CenterY)> words = new();
		using ResultIterator wordIterator = page.GetIterator();
		wordIterator.Begin();
		do
		{
			string text = wordIterator.GetText(PageIteratorLevel.Word)?.Trim() ?? "";
			if (!string.IsNullOrWhiteSpace(text) && wordIterator.TryGetBoundingBox(PageIteratorLevel.Word, out Tesseract.Rect bounds))
			{
				words.Add((text, bounds.X1, (bounds.Y1 + bounds.Y2) / 2));
			}
		}
		while (wordIterator.Next(PageIteratorLevel.Word));

		foreach (TesseractCodeLine line in lines)
		{
			line.FirstCodeLeft = words.Where(word => Math.Abs(word.CenterY - (line.Top + line.Height / 2)) <= line.Height / 2 &&
				!Regex.IsMatch(word.Text, "^(?:[Ss]?\\d+|[>|/Ilv]+)$"))
				.Select(word => (int?)word.Left)
				.Min();
		}

		foreach (TesseractCodeLine line in lines)
		{
			line.CleanText = CleanTesseractCodeLine(line.Text);
		}
		foreach ((TesseractCodeLine Line, string Text) comment in RecognizeChineseComments(source, lines.Where(line => line.CleanText.StartsWith('#')).ToArray(), scale))
		{
			comment.Line.CleanText = comment.Text;
		}

		List<TesseractCodeLine> codeLines = lines.Where(line => line.FirstCodeLeft.HasValue).ToList();
		int minimumLeft = codeLines.Count == 0 ? 0 : codeLines.Min(line => line.FirstCodeLeft!.Value);
		double medianHeight = Median(lines.Select(line => (double)line.Height));
		double indentWidth = EstimateTesseractIndentWidth(codeLines, minimumLeft, medianHeight);
		StringBuilder output = new();
		foreach (TesseractCodeLine line in lines.OrderBy(line => line.Top).ThenBy(line => line.Left))
		{
			if (output.Length > 0)
			{
				output.Append('\n');
			}
			if (line.FirstCodeLeft.HasValue && indentWidth > 1)
			{
				output.Append('\t', Math.Max(0, (int)Math.Round((line.FirstCodeLeft.Value - minimumLeft) / indentWidth)));
			}
			output.Append(line.CleanText);
		}
		return RestoreCodeIndentation(NormalizeCodeSyntax(output.ToString()));
	}

	private static double EstimateTesseractIndentWidth(IEnumerable<TesseractCodeLine> lines, int minimumLeft, double medianHeight)
	{
		double[] offsets = lines.Where(line => line.FirstCodeLeft.HasValue)
			.Select(line => (double)(line.FirstCodeLeft!.Value - minimumLeft))
			.Where(offset => offset >= medianHeight)
			.OrderBy(offset => offset)
			.ToArray();
		return offsets.Length == 0 ? 0 : offsets[0];
	}

	private static string CleanTesseractCodeLine(string text)
	{
		string clean = Regex.Replace(text.Trim(), "^(?:[Ss]?\\d+)(?:\\s+|$)", "");
		clean = Regex.Replace(clean, "^(?:[>|/Il~]+\\s+)+", "");
		clean = Regex.Replace(clean, "^(?:[vV][oO]|[vV])\\s+(?=(?:if|elif|else|for|while)\\b)", "");
		clean = Regex.Replace(clean, "\\)\\s*>\\s*(?=void:)", ") -> ");
		return Regex.IsMatch(clean, "^[Jj]\\.") ? "" : clean;
	}

	private static IEnumerable<(TesseractCodeLine Line, string Text)> RecognizeChineseComments(Bitmap source, IReadOnlyList<TesseractCodeLine> lines, double scale)
	{
		List<(TesseractCodeLine Line, Rectangle Bounds)> crops = new();
		foreach (TesseractCodeLine line in lines)
		{
			if (!line.FirstCodeLeft.HasValue)
			{
				continue;
			}

			int left = Math.Max(0, (int)Math.Floor((line.FirstCodeLeft.Value - line.Height * 0.8) / scale));
			int top = Math.Max(0, (int)Math.Floor((line.Top - line.Height * 0.25) / scale));
			int height = Math.Min(source.Height - top, Math.Max(1, (int)Math.Ceiling(line.Height * 1.5 / scale)));
			if (height >= 4 && left < source.Width)
			{
				crops.Add((line, new Rectangle(left, top, source.Width - left, height)));
			}
		}

		if (crops.Count == 0)
		{
			return Array.Empty<(TesseractCodeLine Line, string Text)>();
		}

		try
		{
			const int rowGap = 12;
			int width = crops.Max(item => item.Bounds.Width);
			int height = crops.Sum(item => item.Bounds.Height + rowGap);
			using Bitmap combined = new Bitmap(width, height, PixelFormat.Format24bppRgb);
			using (Graphics graphics = Graphics.FromImage(combined))
			{
				graphics.Clear(Color.FromArgb(18, 18, 18));
				int y = 0;
				foreach (var item in crops)
				{
					graphics.DrawImage(source, new Rectangle(0, y, item.Bounds.Width, item.Bounds.Height), item.Bounds, GraphicsUnit.Pixel);
					y += item.Bounds.Height + rowGap;
				}
			}

			PaddleOcrAll? ocr = GetPaddleOcr();
			if (ocr == null)
			{
				return Array.Empty<(TesseractCodeLine Line, string Text)>();
			}
			using MemoryStream png = new MemoryStream();
			combined.Save(png, System.Drawing.Imaging.ImageFormat.Png);
			using Mat image = Cv2.ImDecode(png.ToArray(), ImreadModes.Color);
			PaddleOcrResult result;
			lock (PaddleLock)
			{
				result = ocr.Run(image);
			}
			string[] recognized = FormatPaddleRegions(result.Regions).Split('\n', StringSplitOptions.RemoveEmptyEntries);
			if (recognized.Length != crops.Count)
			{
				return Array.Empty<(TesseractCodeLine Line, string Text)>();
			}

			List<(TesseractCodeLine Line, string Text)> replacements = new();
			for (int index = 0; index < crops.Count; index++)
			{
				string comment = recognized[index].Trim().TrimStart('#', ' ', '>', '|', '~', '\u300c', '\u300e', '\u3010');
				int cjkCharacters = comment.Count(character => character is >= '\u3400' and <= '\u9fff');
				if (cjkCharacters >= 2)
				{
					replacements.Add((crops[index].Line, "# " + comment));
				}
			}
			return replacements;
		}
		catch
		{
			return Array.Empty<(TesseractCodeLine Line, string Text)>();
		}
	}

	private static string RestoreCodeIndentation(string text)
	{
		StringBuilder output = new();
		int indentation = 0;
		bool previousLineWasBranch = false;
		foreach (string rawLine in text.Split('\n'))
		{
			int observedIndentation = rawLine.TakeWhile(character => character == '\t').Count();
			string line = rawLine.Trim();
			if (line.Length == 0)
			{
				output.AppendLine();
				continue;
			}

			bool rootDeclaration = Regex.IsMatch(line, "^(?:func|def|class|extends|const|@onready)\\b", RegexOptions.IgnoreCase);
			bool branch = Regex.IsMatch(line, "^(?:elif|else|except|finally)\\b", RegexOptions.IgnoreCase);
			if (rootDeclaration)
			{
				indentation = 0;
			}
			else if (branch)
			{
				indentation = Math.Max(0, indentation - 1);
			}
			else if (!previousLineWasBranch && observedIndentation > 0 && observedIndentation < indentation)
			{
				indentation = observedIndentation;
			}

			output.Append('\t', indentation);
			output.AppendLine(line);
			if (line.EndsWith(':'))
			{
				indentation++;
			}
			previousLineWasBranch = branch;
		}
		return output.ToString().TrimEnd();
	}

	private sealed class TesseractCodeLine(string text, int left, int top, int height)
	{
		public string Text { get; } = text;
		public int Left { get; } = left;
		public int Top { get; } = top;
		public int Height { get; } = height;
		public int? FirstCodeLeft { get; set; }
		public string CleanText { get; set; } = "";
	}

	private static bool LooksLikeCode(string text)
	{
		int printableAscii = text.Count(character => character is >= ' ' and <= '~');
		int markers = Regex.Matches(text, "(?i)(?:extends|class|const|var|let|def|func|import|from|using)[A-Za-z_]*|:=|=>|\\$[A-Za-z_]|(?:get_vector|move_and_slide|vector2|onready)").Count;
		return printableAscii * 100 >= Math.Max(1, text.Length) * 30 && markers >= 3;
	}

	private static bool IsTechnicalLine(string line)
	{
		return line.Contains('=') || line.Contains("://", StringComparison.Ordinal) ||
			(line.StartsWith('[') && line.EndsWith(']'));
	}

	private static Bitmap PrepareForTesseract(Bitmap source)
	{
		const int targetPixels = 4_500_000;
		double requestedScale = Math.Max(source.Width, source.Height) < 1400 ? 3d : 1d;
		double pixelScale = Math.Sqrt((double)targetPixels / Math.Max(1, source.Width * source.Height));
		double scale = Math.Min(requestedScale, pixelScale);
		int width = Math.Max(1, (int)Math.Round(source.Width * scale));
		int height = Math.Max(1, (int)Math.Round(source.Height * scale));

		Bitmap output = new Bitmap(width, height, PixelFormat.Format24bppRgb);
		using Graphics graphics = Graphics.FromImage(output);
		graphics.Clear(Color.White);
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.DrawImage(source, new Rectangle(0, 0, width, height));
		return output;
	}

	private static Bitmap PrepareForCodeTesseract(Bitmap source)
	{
		double scale = GetCodeTesseractScale(source);
		int width = Math.Max(1, (int)Math.Round(source.Width * scale));
		int height = Math.Max(1, (int)Math.Round(source.Height * scale));

		using Bitmap scaled = new Bitmap(width, height, PixelFormat.Format24bppRgb);
		using (Graphics graphics = Graphics.FromImage(scaled))
		{
			graphics.Clear(Color.White);
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.DrawImage(source, new Rectangle(0, 0, width, height));
		}

		Bitmap output = new Bitmap(width, height, PixelFormat.Format24bppRgb);
		Rectangle rectangle = new Rectangle(0, 0, width, height);
		BitmapData sourceData = scaled.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
		BitmapData outputData = output.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
		try
		{
			unsafe
			{
				for (int y = 0; y < height; y++)
				{
					byte* sourceRow = (byte*)sourceData.Scan0 + y * sourceData.Stride;
					byte* outputRow = (byte*)outputData.Scan0 + y * outputData.Stride;
					for (int x = 0; x < width; x++)
					{
						int offset = x * 3;
						byte brightest = Math.Max(sourceRow[offset], Math.Max(sourceRow[offset + 1], sourceRow[offset + 2]));
						byte value = brightest >= 92 ? (byte)0 : (byte)255;
						outputRow[offset] = value;
						outputRow[offset + 1] = value;
						outputRow[offset + 2] = value;
					}
				}
			}
		}
		finally
		{
			scaled.UnlockBits(sourceData);
			output.UnlockBits(outputData);
		}
		return output;
	}

	private static double GetCodeTesseractScale(Bitmap source)
	{
		const int targetPixels = 8_000_000;
		double requestedScale = Math.Max(source.Width, source.Height) < 1800 ? 3d : 1d;
		double pixelScale = Math.Sqrt((double)targetPixels / Math.Max(1, source.Width * source.Height));
		return Math.Min(requestedScale, pixelScale);
	}
}
