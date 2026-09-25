# Phase 4: Face Recognition Implementation Guide

## Overview
Phase 4 adds embedding-based face recognition to the TraceProject backend. It leverages SFace (via OpenCvSharp4 DNN) to compute face embeddings and match them against a database of enrolled employee embeddings, answering the question: "Whose face is this?"

**Note:** Phase 4 assumes Phase 1 (RTSP capture), Phase 2 (frame metrics), and Phase 3 (YuNet face detection) are already in place and working.

## Architecture

### Core Components

1. **IFaceRecognizer** (`Services/Interfaces/IFaceRecognizer.cs`)
   - Abstraction for face recognition operations
   - Key methods:
	 - `RecognizeAsync(DetectedFace, CancellationToken)` → Returns `RecognitionResult` (match or unknown)
	 - `ComputeEmbeddingAsync(DetectedFace, CancellationToken)` → Returns raw embedding (for enrollment)
	 - `RefreshEmbeddingCacheAsync(ApplicationDbContext)` → Loads active embeddings from DB into memory
	 - `GetCacheStatistics()` → Returns current cache state and stats

2. **SFaceRecognizer** (`Services/FaceRecognition/SFaceRecognizer.cs`)
   - Concrete implementation using OpenCvSharp4 DNN/ONNX
   - Loads SFace model at startup
   - Caches employee embeddings in memory
   - Performs cosine-similarity matching
   - Key features:
	 - Automatic cache refresh on configurable interval
	 - Placeholder model support (demo mode)
	 - Unknown-face throttling in logs (reduce spam)

3. **FaceEnrollmentService** (`Services/FaceRecognition/FaceEnrollmentService.cs`)
   - Enrollment workflow for creating and storing employee embeddings
   - Methods:
	 - `EnrollDetectedFaceAsync(Employee, DetectedFace)` → Compute and persist embedding
	 - `EnrollFaceFromCropAsync(Employee, byte[], width, height)` → Enroll from raw face crop
	 - `DeactivateEmployeeEmbeddingsAsync(Employee)` → Soft-disable embeddings
	 - `DeleteEmployeeEmbeddingsAsync(Employee)` → Hard-delete embeddings

4. **FaceEmbedding** (`Models/FaceEmbedding.cs`)
   - EF Core entity for persistent embedding storage
   - One-to-many relationship: `Employee → FaceEmbedding[]`
   - Stores:
	 - `EmbeddingData` (byte[] of float32 values, stored as PostgreSQL `bytea`)
	 - `ModelName` (e.g., "SFace")
	 - `EmbeddingDimension` (e.g., 128 for SFace)
	 - `IsActive` (soft-delete flag)
	 - Timestamps: `CreatedAtUtc`, `UpdatedAtUtc`

### Data Flow

```
Frame (from Phase 2)
  ↓
YuNetFaceDetector (Phase 3)
  ↓ [DetectedFace]
FrameProcessingService
  ├→ DetectFacesAsync()
  └→ For each DetectedFace:
	  └→ RecognizeAsync(DetectedFace)
		  ├→ ComputeEmbedding(face crop)
		  ├→ FindBestMatch(against cached embeddings)
		  └→ Return RecognitionResult (match or unknown)
  ↓
Logged metrics: face detections, recognized employees, unknown count, etc.
```

### Recognition Flow (at Runtime)

1. **Startup** (`Program.cs`):
   - `SFaceRecognizer.InitializeAsync()` loads the ONNX model
   - `RefreshEmbeddingCacheAsync()` loads all active `FaceEmbedding` rows from DB into memory

2. **Frame Processing Loop** (`FrameProcessingService`):
   - For each new frame buffer with detected faces:
	 - Call `detector.DetectFacesAsync()` → list of `DetectedFace`
	 - For each face:
	   - Call `recognizer.RecognizeAsync(face)` → `RecognitionResult`
	   - Log result (match: "Employee X recognized", unknown: throttled)
	   - Cache result for quick access if needed

3. **Cache Refresh** (periodic, ~every 60s):
   - `RefreshEmbeddingCacheAsync()` queries DB for active embeddings
   - Reloads cache in memory (thread-safe)
   - Automatically called by `FrameProcessingService` on interval

### Database Schema

**FaceEmbeddings Table**:
```sql
CREATE TABLE "FaceEmbeddings" (
	"Id" BIGSERIAL PRIMARY KEY,
	"EmployeeId" BIGINT NOT NULL,
	"EmbeddingData" BYTEA NOT NULL,          -- Raw float32 bytes
	"ModelName" VARCHAR(50) NOT NULL,        -- e.g., "SFace"
	"EmbeddingDimension" INTEGER NOT NULL,   -- e.g., 128
	"IsActive" BOOLEAN NOT NULL,             -- Soft-delete
	"CreatedAtUtc" TIMESTAMP NOT NULL,
	"UpdatedAtUtc" TIMESTAMP NOT NULL,
	"Source" VARCHAR(255),                   -- Optional metadata
	CONSTRAINT "FK_FaceEmbeddings_Employees_EmployeeId" 
		FOREIGN KEY ("EmployeeId") REFERENCES "Employees"("Id") 
		ON DELETE CASCADE
);

CREATE INDEX "IX_FaceEmbeddings_EmployeeId_IsActive" 
	ON "FaceEmbeddings"("EmployeeId", "IsActive");
CREATE INDEX "IX_FaceEmbeddings_IsActive" 
	ON "FaceEmbeddings"("IsActive");
```

