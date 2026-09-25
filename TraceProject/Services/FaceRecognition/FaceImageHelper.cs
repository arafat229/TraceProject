using System.Runtime.InteropServices;
using OpenCvSharp;

namespace TraceProject.Services.FaceRecognition;

/// <summary>
/// Converts between aligned face pixels (raw packed BGR, as in DetectedFace.FaceImageData)
/// and JPEG bytes stored in the database for display.
/// </summary>
public static class FaceImageHelper
{
    public static byte[] EncodeJpeg(byte[] bgr, int width, int height, int quality = 90)
    {
        using var mat = Mat.FromPixelData(height, width, MatType.CV_8UC3, bgr);
        Cv2.ImEncode(".jpg", mat, out var jpeg, new ImageEncodingParam(ImwriteFlags.JpegQuality, quality));
        return jpeg;
    }

    /// <summary>
    /// Decodes a stored face JPEG back to raw BGR pixels. Returns null if it isn't a decodable image.
    /// </summary>
    public static (byte[] Bgr, int Width, int Height)? DecodeToBgr(byte[] imageBytes)
    {
        if (imageBytes.Length == 0)
            return null;

        using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (mat.Empty())
            return null;

        using var continuous = mat.Clone(); // Clone guarantees continuous memory
        var bytes = new byte[continuous.Width * continuous.Height * 3];
        Marshal.Copy(continuous.Data, bytes, 0, bytes.Length);
        return (bytes, continuous.Width, continuous.Height);
    }
}
