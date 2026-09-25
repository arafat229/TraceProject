# Manual Download Instructions for YuNet Model

If the automated download scripts fail due to network issues, follow these manual steps:

## Option 1: Download from OpenCV Zoo (Direct Browser)

1. **Open your web browser** and go to:
   ```
   https://github.com/opencv/opencv_zoo/tree/master/models/face_detection_yunet
   ```

2. **Find the file**: `face_detection_yunet_1_3_0_416_416_simplify.onnx` (approximately 340 KB)

3. **Right-click the file** and select "Save link as..."

4. **Navigate to your project folder**:
   ```
   C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection\
   ```

5. **Save the file** there with the exact name: `face_detection_yunet_1_3_0_416_416_simplify.onnx`

---

## Option 2: Download from OpenCV Release (Backup)

If OpenCV Zoo link doesn't work, try:

1. Go to: https://github.com/opencv/opencv/wiki/Face-Detection-Models

2. Look for the YuNet model link

3. Download and place in the same folder above

---

## Option 3: One-Line PowerShell Command (Copy-Paste)

If you want to keep trying programmatically, try this in PowerShell:

```powershell
# Copy the entire line below and paste into PowerShell:
$url = "https://github.com/opencv/opencv_zoo/releases/download/v0.5/face_detection_yunet_2023mar_int8.onnx"; $out = "C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection\yunet.onnx"; [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri $url -OutFile $out -TimeoutSec 600; if (Test-Path $out) { Write-Host "✓ Success!" -ForegroundColor Green; Get-Item $out } else { Write-Host "✗ Failed" -ForegroundColor Red }
```

---

## Verify the File is in Place

Once downloaded, verify it's there:

```powershell
cd C:\Users\ALIENWARE\source\repos\TraceProject
Get-Item "TraceProject\Models\FaceDetection\*.onnx" | Select Name, @{N='Size MB';E={[Math]::Round($_.Length/1MB, 2)}}
```

You should see the file listed with ~340 KB size.

---

## Alternative: Use Docker to Download

If network issues persist, you can download the model using curl (which might be more reliable):

```powershell
# Install or use curl if available
curl -o "C:\Users\ALIENWARE\source\repos\TraceProject\TraceProject\Models\FaceDetection\face_detection_yunet_1_3_0_416_416_simplify.onnx" `
  "https://raw.githubusercontent.com/opencv/opencv_zoo/master/models/face_detection_yunet/face_detection_yunet_1_3_0_416_416_simplify.onnx"
```

---

## Next Steps After Download

Once the model file is in place (whether manually or programmatically):

1. **In Visual Studio**:
   - Right-click the `.onnx` file
   - Select **Properties**
   - Set **Copy to Output Directory** = **"Copy if newer"**

2. **Rebuild the project**:
   ```bash
   dotnet clean
   dotnet build
   ```

3. **Run the application**:
   ```bash
   dotnet run
   ```

---

## Troubleshooting

### File won't download due to network/proxy issues
- Try using a VPN or different network
- Check your firewall settings
- Contact your IT department if behind corporate proxy

### GitHub access blocked
- Try accessing GitHub directly in your browser first
- If you can't access GitHub, you may need proxy configuration

### Still stuck?
Let me know the exact error message and I can help troubleshoot.
