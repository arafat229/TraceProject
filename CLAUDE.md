# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project layout

An ASP.NET Core (.NET 10) Web API for employee attendance tracking through face recognition on an RTSP camera feed, plus a desktop monitoring agent. `TraceProject.slnx` references two projects:

- **`TraceProject/TraceProject.csproj`**: the API.
- **`TraceAgent/TraceAgent.csproj`**: the WinForms tray agent installed on employees' laptops.

### Project goals and where each one stands

This is a research project. Its overall aim is a workforce monitoring system that combines AI smart cameras (tracking where workers physically are) with laptop activity monitoring, to improve transparency, accountability and productivity. The three specific objectives:

1. **AI cameras detect and verify that employees are physically present at their workstations.** Mostly built: this is the video → recognition → attendance pipeline described below. It records daily check-in and check-out per camera location (one camera, "Main Entrance" by default), not presence at a particular desk.
2. **A desktop monitoring app tracks computer usage, active applications, working time and idle time.** Built: the TraceAgent tray app plus its API (see "Desktop monitoring" below). The dashboard has a Devices page and an Activity Monitoring tab on each employee.
3. **One central system combines the camera and desktop data and updates in real time.** Built (see "Presence" below): a combined status per employee, pushed live as `PresenceChanged`, plus a daily presence report.

The deployment target is 2 to 4 laptops on the same local network as the server. Nothing is hosted online.

The `Program.cs`, `Data/`, `Dto/`, `Helpers/`, `Hubs/`, `Models/` and `appsettings.json` at the **repo root** are leftovers from an earlier layout. Their `.csproj` has been deleted, so they are not compiled. Don't edit them by mistake. The same goes for `TraceProject-Backup.zip` and the empty `{Models,Data,...}` folder at the root, which was left by a failed shell brace expansion.

The exception is **`TraceProject/TraceProject/Models/EmployeeAccess.cs`**. It sits in an odd nested folder, but it is live code: the SDK compiles every `.cs` under the project directory, and `ApplicationDbContext` maps it. Nothing else defines `EmployeeAccess`.

The csproj still references `Microsoft.EntityFrameworkCore.Sqlite`, but only Npgsql is used.

There is no test project and no CI (`.github/workflows/` is empty), so `dotnet build` is the only automated check. The many `PHASE*_*.md` / `*_GUIDE.md` files are AI-generated progress notes and may be out of date. Trust the code over them.

## Commands

Run these from `TraceProject/`, the project directory:

```powershell
dotnet build
dotnet run                     # http profile: http://localhost:5297 (https profile: https://localhost:7209)
dotnet run --launch-profile lan   # http://0.0.0.0:5297, reachable by agents on other laptops
dotnet ef migrations add <Name> --output-dir Data/Migrations
dotnet ef database update
```

The agent, run from the repo root:

```powershell
dotnet build TraceAgent
# One exe to copy onto each laptop (Windows 10/11, no .NET needed):
dotnet publish TraceAgent -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o TraceAgent/publish
```

Other laptops reach the API only if Windows Firewall on the server allows inbound TCP 5297.

- Swagger UI is at `/swagger`. `TraceProject.http` holds sample requests.
- Startup runs `Database.Migrate()` and `SeedData.Initialize`, which seeds `admin@trace.com` / `P@ssw0rd` when the Users table is empty. A failed DB connection is logged, and the app keeps running.
- The Dockerfile and `DockerDefaultTargetOS` target **Windows**, because of the `OpenCvSharp4.runtime.win` and `VideoLAN.LibVLC.Windows` native packages.

## Database