**Relationship** in `Employee`:
```csharp
public List<FaceEmbedding> FaceEmbeddings { get; set; } = [];
```

## Configuration

### appsettings.json

Add a new `FaceRecognition` section:

```json
"FaceRecognition": {
	"ModelPath": "Models/FaceRecognition/face_recognition_sface.onnx",
	"SimilarityThreshold": 0.6,              // Cosine similarity >= 0.6 is a match
	"EmbeddingCacheRefreshSeconds": 60,     // Reload embeddings every ~60s
	"MaxEmbeddingsPerEmployee": 0            // 0 = unlimited, >0 = keep only latest N
}
```

### Key Settings Explained

- **ModelPath**: Relative path to SFace ONNX model file (placed in `Models/FaceRecognition/` at build time)
- **SimilarityThreshold**: Cosine similarity cutoff; lower = more lenient, higher = stricter
- **EmbeddingCacheRefreshSeconds**: How often to reload active embeddings from DB (prevents stale cache)
- **MaxEmbeddingsPerEmployee**: Enrollment limit per employee (useful for cleanup; 0 = no limit)

## Model Setup

### SFace ONNX Model

The face recognition uses **SFace**, a lightweight face recognition model designed for fast inference on CPU.

#### Download Instructions

1. **Official SFace ONNX**:
   - Source: OpenCV's face recognition models
   - Download from: https://github.com/opencv/opencv_zoo/tree/master/models/face_recognition_sface
   - File: `face_recognition_sface.onnx` (typically ~68 MB)

2. **Alternative (if above unavailable)**:
   - Search for "SFace ONNX" on Hugging Face Model Hub
   - Look for models tagged with `face-recognition` and `onnx`

3. **Placement**:
   - Save to: `TraceProject/Models/FaceRecognition/face_recognition_sface.onnx`
   - Ensure the filename matches the `ModelPath` setting in `appsettings.json`
   - The build process automatically copies all `.onnx` files to output directory

4. **Verification**:
   - File should be valid ONNX format (binary)
   - Size typically ~50-100 MB
   - Do NOT use the placeholder file for production

### Testing Model Readiness

To verify the model is loaded correctly, check the startup logs:
```
[INF] Face recognizer (SFace) initialized successfully. Model path: Models/FaceRecognition/face_recognition_sface.onnx
[INF] Embedding cache loaded: X active embeddings for Y employees
```

## Usage

### Enrollment (Adding a New Employee Face)

```csharp
// Requires IFaceEnrollmentService (injected or scoped)
var enrollmentService = sp.GetRequiredService<FaceEnrollmentService>();

// Option 1: From a DetectedFace (already cropped by detector)
var detectedFace = /* from FrameProcessingService */;
await enrollmentService.EnrollDetectedFaceAsync(employee, detectedFace);

// Option 2: From raw face crop bytes (e.g., manual upload)
byte[] faceCropBytes = /* ... */;
int width = 416, height = 416;
await enrollmentService.EnrollFaceFromCropAsync(employee, faceCropBytes, width, height);

// Result: New FaceEmbedding row created in DB, cache refreshed automatically
```

### Recognition (at Runtime)

**Automatic** via `FrameProcessingService`:
- For each detected face, `RecognizeAsync()` is called automatically
- Results logged as: `"Employee {name} recognized with {confidence}% confidence"`
- Unknown faces: throttled log (once per ~60s) to avoid spam

**Manual** (if needed):
```csharp
var recognizer = sp.GetRequiredService<IFaceRecognizer>();
var result = await recognizer.RecognizeAsync(detectedFace, cancellationToken);

if (result.IsMatch)
{
	Console.WriteLine($"Matched: {result.MatchedEmployeeName} ({result.Confidence})");
}
else
{
	Console.WriteLine("Unknown face");
}
```

### Deactivating Embeddings

```csharp
// Soft-delete (IsActive = false, can be reactivated later)
await enrollmentService.DeactivateEmployeeEmbeddingsAsync(employee);

// Hard-delete (removes rows permanently)
await enrollmentService.DeleteEmployeeEmbeddingsAsync(employee);
```

## Metrics and Monitoring

`FrameProcessingService` logs periodic metrics every ~10s (configurable via `VideoProcessing.MetricsSummaryIntervalSeconds`):

```
[INF] Frame Processing Metrics: 
  Frames: 45, 
  Detections: 12 faces, 
  Recognized: 10 (83%), 
  Unknown: 2, 
  Avg Confidence: 92.5%, 
  Cache: 24 embeddings (4 employees), 
  Last Cache Refresh: 2s ago
```

**Key Metrics**:
- **Frames**: Total frames processed in this interval
- **Detections**: Total faces detected
- **Recognized**: Faces matched to known employees (with %)
- **Unknown**: New/unrecognized faces
- **Avg Confidence**: Average cosine similarity of matched faces
- **Cache**: Current embedding cache size and employee count
- **Last Cache Refresh**: Time since cache was last reloaded from DB

