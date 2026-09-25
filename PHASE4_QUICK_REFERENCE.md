# Phase 4 Quick Reference

## Essential Commands

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

### Apply Database Migration
```bash
dotnet-ef database update
```

### Add a New Migration
```bash
dotnet-ef migrations add <MigrationName> --output-dir Data/Migrations
```

### Restore NuGet Packages
```bash
dotnet restore
```

---

## Key File Locations

| Purpose | Path |
|---------|------|
| Main recognizer | `TraceProject/Services/FaceRecognition/SFaceRecognizer.cs` |
| Enrollment service | `TraceProject/Services/FaceRecognition/FaceEnrollmentService.cs` |
| Recognizer interface | `TraceProject/Services/Interfaces/IFaceRecognizer.cs` |
| FaceEmbedding entity | `TraceProject/Models/FaceEmbedding.cs` |
| Recognition result DTO | `TraceProject/Models/RecognitionResult.cs` |
| Config options | `TraceProject/Configuration/FaceRecognitionOptions.cs` |
| Frame processing loop | `TraceProject/Services/FrameProcessingService.cs` |
| DB context | `TraceProject/Data/ApplicationDbContext.cs` |
| Migration (generated) | `TraceProject/Data/Migrations/20260922230905_AddFaceEmbeddings.cs` |
| Config file | `TraceProject/appsettings.json` |
| DI & startup wiring | `TraceProject/Program.cs` |
| Project file | `TraceProject/TraceProject.csproj` |
| ONNX model (placeholder) | `TraceProject/Models/FaceRecognition/face_recognition_sface.onnx` |

---

## Configuration Settings

```json
{
  "FaceRecognition": {
	"ModelPath": "Models/FaceRecognition/face_recognition_sface.onnx",
	"SimilarityThreshold": 0.6,
	"EmbeddingCacheRefreshSeconds": 60,
	"MaxEmbeddingsPerEmployee": 0
  }
}
```

**Tuning Tips:**
- Lower `SimilarityThreshold` (e.g., 0.5) if getting too many "Unknown" results
- Raise `SimilarityThreshold` (e.g., 0.7) if getting false matches
- Increase `EmbeddingCacheRefreshSeconds` (e.g., 300) for better performance in static scenarios
- Set `MaxEmbeddingsPerEmployee` > 0 to limit enrollment size per employee

---

## Common Tasks

### Enroll an Employee Face Programmatically
```csharp
var enrollmentService = serviceProvider.GetRequiredService<FaceEnrollmentService>();
var employee = /* get from DB */;
var detectedFace = /* from frame processing */;

await enrollmentService.EnrollDetectedFaceAsync(employee, detectedFace);
// Result: Embedding stored in DB, cache refreshed
```

### Manually Recognize a Face
```csharp
var recognizer = serviceProvider.GetRequiredService<IFaceRecognizer>();
var result = await recognizer.RecognizeAsync(detectedFace, cancellationToken);

if (result.IsMatch)
	Console.WriteLine($"Matched: {result.MatchedEmployeeName}");
else
	Console.WriteLine("Unknown face");
```

### Get Cache Statistics
```csharp
var recognizer = serviceProvider.GetRequiredService<IFaceRecognizer>();
var stats = recognizer.GetCacheStatistics();
Console.WriteLine($"Cache size: {stats.CacheSize}, Employees: {stats.EmployeeCount}");
```

### Force Cache Refresh
```csharp
var recognizer = serviceProvider.GetRequiredService<IFaceRecognizer>();
var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

await recognizer.RefreshEmbeddingCacheAsync(dbContext);
// Result: Cache reloaded from database
```

### Deactivate All Embeddings for an Employee
```csharp
var enrollmentService = serviceProvider.GetRequiredService<FaceEnrollmentService>();
var employee = /* from DB */;

await enrollmentService.DeactivateEmployeeEmbeddingsAsync(employee);
// Result: All embeddings marked IsActive = false, cache refreshed
```

---

## Debugging

### Check Recognizer Status
Look for startup logs:
```
[INF] Face recognizer (SFace) initialized successfully. Model path: Models/FaceRecognition/face_recognition_sface.onnx
[INF] Embedding cache loaded: X active embeddings for Y employees
```

### Check Recognition Logs
During frame processing:
```
[INF] Employee John recognized with 94.2% confidence
[WRN] Unknown face detected (throttled, last logged 45s ago)
```

### Check Metrics
Every ~10 seconds:
```
[INF] Frame Processing Metrics: 
  Frames: 45, 
  Detections: 12, 
  Recognized: 10, 
  Unknown: 2, 
  Avg Confidence: 92.5%, 
  Cache: 24 embeddings (4 employees), 
  Last Refresh: 2s ago
```

### Troubleshoot Model Loading
If recognizer fails to initialize:
1. Verify `Models/FaceRecognition/face_recognition_sface.onnx` exists
2. Ensure it's not the placeholder text file (should be binary ONNX)
3. Check `appsettings.json` has correct `ModelPath`
4. Rebuild to ensure model copied to output directory

---

## NuGet Package Versions

| Package | Version | Purpose |
|---------|---------|---------|
| OpenCvSharp4 | 4.9.0.20240103 | Face detection & recognition (DNN/ONNX) |
| OpenCvSharp4.runtime.win | 4.9.0.20240103 | Windows runtime for OpenCV |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 | PostgreSQL database access |
| Microsoft.EntityFrameworkCore | 10.0.11 | ORM and migrations |
| Microsoft.EntityFrameworkCore.Tools | 10.0.11 | EF migrations CLI (dotnet-ef) |

---

## Important Files to Review

1. **PHASE4_FACE_RECOGNITION.md** - Full technical documentation
2. **PHASE4_COMPLETION_SUMMARY.md** - Implementation summary and status
3. **SFaceRecognizer.cs** - Core recognition engine (start here for understanding flow)
4. **SFaceRecognizer.cs** - Uses OpenCvSharp for ONNX model loading and inference

---

## Next Steps

### Immediate (Before Testing)
1. Replace placeholder ONNX: Download real SFace model to `Models/FaceRecognition/face_recognition_sface.onnx`
2. Apply migration: `dotnet-ef database update`
3. Build: `dotnet build`
4. Run: `dotnet run`

### Testing
1. Verify startup logs show recognizer initialized
2. Enroll a test employee face
3. Present that face to camera
4. Verify logs show recognition match

### Integration (Phase 5+)
1. Add REST APIs for enrollment and statistics
2. Implement attendance logic with cooldowns
3. Add duplicate-prevention and face-ID tracking
4. Create recognition dashboard/metrics endpoint

---

## Support & Documentation

- Full guide: See `PHASE4_FACE_RECOGNITION.md`
- Architecture: See "Architecture" section in `PHASE4_FACE_RECOGNITION.md`
- Troubleshooting: See "Troubleshooting" section in `PHASE4_FACE_RECOGNITION.md`
- Model details: See "Model Setup" section in `PHASE4_FACE_RECOGNITION.md`

---

**Last Updated:** 2026-09-22 | Phase 4 Complete
