# 🎯 How to Download the YuNet Model File - SIMPLE GUIDE

Your directory is ready:  
📁 `C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection`

---

## Method 1: Browser Download (SIMPLEST) ✅

### Step 1: Open Browser
Go to this URL in your web browser:
```
https://github.com/opencv/opencv_zoo/tree/master/models/face_detection_yunet
```

### Step 2: Find the File
You should see several `.onnx` files in the list. The one you need is:
```
face_detection_yunet_1_3_0_416_416_simplify.onnx
```
It's usually ~340 KB in size.

### Step 3: Right-Click Download
- Right-click on the filename
- Select: **"Save link as..."** (or **"Download"** depending on your browser)
- A save dialog will pop up

### Step 4: Navigate to Your Folder
In the save dialog, navigate to:
```
C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection
```

### Step 5: Save
- Click **Save**
- Wait for download to complete
- You'll see the file appear in the folder

### Step 6: Copy Exact Path
After saving, the full path should be:
```
C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection\face_detection_yunet_1_3_0_416_416_simplify.onnx
```

---

## Method 2: Direct Link (If Method 1 Fails)

If the GitHub link doesn't work, try this CDN mirror:
```
https://cdn.jsdelivr.net/gh/opencv/opencv_zoo@master/models/face_detection_yunet/face_detection_yunet_1_3_0_416_416_simplify.onnx
```

Right-click this link in your browser → Save as → Save to the FaceDetection folder

---

## Method 3: Copy-Paste PowerShell One-Liner

If you prefer command-line (copy the ENTIRE line below):

```powershell
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/gh/opencv/opencv_zoo@master/models/face_detection_yunet/face_detection_yunet_1_3_0_416_416_simplify.onnx" -OutFile "C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection\face_detection_yunet_1_3_0_416_416_simplify.onnx" -UseBasicParsing; Write-Host "Done!"
```

**How to use:**
1. Open **PowerShell** (Windows 10/11: Press Win+X → PowerShell)
2. Copy the entire line above
3. Right-click in PowerShell and paste
4. Press **Enter**
5. Wait for "Done!" message

---

## Verify the File Was Downloaded

After downloading, verify the file is there:

**Option A: In File Explorer**
1. Press `Windows Key + E`
2. Navigate to: `C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection`
3. You should see the `.onnx` file

**Option B: PowerShell**
```powershell
Get-Item "C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection\*.onnx"
```
You should see output with the filename and size (~340 KB)

---

## Next Step: Configure Visual Studio

Once the file is downloaded:

1. **Open Visual Studio**
2. **Find the file** in Solution Explorer (it might not show up automatically)
   - Right-click project → Add → Existing Item
   - Or just refresh (F5)
3. **Right-click the .onnx file** in Solution Explorer  
4. **Select Properties** (at the bottom)
5. **Set "Copy to Output Directory"** = **"Copy if newer"**
6. **Click Apply** and **OK**

This ensures the model file is copied to your bin directory when you build.

---

## Build & Run

After the file is in place and configured:

```bash
cd C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject
dotnet clean
dotnet build
dotnet run
```

You should see in the logs:
```
Loading YuNet face detector model from: ...
✓ YuNet face detector loaded successfully
```

---

## Troubleshooting

### "File still not downloading"
- Check your internet connection
- Try a different browser (Chrome, Edge, Firefox)
- Disable any browser extensions that might block downloads
- Try Method 2 (CDN mirror) or Method 3 (PowerShell)

### "File shows 0 KB or is corrupted"
- Delete the file
- Try downloading again using a different method
- Make sure download completed (check file properties for timestamp)

### "Visual Studio won't copy the file to output"
- Make sure you set "Copy to Output Directory" = "Copy if newer" ✓
- Try "Copy always" instead
- Manually copy the file to `bin\Debug\net10.0\Models\FaceDetection\`

### Error says "Model not found" at runtime
- Verify the file exists in the output directory:
  ```
  C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\bin\Debug\net10.0\Models\FaceDetection\
  ```
- If it's not there, you missed the "Copy to Output Directory" step

---

## Quick Checklist

- [ ] Downloaded the `.onnx` file
- [ ] Saved it to: `C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection\`
- [ ] File name is exactly: `face_detection_yunet_1_3_0_416_416_simplify.onnx`
- [ ] Set "Copy to Output Directory" = "Copy if newer" in Visual Studio
- [ ] File size is ~340 KB (not 0 KB)
- [ ] Built the project: `dotnet build`
- [ ] File exists in: `bin\Debug\net10.0\Models\FaceDetection\`
- [ ] Run the app: `dotnet run`

---

## Need Help?

If you're still stuck:
1. Tell me which method you tried (Browser, CDN, PowerShell)
2. Share the exact error message you see
3. I'll help troubleshoot!
