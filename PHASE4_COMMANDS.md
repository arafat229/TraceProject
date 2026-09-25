# PHASE 4: QUICK COMMAND REFERENCE

## Apply Migration
```bash
cd C:\Users\ALIENWARE\source\repos\TraceProject
dotnet-ef database update
```

**Expected Output**:
```
Build started...
Build succeeded.
Applying migration '20260922230905_AddFaceEmbeddings'...
Done.
```

## Start Application
```bash
dotnet run
```

**Expected Startup Logs** (first 30 seconds):
```
[INF] Application started
[INF] Camera capture worker starting...
[INF] RTSP video source initialized
[INF] Face detector (YuNet) initialized successfully
[INF] Face recognizer (SFace) initialized successfully
[INF] Embedding cache loaded: 0 active embeddings
[INF] Frame processing service started
```

## Verify Database Schema
```bash
# Connect to database
psql "host=localhost port=5432 dbname=TraceProject user=postgres password=<your-postgres-password>"

# List tables
\dt

# View FaceEmbeddings structure
\d "FaceEmbeddings"

# Count embeddings (should be 0 initially)
SELECT COUNT(*) FROM "FaceEmbeddings" WHERE "IsActive" = true;

# Exit psql
\q
```

## Verify No Sensitive Data in Logs
```bash
# If logging to file, check for data leaks:
grep -E "embedding|0x[0-9a-f]|base64" logs.txt
# Should return: (nothing)

grep -E "admin|password|rtsp" logs.txt
# Should return: (nothing) - no credentials
```

## Add Temporary Console Code for Enrollment Test
**File**: `TraceProject/Program.cs`

Add this after `app.Build()` and before `await app.RunAsync()`:

```csharp
// TEMPORARY: Remove after Phase 4 verification
if (args.Contains("--enroll-test"))
{
	Console.WriteLine("\n=== PHASE 4 ENROLLMENT TEST ===\n");

	using (var scope = app.Services.CreateScope())
	{
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var frameService = scope.ServiceProvider.GetRequiredService<IFrameProcessingService>();
		var enrollmentService = scope.ServiceProvider.GetRequiredService<FaceEnrollmentService>();
		var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

		// Wait for frames to arrive from camera
		logger.LogInformation("Waiting for frames from camera...");
		await Task.Delay(5000);

		var latest = frameService.GetLatestDetectionResults();

		if (latest?.Faces.Count > 0)
		{
			var face = latest.Value.Faces[0];
			logger.LogInformation("Detected face with {Confidence:P1} confidence", face.Confidence);

			// Create test employee
			var employee = new Employee 
			{ 
				FirstName = "PhaseTest",
				LastName = "FaceVerification",
				Email = "phase4@test.local",
				IsActive = true
			};

			db.Employees.Add(employee);
			await db.SaveChangesAsync();
			logger.LogInformation("Created employee: {Name}", employee.FirstName);

			// Enroll their face
			await enrollmentService.EnrollDetectedFaceAsync(employee, face);
			logger.LogInformation("✅ ENROLLED: {Name} Face", employee.FirstName);

			// Verify in DB
			var embeddings = db.FaceEmbeddings.Count(f => f.EmployeeId == employee.Id && f.IsActive);
			logger.LogInformation("Database confirms: {Count} active embedding(s)", embeddings);
		}
		else
		{
			logger.LogError("❌ No faces detected! Check camera connectivity.");
		}
	}

	Console.WriteLine("\n=== END ENROLLMENT TEST ===\n");
	Environment.Exit(0);
}
```

Then run enrollment test:
```bash
dotnet run -- --enroll-test
```

**Expected Output**:
```
=== PHASE 4 ENROLLMENT TEST ===

Waiting for frames from camera...
Detected face with 95.3% confidence
Created employee: PhaseTest
✅ ENROLLED: PhaseTest Face
Database confirms: 1 active embedding(s)

=== END ENROLLMENT TEST ===
```

## Test Recognition - Match Case
```bash
# 1. Start app normally
dotnet run

# 2. In logs, monitor:
# [INF] Employee 'PhaseTest FaceVerification' recognized with 94.5% confidence

# 3. In another terminal, check database:
psql "host=localhost port=5432 dbname=TraceProject user=postgres password=<your-postgres-password>" \
  -c "SELECT COUNT(*) FROM \"Attendances\" WHERE \"EmployeeId\" = (SELECT \"Id\" FROM \"Employees\" WHERE \"FirstName\" = 'PhaseTest');"
```

## Test Recognition - Unknown Case
```bash
# 1. With app running (after test employee enrolled)
# 2. Present an unknown face to camera (someone not enrolled)
# 3. Monitor logs:
# [WRN] Unknown face detected (throttled - last logged 15s ago)

# 4. Verify NO attendance record created for unknown:
psql "host=localhost port=5432 dbname=TraceProject user=postgres" \
  -c "SELECT COUNT(*) FROM \"Attendances\" WHERE \"EmployeeId\" IS NULL;"
# Should return: 0
```

## Check Frame Processing Metrics
Look for logs every 10-30 seconds:
```
[INF] Frame Processing Metrics:
  Frames: 120
  Detections: 8
  Recognized: 7 (87.5%)
  Unknown: 1
  Avg Confidence: 93.2%
  Cache: 1 embeddings (1 employee)
  Last Refresh: 3s ago
```

## Verify Embedding Cache Auto-Refresh
```bash
# 1. Enroll another face for same employee
# 2. Monitor logs for:
# [INF] Embedding cache refreshed

# Execute manual refresh (if needed via DI):
# var recognizer = serviceProvider.GetRequiredService<IFaceRecognizer>();
# await recognizer.RefreshEmbeddingCacheAsync(dbContext);
```

## Cleanup After Phase 4 Test
```bash
# Remove temporary console code from Program.cs
# Delete test employee from database:
psql "host=localhost port=5432 dbname=TraceProject user=postgres password=<your-postgres-password>"

DELETE FROM "FaceEmbeddings" WHERE "EmployeeId" = (SELECT "Id" FROM "Employees" WHERE "FirstName" = 'PhaseTest');
DELETE FROM "Employees" WHERE "FirstName" = 'PhaseTest';
\q
```

## Documentation References

| Document | Purpose |
|----------|---------|
| PHASE4_COMPLETE.md | Executive summary and key facts |
| PHASE4_VERIFICATION_CHECKLIST.md | Detailed step-by-step testing |
| PHASES_HANDOFF.md | Overview of all phases 4-8 |

## Success Criteria Checklist

- [ ] Migration applied successfully
- [ ] App starts without errors
- [ ] Logs show "Face recognizer (SFace) initialized successfully"
- [ ] Real camera frames captured (metrics show non-zero frame count)
- [ ] Test employee can be enrolled
- [ ] Enrollment confirms in database (`FaceEmbeddings` table has 1 row)
- [ ] Enrolled face is recognized with high confidence (>85%)
- [ ] Logs show employee name (NOT embedding bytes)
- [ ] Unknown face is rejected ("Unknown face detected" log)
- [ ] Unknown faces do NOT create Attendance records
- [ ] Logs contain NO RTSP passwords or credentials
- [ ] Frame processing metrics are accurate
- [ ] Cache auto-refreshes (check log periodically)

## Next Steps

Once all criteria above are met, reply:
```
Phase 4 verified. Ready for Phase 5 (Attendance Persistence).
```

This will trigger implementation of:
- Attendance record creation on recognition
- Error handling for database failures
- Cooldown/dedup logic (Phase 6)