- **PostgreSQL** (`Npgsql`), connection string `ConnectionStrings:DefaultConnection`. The README says SQLite, but that is outdated. `traceproject.db` is a leftover from the SQLite era.
- **Secrets are not in any committed file.** `ConnectionStrings:DefaultConnection`, `AppSettings:Token` (JWT key), `Camera:Username` and `Camera:Password` come from User Secrets (`UserSecretsId` is in the csproj; set them with `dotnet user-secrets set` in `TraceProject/`) or from environment variables outside Development. `Program.cs` fails fast if the token or connection string is missing. `appsettings.json` is git-ignored; `appsettings.example.json` is the committed template.
- `Data/ApplicationDbContextFactory.cs` (design-time `dotnet ef`) reads the same appsettings, User Secrets and environment variables.
- Migrations are split across two folders. `Migrations/` holds the initial Postgres migration **and the model snapshot**. `Data/Migrations/` (namespace `TraceProject.Data.Migrations`) holds later migrations. Check where the snapshot lives before adding a migration, so a second snapshot doesn't get created.
- Plain `DbContext`, not `IdentityDbContext`. Users (`Models/Account.cs`, `User : BasicUser`) and password hashing come from the private **`libs/HlkHelpers.dll`**, which also provides `BasicUserManager<User>`, `PaginationDto`, `Utils` and `SwaggerApiGrouping`. There is no source for it in this repo.

## Architecture

**REST API.** `Controllers/` are `[ApiController]`, `[Route("api/[controller]")]`, `[Authorize]` controllers. They inject `ApplicationDbContext` directly (there is no repository layer) and use DTOs from `Dtos/`. JWT bearer auth is configured in `Program.cs` from `AppSettings:{Token,Issuer,Audience}`. Controllers push real-time events such as `EmployeeCheckedIn` through `IHubContext<NotificationHub>` to the SignalR hub at `/hubs/notifications`. CORS origins come from `Cors:AllowedOrigins` (default `http://localhost:5173`, the frontend dev server: `C:\Users\ALIENWARE\TRACE\trace9-ui`, React + Vite, not in this repo). `UseHttpsRedirection` is skipped in Development so that frontend can use the http profile.

**Video → recognition → attendance pipeline.** Background services, all wired in `Program.cs`, each built in its own "phase":

1. `Workers/CameraCaptureWorker`, with `CameraConnectionManager` (reconnect/backoff, from `Connection` options), drives `IVideoSource` = `Services/VideoSources/RtspVideoSource` (LibVLCSharp). LibVLC memory-rendering callbacks decode frames as **BGRA**, capped at 1280 px wide and 15 fps, and only the latest frame is kept. It does nothing when `Camera:Enabled` is false or `Camera:Password` is empty.
2. `Services/FrameProcessingService`, a singleton `BackgroundService` that is also exposed as `IFrameProcessingService`, polls `GetLatestFrame()` at `TargetProcessingFps`, resizes the frame, and runs:
   - `IFaceDetector` → `YuNetFaceDetector` (`FaceDetectorYN`, ONNX model in `Models/FaceDetection/`)
   - `IFaceRecognizer` → `SFaceRecognizer` (`Models/FaceRecognition/`), matching against an in-memory cache of `FaceEmbeddings`. It refreshes the cache through its own DI scope, both after enrollment changes and every `EmbeddingCacheRefreshSeconds`, and reads its threshold from `SystemSettingsService`.
   - **Safeguards against misidentification:**
     - Side-profile faces (`DetectedFace.IsFrontal == false`, judged from the eye and nose landmarks) are not recognized, saved as unknown, or enrolled.
     - A match must beat the next-best employee by 0.06 (`AmbiguityMargin`); otherwise the result `IsAmbiguous` and is ignored.
     - An employee must be matched 3 times within 3 s (`FrameProcessingService.IsConfirmedSighting`) before attendance is touched.
   - on a match, `AttendancePersistenceService`, **per employee and per day** (unique index on `EmployeeId` + `WorkDate`, where `WorkDate` is the server-local date from `Services/WorkDay.cs`):
     - **Check-in** is the first recognition that day. It creates the row and broadcasts `EmployeeCheckedIn`.
     - **Check-out** is that employee's own last recognition that day. It is updated at most once a minute and broadcasts `AttendanceUpdated`.
     - Manual `POST /api/Attendances` also refuses a second row for the same employee and day.
   - no match: `UnknownFaceService` (singleton) saves an `UnknownFace` (JPEG, time, camera, location) when the face is clear enough (score ≥ 0.85, ≥ 60 px). The same stranger is de-duplicated in memory for 10 minutes. It broadcasts `UnknownFaceDetected`.
   - The location comes from `CameraLocationResolver`: `Camera:LocationId`, then the `Cameras` row's `LocationId`, then the first active location.
