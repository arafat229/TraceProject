// Quick verification of OpenCvSharp4 FaceRecognizerSF API
// This file is temporary and can be deleted after verification

using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace TraceProject.ApiVerification;

public class OpenCvApiVerification
{
    public static void VerifyFaceRecognizerAPI()
    {
        // Expected API surface for FaceRecognizerSF:
        // 1. FaceRecognizerSF.Create(modelPath, configPath) or similar static factory
        // 2. Public methods: alignCrop, feature, match
        // 3. alignCrop(src_img): returns aligned Mat
        // 4. feature(aligned_img): returns embedding as Mat (128-dim typically)
        // 5. match(feature1, feature2, dis_type): returns similarity score

        Console.WriteLine("Checking OpenCvSharp4 version 4.9.0");

        // Try to find FaceRecognizerSF
        var dnnType = typeof(CvDnn);
        Console.WriteLine($"CvDnn type found: {dnnType.Name}");
        Console.WriteLine($"CvDnn methods: {string.Join(", ", dnnType.GetMethods().Select(m => m.Name).Distinct().Take(20))}");

        // Check if FaceRecognizerSF exists
        var faceRecognizerType = Type.GetType("OpenCvSharp.FaceRecognizerSF, OpenCvSharp");
        if (faceRecognizerType != null)
        {
            Console.WriteLine("✓ FaceRecognizerSF found in OpenCvSharp");
            Console.WriteLine($"Public methods: {string.Join(", ", faceRecognizerType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance).Select(m => m.Name).Distinct())}");
        }
        else
        {
            Console.WriteLine("✗ FaceRecognizerSF NOT found");
        }

        // Try Face namespace
        var faceNamespace = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.Contains("Face") == true)
            .Select(t => t.FullName)
            .Distinct()
            .ToList();

        Console.WriteLine($"Face-related types: {string.Join(", ", faceNamespace.Take(10))}");
    }
}
