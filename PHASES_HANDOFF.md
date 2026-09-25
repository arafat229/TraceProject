# PHASES 4-8: IMPLEMENTATION HANDOFF

## Overview
Phases 4-8 are designed to be implemented sequentially with verification at each phase boundary. This document consolidates the current status and detailed verification steps for each phase.

## PHASE 4: FACE RECOGNITION

### Status: Code Complete, Awaiting Verification

**Files Created/Modified**:
```
NEW FILES:
  ✅ TraceProject/Models/FaceEmbedding.cs
  ✅ TraceProject/Models/RecognitionResult.cs
  ✅ TraceProject/Configuration/FaceRecognitionOptions.cs
  ✅ TraceProject/Services/Interfaces/IFaceRecognizer.cs
  ✅ TraceProject/Services/FaceRecognition/SFaceRecognizer.cs
  ✅ TraceProject/Services/FaceRecognition/FaceEnrollmentService.cs
  ✅ TraceProject/Data/Migrations/20260922230905_AddFaceEmbeddings.cs
  ✅ TraceProject/Data/Migrations/20260922230905_AddFaceEmbeddings.Designer.cs

MODIFIED FILES:
  ✅ TraceProject/Models/Employee.cs - Added FaceEmbeddings navigation property
  ✅ TraceProject/Data/ApplicationDbContext.cs - Added FaceEmbeddings DbSet
  ✅ TraceProject/Services/FrameProcessingService.cs - Integrated recognition
  ✅ TraceProject/Program.cs - DI registration and startup initialization
  ✅ TraceProject/appsettings.json - Added FaceRecognition section
```

### Migration Details
**File**: `TraceProject/Data/Migrations/20260922230905_AddFaceEmbeddings.cs`

**Creates**: `FaceEmbeddings` table with schema:
```sql
CREATE TABLE "FaceEmbeddings" (
	"Id" BIGSERIAL PRIMARY KEY,
	"EmployeeId" BIGINT NOT NULL,
	"EmbeddingData" BYTEA NOT NULL,           -- Float32 embedding bytes
	"ModelName" VARCHAR(50) NOT NULL,         -- "SFace"
	"EmbeddingDimension" INT NOT NULL,        -- 128
	"IsActive" BOOLEAN NOT NULL DEFAULT true,
	"CreatedAtUtc" TIMESTAMP NOT NULL,
	"UpdatedAtUtc" TIMESTAMP NOT NULL,
	"Source" VARCHAR(255),
	CONSTRAINT "FK_FaceEmbeddings_Employees_EmployeeId"
		FOREIGN KEY ("EmployeeId") REFERENCES "Employees"("Id")
		ON DELETE CASCADE
);

CREATE INDEX "IX_FaceEmbeddings_EmployeeId_IsActive" 
	ON "FaceEmbeddings"("EmployeeId", "IsActive");
CREATE INDEX "IX_FaceEmbeddings_IsActive" 
	ON "FaceEmbeddings"("IsActive");
```

**⚠️ NOTE**: Migration also includes unrelated schema changes to Locations, Cameras, Attendances tables (model drift). Review before applying.

### NuGet Packages
- OpenCvSharp4 (4.9.0.20240103) - Already installed, used for ONNX with SFace
- Npgsql.EntityFrameworkCore.PostgreSQL (10.0.0) - Already installed
- Microsoft.EntityFrameworkCore (10.0.11) - Already installed

### Architecture
```
PIPELINE FLOW:
  RTSP Frame → YuNet Detection → SFace Recognition → Identity Match
			   (Phase 3)        (Phase 4 NEW)

COMPONENTS:
  • IFaceRecognizer (interface)
  • SFaceRecognizer (ONNX-based implementation)
	- Loads SFace model at startup
	- Computes embeddings from face crops
	- Maintains in-memory embedding cache
	- Performs cosine similarity matching
  • FaceEnrollmentService (enrollment workflow)
	- Creates embeddings from detected faces
	- Persists to FaceEmbeddings table
	- Refreshes cache after enrollment
  • FaceRecognitionOptions (configuration)
	- ModelPath: Path to SFace ONNX
	- SimilarityThreshold: Match cutoff (default 0.6)
	- EmbeddingCacheRefreshSeconds: Cache refresh interval (default 60)

INTEGRATION:
  • FrameProcessingService now:
	- Accepts optional IFaceRecognizer via DI
	- Calls RecognizeAsync() per detected face
	- Caches recognition results
	- Logs metrics (recognized count, unknown count, avg confidence)
  • Frame processing: 5 FPS, ~200ms per frame
```

### Verification Steps
See: `PHASE4_VERIFICATION_CHECKLIST.md` for complete testing procedure

**Summary**:
1. Apply migration: `dotnet-ef database update`
2. Start app: `dotnet run`
3. Enroll test employee face
4. Present enrolled face to camera → verify recognition log
5. Present unknown face → verify "Unknown" log (not added to attendance)
6. Verify NO embeddings/images/passwords in logs

