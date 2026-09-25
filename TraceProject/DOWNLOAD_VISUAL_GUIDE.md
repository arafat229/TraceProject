# 📥 YuNet Model Download - VISUAL GUIDE

## Your Setup (Already Done ✓)

```
C:\Users\ALIENWARE\source\repos\TraceProject\
└── TraceProject\
	├── Models\
	│   └── FaceDetection\  ← Folder is ready! 
	│       └── [EMPTY - You need to put the file here]
	├── Services\
	├── Program.cs
	└── appsettings.json
```

---

## The File You Need

**Name:** `face_detection_yunet_1_3_0_416_416_simplify.onnx`  
**Size:** ~340 KB  
**Source:** OpenCV Zoo Repository  
**Type:** ONNX deep learning model for face detection

---

## Option 1️⃣: Browser Download (EASIEST)

```
1. Open browser
		 ↓
2. Go to: https://github.com/opencv/opencv_zoo/tree/master/models/face_detection_yunet
		 ↓
3. Find: face_detection_yunet_1_3_0_416_416_simplify.onnx
		 ↓
4. Right-click → "Save link as..."
		 ↓
5. Save to: C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection
		 ↓
6. Done! ✓
```

---

## Option 2️⃣: Direct Link + Browser

Paste this in your browser URL bar:
```
https://cdn.jsdelivr.net/gh/opencv/opencv_zoo@master/models/face_detection_yunet/face_detection_yunet_1_3_0_416_416_simplify.onnx
```

Then:
- Right-click the downloaded file
- Move it to the FaceDetection folder
- Or let browser auto-save to Downloads, then cut/paste to the folder

---

## Option 3️⃣: PowerShell One-Liner

```powershell
# Copy this ENTIRE line, paste in PowerShell, press Enter:

Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/gh/opencv/opencv_zoo@master/models/face_detection_yunet/face_detection_yunet_1_3_0_416_416_simplify.onnx" -OutFile "C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection\face_detection_yunet_1_3_0_416_416_simplify.onnx" -UseBasicParsing; Write-Host "✓ Download complete!"
```

---

## After Download: Visual Studio Setup

```
Visual Studio (after file is downloaded):

1. Solution Explorer
   ├── TraceProject (project)
   │   ├── Models
   │   │   └── FaceDetection
   │   │       └── face_detection_yunet_1_3_0_416_416_simplify.onnx  ← File appears here
   │
2. Right-click the .onnx file
   │
3. Select "Properties"
   │
4. Find: "Copy to Output Directory"
   │
5. Set to: "Copy if newer"  ✓
   │
6. Click OK
```

---

## Verify It Worked

### Check in File Explorer:
```
📁 Open File Explorer
📁 Navigate to: C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection
✓ You should see: face_detection_yunet_1_3_0_416_416_simplify.onnx
```

### Check in PowerShell:
```powershell
Get-Item "C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection\*.onnx"
```

Expected output:
```
	Directory: C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection

Mode                 LastWriteTime         Length Name
----                 ------------- ---------- ----
-a----  9/22/2025   3:45 PM         345678 face_detection_yunet_1_3_0_416_416_simplify.onnx
```

---

## Then Build & Run

```bash
# In PowerShell or Command Prompt:
cd C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject
dotnet clean
dotnet build
dotnet run
```

---

## Look for This in Logs

When you run `dotnet run`, you should see:

```
[info] Loading YuNet face detector model from: ...Models\FaceDetection\face_detection_yunet_1_3_0_416_416_simplify.onnx
[info] ✓ YuNet face detector loaded successfully. ConfidenceThreshold: 0.6, InputSize: 416x416
[info] Frame processing loop started. Target rate: 5 FPS (~200ms between frames)
```

✅ **If you see this, it worked!**

---

## Common Issues

| Problem | Check |
|---------|-------|
| "Model not found" | File exists in FaceDetection folder? |
| File keeps downloading | Size should be ~340 KB, not 0 KB |
| Model not copied to output | Did you set "Copy if newer"? |
| "404 Not Found" | Try CDN link instead of GitHub raw |
| Download stalls | Try PowerShell over browser |

---

## File Location Reference

| Purpose | Folder | File Name |
|---------|--------|-----------|
| **Source Location** | `TraceProject\Models\FaceDetection` | `face_detection_yunet_1_3_0_416_416_simplify.onnx` |
| **At Runtime** | `bin\Debug\net10.0\Models\FaceDetection` | `face_detection_yunet_1_3_0_416_416_simplify.onnx` |

---

## Ready?

✅ Choose **Option 1, 2, or 3** above  
✅ Download the file  
✅ Put it in the folder  
✅ Set "Copy if newer" in Visual Studio  
✅ Build and run!

Then check the logs for the ✓ message above.

Need help? Let me know which option you chose and any error messages you see!
