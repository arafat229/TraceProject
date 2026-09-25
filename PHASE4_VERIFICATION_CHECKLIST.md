# PHASE 4: FACE RECOGNITION - VERIFICATION CHECKLIST

## Current Status
- ✅ All Phase 4 code exists and compiles successfully
- ✅ SFace ONNX model file present
- ✅ DI registration complete in Program.cs
- ✅ FaceRecognitionOptions binding active
- ✅ FrameProcessingService integrated with recognizer
- ❌ **Migration NOT yet applied to database** (requires user action)

## Prerequisites Before Testing

### 1. Database Migration (REQUIRED)
Run this command BEFORE testing:
```bash
cd C:\Users\ALIENWARE\source\repos\TraceProject
dotnet-ef database update
```

This will:
- Create `FaceEmbeddings` table with schema:
  - Id (BIGSERIAL PRIMARY KEY)
  - EmployeeId (BIGINT, FK to Employees)
  - EmbeddingData (BYTEA - raw embedding bytes)
  - ModelName (VARCHAR(50) - e.g., "SFace")
  - EmbeddingDimension (INT - e.g., 128)
  - IsActive (BOOLEAN)
  - CreatedAtUtc (TIMESTAMP)
  - UpdatedAtUtc (TIMESTAMP)
  - Source (VARCHAR(255), optional)
- Add indexes: (EmployeeId, IsActive), (IsActive)
- Add FK constraint with cascade delete

**⚠️ CRITICAL NOTE**: The migration includes unrelated schema changes to Locations, Cameras, Attendances tables (model drift from previous work). Review the migration output before confirming it's correct.

### 2. Real Camera Connection
Camera must be accessible at: `rtsp://admin:admin@192.168.1.10:554/`
- Model: ASECAM 4MP HEVC/H.265
- Transport: TCP
- Verify BEFORE starting app with: `ping 192.168.1.10`

### 3. SFace Model File
- Location: `TraceProject/Models/FaceRecognition/face_recognition_sface.onnx`
- Should be ~68 MB binary ONNX file (not placeholder text)
- If placeholder, replace with real model from: https://github.com/opencv/opencv_zoo

## Phase 4 Verification Flow

### Step 1: Start with Real Camera
```bash
dotnet run
```

Expected startup logs:
```
[INF] Camera capture worker starting...
[INF] RTSP video source initialized
[INF] Face detector (YuNet) initialized successfully
[INF] Face recognizer (SFace) initialized successfully
[INF] Embedding cache loaded: 0 active embeddings (no employees enrolled yet)
[INF] Frame processing service started
```

**VERIFY**: Camera is connected and frames are being captured (look for "Frames processed" metrics every 10-30 seconds)

### Step 2: Enroll a Test Employee Face

Since enrollment API doesn't exist yet, you must do this programmatically. Two options:

#### Option A: Add Temporary Console Code to Program.cs
Add after app.Run():
```csharp
// TEMPORARY ENROLLMENT TEST (remove after Phase 4 verification)
if (args.Contains("--enroll-test"))
{
	using (var scope = app.Services.CreateScope())
	{
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var enrollmentService = scope.ServiceProvider.GetRequiredService<FaceEnrollmentService>();
		var frameService = scope.ServiceProvider.GetRequiredService<IFrameProcessingService>();

		// Wait for frame processing to initialize
		await Task.Delay(3000);

		var latestResults = frameService.GetLatestDetectionResults();
		if (latestResults?.Faces.Count > 0)
		{
			// Create or get test employee
			var employee = db.Employees.FirstOrDefault(e => e.FirstName == "TestEnrollment")
				?? new Employee { FirstName = "TestEnrollment", LastName = "Face", Email = "test@enroll.local", IsActive = true };
			db.Employees.Add(employee);
			await db.SaveChangesAsync();

			// Enroll their face
			var face = latestResults.Value.Faces[0];
			await enrollmentService.EnrollDetectedFaceAsync(employee, face);
			Console.WriteLine($"✅ ENROLLED: {employee.FirstName} with {face.Confidence:P1} confidence");
		}
		else
		{
			Console.WriteLine("❌ No face detected for enrollment");
		}
	}
	Environment.Exit(0);
}
```

Run:
```bash
dotnet run -- --enroll-test
```

#### Option B: Manual Database Insert (if API enrollment not working)
```sql
INSERT INTO "Employees" ("FirstName", "LastName", "Email", "IsActive", "CreatedAt")
VALUES ('TestFace', 'Employee', 'testface@local', true, NOW())
RETURNING "Id";

-- Then use FaceEnrollmentService programmatically via app
```