### How to Enroll (Manual for Phase 4)
Since Phase 7 provides the API, Phase 4 enrollment is currently programmatic:

**Option**: Add to Program.cs startup:
```csharp
// After app.Build(), before app.Run()
using (var scope = app.Services.CreateScope())
{
	var frameService = scope.ServiceProvider.GetRequiredService<IFrameProcessingService>();
	var enrollmentService = scope.ServiceProvider.GetRequiredService<FaceEnrollmentService>();
	var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

	// Wait for frames to arrive
	await Task.Delay(3000);

	var latest = frameService.GetLatestDetectionResults();
	if (latest?.Faces.Count > 0)
	{
		var employee = new Employee 
		{ 
			FirstName = "TestFace",
			LastName = "Employee",
			Email = "test@face.local",
			IsActive = true
		};
		db.Employees.Add(employee);
		await db.SaveChangesAsync();

		await enrollmentService.EnrollDetectedFaceAsync(employee, latest.Value.Faces[0]);
		Log.Information("✅ Enrolled {Name}", employee.FirstName);
	}
}
```

---

## PHASE 5: ATTENDANCE PERSISTENCE (Planned)

### Objective
Store recognition results as Attendance records. No cooldown/dedup yet.

### Required Actions
1. Extend Attendance entity with recognition fields (if not already present)
2. Create migration for Attendance schema updates (indexes)
3. Implement AttendanceInsertionService
4. Wire into FrameProcessingService post-recognition
5. Add error handling (catch, log, don't crash pipeline)

### Will Cover
- Attendance record creation on successful recognition
- Bypassing Attendance for Unknown faces and inactive employees
- Async EF Core with cancellation tokens
- Indexes: (EmployeeId + CheckIn), (CameraId + CheckIn)
- Database error resilience

---

## PHASE 6: COOLDOWN / DUPLICATE PREVENTION (Planned)

### Objective
Same person in 100 frames = 1 Attendance record (not 100)

### Key Features
- Configurable AttendanceCooldownMinutes in appsettings
- In-memory cache + database check (not memory alone)
- Throttled "Duplicate attendance ignored" log
- Testable independent of frame loop

### Will Cover
- AttendanceDedupService implementation
- Cache + DB source-of-truth pattern
- Unit test harness

---

## PHASE 7: API WIRING (Planned)

### Objective
Connect pipeline to existing controllers (don't rewrite CRUD)

### Will Cover
- CamerasController live status endpoint (connected, resolution, fps, last frame time)
- Verify no RTSP passwords in API responses
- AttendancesController today's records query (add only if missing)
- Inspect Controllers/ first, extend don't replace

---

## PHASE 8: REPORTS (Planned)

### Objective
Reflect real pipeline in existing ReportsController/DashboardController

### Will Cover
- Daily/monthly attendance from Phase 5 persistence
- Recognized vs. unknown counts from metrics
- Extend don't rebuild (inspect Controllers/ first)

---

## Current Blockers / Next Steps

### To Proceed to Phase 4 Verification:
1. **Apply migration** to create FaceEmbeddings table
2. **Run app** (`dotnet run`) and confirm startup logs show recognizer loaded
3. **Enroll a test employee** face (follow instructions above)
4. **Test recognition** with real camera:
   - Present enrolled face → should log recognition + confidence
   - Present unknown face → should log "Unknown" (no Attendance record)
5. **Confirm acceptance criteria** from checklist

### Known Issues / Considerations
- Migration includes unrelated schema changes (review carefully)
- SFace model must be real ONNX file (current is placeholder)
- No enrollment API yet (manual/programmatic only in Phase 4)
- Phase 1-3 code untouched (per requirements)

---

## Build Status
✅ **Compiles**: 0 errors, all Phase 4 code active
✅ **Ready for migration + verification**
⚠️ **Pending**: Database update + camera testing

---

## Summary Table

| Phase | Objective | Status | Files | Migration | Next |
|-------|-----------|--------|-------|-----------|------|
| 4 | Face Recognition | ✅ Code done | 13 files | 20260922230905 | Apply + Test |
| 5 | Attendance Persist | 🔄 Awaiting Phase 4 verify | Planned | Planned | After Phase 4 OK |
| 6 | Cooldown + Dedup | 🔄 Awaiting Phase 5 | Planned | Planned | After Phase 5 OK |
| 7 | API Wiring | 🔄 Awaiting Phase 6 | Planned | None | After Phase 6 OK |
| 8 | Reports | 🔄 Awaiting Phase 7 | Planned | None | After Phase 7 OK |

---

**Next Action**: Follow `PHASE4_VERIFICATION_CHECKLIST.md` to verify Phase 4 against real camera. Reply when ready to proceed to Phase 5.