3. `FaceEnrollmentService` (scoped) detects, aligns, embeds and stores faces with a JPEG thumbnail (`FaceEmbedding.FaceImage`). It is exposed by `EmployeeFacesController` (`/api/Employees/{id}/faces`: list, `capture` from the live camera, `upload` a photo, delete) and used by `UnknownFacesController` register-employee and match-employee.
4. `CamerasController` serves the latest frame as MJPEG (`GET /api/Cameras/{id}/stream`) and as a JPEG (`/snapshot`), plus `GET /api/Cameras/status/live`. Only the camera whose DB id equals `Camera:Id` has a feed. Browsers can't set headers on `<img>`, so these two endpoints and the SignalR hub accept the JWT as `?access_token=` (`JwtBearerEvents.OnMessageReceived` in `Program.cs`). Request logging redacts it.

Both hosted services are wrapped in `Workers/SafeHostedServiceWrapper`, which swallows startup exceptions. They are registered with `AddSingleton<IHostedService>`, not `AddHostedService`: the latter de-duplicates by implementation type and would silently drop the second wrapper. Face detector and recognizer initialization failures in `Program.cs` are also only logged. **A broken pipeline usually shows up as log errors, not a crash**, so check the startup logs. The recognizer is optional in `FrameProcessingService`. Attendance is saved through a new DI scope per save (`IServiceScopeFactory`), because `AttendancePersistenceService` is scoped (DbContext). Don't inject scoped services into the singletons; Development scope validation rejects it.

`SystemSettingsService` (singleton) caches the single `SystemSettings` row: the recognition threshold, working hours and late grace. `SettingsController` (`/api/Settings`) edits it, and changes apply live. `SeedData` creates the admin user, a "General" department, a "Main Entrance" location, a `Cameras` row with id `Camera:Id` (then resets the identity sequence) and the settings row.

Each pipeline stage is configured through an options class in `Configuration/` bound to an `appsettings.json` section: `Camera`, `VideoProcessing`, `Connection`, `FaceDetection`, `FaceRecognition`. The `.onnx` models and `HlkHelpers.dll` are copied to the output directory by csproj rules. Model paths are relative to the output directory.