**EXPECTED OUTPUT**:
```
✅ ENROLLED: TestEnrollment Face
[INF] Face embedding stored for TestEnrollment Face with 128-dim embedding
[INF] Embedding cache refreshed: 1 employee, 1 active embedding
```

### Step 3: Verify Enrollment in Database
```sql
SELECT COUNT(*) FROM "FaceEmbeddings" WHERE "IsActive" = true;
-- Should return: 1

SELECT e."FirstName", COUNT(f."Id") as embedding_count 
FROM "Employees" e
LEFT JOIN "FaceEmbeddings" f ON e."Id" = f."EmployeeId" AND f."IsActive" = true
GROUP BY e."Id", e."FirstName"
WHERE e."FirstName" = 'TestEnrollment';
-- Should show: TestEnrollment | 1
```

### Step 4: Test Real Recognition - Match Case
1. Keep app running (`dotnet run`)
2. **Present the enrolled employee's face to the camera**
3. Observe logs for recognition match:

**EXPECTED LOG OUTPUT**:
```
[INF] Frame Processing Metrics: 
  Frames: 120, 
  Detections: 5, 
  Recognized: 4 (80%), 
  Unknown: 1,
  Avg Confidence: 94.3%,
  Cache: 1 embeddings (1 employee),
  Last Refresh: 5s ago

[INF] Employee 'TestEnrollment Face' recognized with 94.3% confidence
[INF] Attendance record created for TestEnrollment (confidence: 0.943)
```

**VERIFY**:
- ✅ Recognition confidence shown (e.g., 94.3%)
- ✅ Employee name logged (NOT embedding bytes or face image)
- ✅ Metrics show recognized count increasing
- ✅ NO sensitive data (embeddings, face images) in logs

### Step 5: Test Real Recognition - Unknown Case
1. **Present an unknown face** (someone not enrolled) to the camera
2. Observe logs:

**EXPECTED LOG OUTPUT**:
```
[WRN] Unknown face detected (throttled - last logged 5s ago)
[INF] Frame Processing Metrics:
  ...
  Unknown: 2,  # increased
  ...
```

**VERIFY**:
- ✅ Unknown face is logged
- ✅ Throttling is active (message doesn't repeat every frame, only periodically)
- ✅ Unknown face does NOT create an Attendance record
- ✅ NO face image data in logs

### Step 6: Verify No Sensitive Data Leakage
Check logs file (if logging to file enabled):
```bash
grep -i "embedding\|byte\|base64\|0x" logs.txt
# Should return: (nothing) - no embedding data leaked
```

Check logs for password leakage:
```bash
grep -i "admin\|password\|rtsp" logs.txt
# Should return: (nothing) - RTSP connection string not logged
```

## Acceptance Criteria for Phase 4

✅ **PASS if**:
1. Migration applied successfully to database
2. App starts without errors, SFace model loads
3. Real camera captures frames consistently
4. Test employee can be enrolled (embedding stored in DB)
5. Enrolled face is recognized with confidence score shown in logs
6. Unknown face is rejected and NOT recorded as attendance
7. "Unknown person" logs are throttled (not every frame)
8. NO embeddings, face images, or RTSP credentials in any logs
9. Metrics show accurate detection/recognition/unknown counts

❌ **FAIL if**:
- Migration fails or creates wrong schema
- SFace model doesn't load (startup error)
- Camera doesn't connect
- Enrollment doesn't store embedding in DB
- Recognition always returns "Unknown" (threshold too high or model issue)
- Unknown faces create Attendance records (should not)
- Passwords/embeddings appear in logs
- App crashes when unknown face detected

## Command Reference

```bash
# Apply migration
dotnet-ef database update

# Start app for testing
dotnet run

# Test enrollment (if console code added)
dotnet run -- --enroll-test

# Check logs (if logging to file)
tail -f logs.txt

# Query enrolled faces
psql "host=localhost port=5432 dbname=TraceProject user=postgres password=<your-postgres-password>" \
  -c "SELECT COUNT(*) FROM \"FaceEmbeddings\" WHERE \"IsActive\" = true;"

# Check Attendance records (should be empty after Phase 4)
psql "host=localhost port=5432 dbname=TraceProject user=postgres password=<your-postgres-password>" \
  -c "SELECT COUNT(*) FROM \"Attendances\";"
```

## Next After Phase 4 Verification

Once you confirm all acceptance criteria are met, reply:
```
Phase 4 verified. Ready to continue to Phase 5 (Attendance Persistence).
```

This will trigger implementation of:
- Attendance insertion on recognition
- Database error handling
- Phase 5 migration (indexes on Attendance)
