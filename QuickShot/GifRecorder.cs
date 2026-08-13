using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.Serialization;

namespace QuickShot;

internal static class GifRecorder
{
    public static void Save(IReadOnlyList<Bitmap> frames, string path, int framesPerSecond)
    {
        if (frames.Count == 0)
        {
            throw new InvalidOperationException("没有可保存的 GIF 帧。");
        }

        int delay = Math.Max(1, (int)Math.Round(100.0 / Math.Clamp(framesPerSecond, 1, 25)));
        foreach (Bitmap frame in frames)
        {
            SetFrameDelay(frame, delay);
        }
        SetLoop(frames[0]);

        ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Gif.Guid);
        using EncoderParameters parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        frames[0].Save(path, encoder, parameters);

        for (int i = 1; i < frames.Count; i++)
        {
            parameters.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
            frames[0].SaveAdd(frames[i], parameters);
        }

        parameters.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
        frames[0].SaveAdd(parameters);
    }

    private static void SetFrameDelay(Image image, int delay)
    {
        image.SetPropertyItem(CreatePropertyItem(0x5100, 4, BitConverter.GetBytes(delay)));
    }

    private static void SetLoop(Image image)
    {
        image.SetPropertyItem(CreatePropertyItem(0x5101, 3, BitConverter.GetBytes((ushort)0)));
    }

    private static PropertyItem CreatePropertyItem(int id, short type, byte[] value)
    {
#pragma warning disable SYSLIB0050
        PropertyItem item = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
#pragma warning restore SYSLIB0050
        item.Id = id;
        item.Type = type;
        item.Len = value.Length;
        item.Value = value;
        return item;
    }
}