Models come from opencv_zoo: `face_detection_yunet_2023mar.onnx` runs through `cv::FaceDetectorYN` (it does YuNet decoding and NMS; OpenCvSharp has no `SetInputSize`, so the detector is recreated when the input size changes). `face_recognition_sface_2021dec.onnx` runs as a plain DNN net. OpenCvSharp doesn't expose `FaceRecognizerSF`, so the detector aligns each face to SFace's 112x112 five-landmark template itself. `DetectedFace.FaceImageData` is that aligned face as **raw packed BGR bytes**, not an encoded image. SFace input is raw 0–255 pixels with BGR→RGB (no mean/scale), and the match threshold is cosine ≥ 0.363 by default (OpenCV's recommended value; editable in Settings). Model files under 1 KB are treated as placeholders ("demo mode").

**Face quality gates** (`Services/FaceDetection/FaceQuality.cs`). These keep bad faces out of recognition and out of the gallery.
- **What the detector measures.** While aligning each face, the detector records:
  - `DetectedFace.CutOffFraction`: the share of the aligned face that came from outside the frame, found by warping an all-white mask with the same transform.
  - `Sharpness`: the variance of the Laplacian of the aligned face.
- **Recognition gate.** The detector sets `RecognitionProblem` from the `FaceDetection` options. `FrameProcessingService` skips any face that has one, and the metrics log counts skips per reason. Faces are skipped for:
  - a side profile
  - score < `MinRecognitionScore` (0.85): YuNet also fires on hands and fabric (a hand scored 0.74 and came within 0.008 of matching an employee)
  - size < 60 px
  - cut-off > 15 %
  - sharpness < 100
- **Enrollment gate** (`EnrollmentProblem` / `CropProblem`), stricter: score ≥ 0.9, ≥ 80 px, cut-off ≤ 10 %, sharpness ≥ 400.
- **Conflict check.** After that, `FaceEnrollmentService.EnrollCropAsync` calls `IFaceRecognizer.CompareWithGallery`.
  - It **refuses** a face that already matches another employee at or above the threshold.
  - It warns when the face is within 0.05 of that, or when it resembles the employee's own faces at less than 0.2.
  - Warnings come back in `EnrollmentResult.Error` on a successful enroll.
- **Gallery audit.** `GET /api/Employees/{id}/faces/audit` rates each enrolled face (`Good` / `Weak` / `CloseToOther` / `Unusable`) from its stored embedding and thumbnail. The Face Profiles tab shows the verdicts.
- **Alignment stays LMEDS** (`EstimateAffinePartial2D`). A least-squares (Umeyama) fit was compared on 174 real faces and gave no gain; switching would also make new embeddings inconsistent with the enrolled ones.

PostgreSQL `timestamptz` columns reject `DateTime` values without a timezone. Clients must send ISO dates with `Z` or an offset.

## Desktop monitoring (TraceAgent)

**Pairing.**
1. An admin calls `POST /api/Devices` with `{employeeId, name}` and gets an 8-character pairing code that expires after `PairingCodeMinutes`.
2. On the laptop, the agent's first-run `PairForm` sends the code to `POST /api/Agent/pair` and receives a device token.
3. The server stores only the token's SHA-256 hash (`Device.TokenHash`). The agent stores the token DPAPI-encrypted in `%LOCALAPPDATA%\TraceAgent\settings.json` and adds itself to the HKCU `Run` key.

`POST /api/Devices/{id}/pairing-code` issues a new code and **revokes the current token**. Deleting the device also cuts the agent off: on a 401 the agent clears its token and shows the pair form again. A **disabled** device still authenticates, but `POST /api/Agent/report` answers 403. The agent keeps its token, drops what it recorded and shows "paused", then resumes by itself once the device is re-enabled. Idle time is never dated back before the agent started sampling (at agent start or wake from sleep).

**Two auth schemes.** `AgentController` accepts only `Authorization: Device <token>` (`Services/DeviceTokenAuthentication.cs`, scheme `DeviceTokenAuthenticationHandler.SchemeName` = "DeviceToken"). JWT stays the default for every other controller. Neither token works on the other side.

**Sessions.**
- `ActivityTracker` samples once a second:
  - the foreground window (process `FileDescription`, falling back to the process name; Store apps use their title instead of `ApplicationFrameHost`)
  - `GetLastInputInfo` idle time
  - lock and unlock (`SystemEvents.SessionSwitch`)
- It starts a new `ActivitySession` whenever the state (`Active`/`Idle`/`Locked`), the app or the title changes. Idle sessions carry no app or title.
- Going idle is detected only after the threshold, so the active session is **backdated** to end at the last input. Sleep (a gap of more than 30 s between samples) or suspend closes the session where sampling stopped.

**Upload.**
- Every `ReportIntervalSeconds`, `Uploader` posts finished sessions plus the open one to `POST /api/Agent/report`. The open one is resent each time with a growing `EndedAt`.
- The server upserts by `(DeviceId, ClientId)` and **overwrites `EndedAt`**, which may move backwards because of the backdating. Don't change that to "max".
- Unsent sessions persist in `%LOCALAPPDATA%\TraceAgent\pending.json`.
- **Clock skew.** Times more than 2 minutes in the future are dropped, so `ActivityIngestionService.ClockOffset` shifts each report onto the server clock first:
  - Agents 1.1.0+ send `SentAt`, which gives the exact offset.
  - For older agents the newest `EndedAt` stands in for the agent's "now". That only catches a clock that runs ahead.
  - A skew over 2 minutes logs a warning (`The clock on {device} is ... ahead of the server`). One laptop was 138 s ahead, and every session it sent was silently lost until this correction existed.
- The agent fetches `IdleThresholdSeconds`, `ReportIntervalSeconds` and `CaptureWindowTitles` from `GET /api/Agent/me`, so the `ActivityMonitoring` section on the server controls every agent.

**Server side.**
- `ActivityIngestionService` (scoped) stores sessions with `WorkDate = WorkDay.Of(StartedAt)`.
- It updates the per-employee live `Activity` row (`State`, `IsActive`, `CurrentApp`, `LastSeenAt`, ...) and broadcasts `ActivityUpdated` only when the state or app changes.
- `Workers/DeviceStatusWatcher` sets the row to `Offline` after `OfflineAfterSeconds` without a report. `Offline` is a live state only; it is never stored as a session.
- The dashboard reads `GET /api/Activities/employee/{id}/summary?date=yyyy-MM-dd` (active, idle and locked seconds, plus top apps) and `.../sessions?date=` (timeline). Time on several devices at the same moment is added up.
- `TraceAgent/AgentApi.cs` keeps a **copy** of the contracts in `TraceProject/Dtos/AgentDtos.cs`. Change both together. Enums travel as numbers.

**Lifecycle events (exit detection).**
- The agent records `AgentEvent`s and queues and persists them with its sessions (`pending.json` is `{Sessions, Events}`; a bare list from agent 1.0.0 is still read). The kinds are:
  - `Started`
  - `UserExit`: the tray **Exit**, which asks for confirmation first
  - `Shutdown`: Windows `SessionEnding`
  - `Sleep` and `Resumed`
- They go in the last batch of a report. If the **newest** event is `UserExit`, `Shutdown` or `Sleep`, ingestion sets the live row to `Offline` with `Activity.OfflineReason` at once. A `UserExit` less than 10 minutes old also broadcasts `AgentStopped`. Events queued while offline are stored as history only.
- `DeviceStatusWatcher` writes a server-side `ConnectionLost` event (`ClientId` null) for a silent device: killed, crashed, or the network gone.
- `GET /api/Activities/employee/{id}/events?date=` lists a day's events.

## Presence (camera + computer combined)

`Services/PresenceService.cs` (scoped) joins, per active employee and server-local work day:
- today's `Attendance` row (the camera check-in, with `CheckOut` = last recognition)
- the live `Activity` row (computer state)
- that day's `ActivitySessions`

`PresenceService.StatusOf` is the single rule:

| Camera check-in today | Computer state | Status |
|---|---|---|
| yes | Active | `Working` |
| yes | Idle or Locked | `PresentIdle` |
| yes | Offline or none | `PresentNoComputer` |
| no | Active | `Unverified` |
| no | anything else | `Absent` |

**Endpoints.**
- `GET /api/Presence` returns the live board.
- `GET /api/Presence/daily?date=` returns one row per employee with an outcome (`Verified` / `PresentNoComputer` / `UnverifiedComputerUse` / `Absent`) and `VerifiedActiveSeconds`: active computer time after the camera check-in. Each row also has `IsLate` (Settings start time + grace, the same rule as the dashboard) and `AgentClosedByUser`.
- `GET /api/Presence/range?from=&to=` is the week/month report (at most 93 days). It returns per-employee day counts, late days, average check-in, time totals and agent closes, plus per-day totals for charts.
  - Days after today are dropped.
  - **Absence counts Monday to Friday only** (`PresenceService.IsWorkingDay`).
  - All periods share `BuildDay` over data loaded once by `LoadRangeAsync`.
- The legacy Reports page and `ReportsController` only store metadata rows (type and dates). The actual reports are on the Presence page (Day / Week / Month).

**Live updates.**
- `Workers/PresenceMonitor` (a singleton `BackgroundService`) keeps the last status per employee and broadcasts `PresenceChanged` `{employeeId, employeeName, status, previousStatus, ...}` on every change.
- It recomputes every 15 s, which covers manual attendance edits and midnight, and immediately when `Poke()`d. `AttendancePersistenceService` (check-in), `ActivityIngestionService` (state or app change) and `DeviceStatusWatcher` (offline) call `Poke()`.
- Its first pass only records statuses and broadcasts nothing.
- Statuses are computed, never stored: change the rule in `StatusOf` and every view follows.