## Integration Points

### FrameProcessingService Wiring

`FrameProcessingService` is automatically wired in `Program.cs`:

```csharp
builder.Services.AddSingleton<IFaceRecognizer, SFaceRecognizer>();
builder.Services.AddScoped<FaceEnrollmentService>();
builder.Services.AddHostedService<FrameProcessingService>();
builder.Services.AddSingleton<IFrameProcessingService>(
	sp => sp.GetRequiredService<FrameProcessingService>()
);
```

**No additional endpoints were added in Phase 4.**
- Enrollment must be done programmatically (e.g., via a future enrollment endpoint in Phase 5)
- Recognition results are logged internally; can be exposed via a controller later

### IFrameProcessingService (Phase 3 Interface)

The existing interface remains **detection-focused**:

```csharp
public interface IFrameProcessingService
{
	(IReadOnlyList<DetectedFace> Faces, DateTime ProcessedAtUtc)? GetLatestDetectionResults();
	bool IsRunning { get; }
}
```

**Recognition results are NOT exposed** through this interface yet. They are:
1. Logged internally
2. Cached locally in `FrameProcessingService`
3. Available for future APIs (e.g., `/face-recognition/statistics`, `/employee/{id}/recognized-count`)

## Known Limitations and Future Work

1. **No Attendance Logic Yet** (Phase 6):
   - Recognition does not yet create attendance records
   - No cooldown to prevent duplicate entries for the same face
   - No persistence of "seen at X time" events

2. **Limited Multi-Face Scenarios**:
   - Each face is recognized independently
   - No tracking of individual persons across frames (no face ID persistence)
   - No aggregated confidence scores for repeated detections

3. **Cache Mechanism**:
   - Cache is per-process (not shared across multiple instances)
   - Periodic refresh may cause brief inconsistencies in multi-instance deployments
   - No explicit invalidation on immediate enrollment (relies on refresh interval)

4. **Model Constraints**:
   - SFace is optimized for faces ~100-200 pixels wide
   - Very small or angled faces may have lower accuracy
   - No built-in age/gender/emotion detection

## Troubleshooting

### Issue: "Failed to initialize face recognizer on startup"

**Cause**: Model file not found or invalid format

**Solution**:
1. Verify `Models/FaceRecognition/face_recognition_sface.onnx` exists
2. Check that the file is valid binary ONNX (not the placeholder text file)
3. Confirm `appsettings.json` has the correct `ModelPath`
4. Rebuild and ensure model file is copied to output directory

### Issue: "Embedding cache loaded: 0 active embeddings"

**Cause**: No employees have enrolled faces yet, or all embeddings are marked inactive

**Solution**:
1. Use `FaceEnrollmentService.EnrollDetectedFaceAsync()` to enroll at least one employee
2. Verify `IsActive = true` in the database
3. Check that `EmbeddingCacheRefreshSeconds` is set (default 60s)

### Issue: "Unknown" faces logged constantly

**Cause**: Recognition is working, but faces do not match any enrolled embeddings

**Solution**:
1. Enroll more employee faces (multiple faces per employee improve matching)
2. Verify lighting and angles match enrollment conditions
3. Lower `SimilarityThreshold` to make matching more lenient (e.g., 0.5 instead of 0.6)
4. Check embeddings are `IsActive = true`

### Issue: "High average confidence mismatch" (very high even for unknown faces)

**Cause**: Placeholder model is still in use

**Solution**:
1. Replace the placeholder file at `Models/FaceRecognition/face_recognition_sface.onnx` with the real SFace model
2. Restart the application

## Next Steps

1. **Download and place the real SFace ONNX model** at `Models/FaceRecognition/face_recognition_sface.onnx`
2. **Apply the migration** to add `FaceEmbeddings` table to the database:
   ```bash
   dotnet-ef database update
   ```
3. **Enroll a test employee**:
   - Start the app
   - Call `FaceEnrollmentService.EnrollDetectedFaceAsync()` with a detected face
   - Verify log shows "Embedding cached for employee X"
4. **Test real-time recognition**:
   - Present the enrolled face to the camera
   - Verify logs show "Employee X recognized with 95% confidence"
5. **(Future) Phase 5**: Add REST API endpoints for enrollment and recognition statistics
6. **(Future) Phase 6**: Implement attendance logic, cooldowns, and duplicate prevention

## References

- **SFace Paper**: "SFace: An Efficient Network for Face Recognition in High Resolution Images" (2021)
- **OpenCV Face Recognition Zoo**: https://github.com/opencv/opencv_zoo/tree/master/models/face_recognition_sface
- **OpenCvSharp4**: https://github.com/shimat/opencvsharp
- **Cosine Similarity**: https://en.wikipedia.org/wiki/Cosine_similarity

---

**Last Updated**: 2026-09-22  
**Author**: TraceProject Development Team  
**Status**: Phase 4 Complete (Recognition Core), Awaiting Real Model + Enrollment/Attendance API
