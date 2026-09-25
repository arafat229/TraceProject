using System.Runtime.InteropServices;
using OpenCvSharp;
using TraceProject.Services.Interfaces;

namespace TraceProject.Services.VideoSources;

/// <summary>
/// An IVideoFrame wrapping a single still image (e.g. an uploaded photo), as packed BGR.
/// </summary>
public sealed class StillImageFrame : IVideoFrame
{
    public required byte[] Data { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string PixelFormat => "BGR";
    public int Pitch => Width * 3;

    /// <summary>
    /// Decodes an encoded image (JPEG, PNG, …). Returns null if the bytes are not an image.
    /// </summary>
    public static StillImageFrame? FromEncodedImage(byte[] imageBytes)
    {
        if (imageBytes.Length == 0)
            return null;

        using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (mat.Empty())
            return null;

        using var continuous = mat.Clone();
        var data = new byte[continuous.Width * continuous.Height * 3];
        Marshal.Copy(continuous.Data, data, 0, data.Length);
        return new StillImageFrame { Data = data, Width = continuous.Width, Height = continuous.Height };
    }
}
