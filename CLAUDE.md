# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project layout

ASP.NET Core (.NET 10) Web API for employee attendance tracking through face recognition on an RTSP camera feed. The solution file `TraceProject.slnx` references a single project, **`TraceProject/TraceProject.csproj`**. All live code is under `TraceProject/`.

The `Program.cs`, `Data/`, `Dto/`, `Helpers/`, `Hubs/`, `Models/` and `appsettings.json` at the **repo root** are leftovers from an earlier layout. Their `.csproj` has been deleted, so they are not compiled. Don't edit them by mistake. The same goes for `TraceProject-Backup.zip`.

There is no test project. The many `PHASE*_*.md` / `*_GUIDE.md` files are AI-generated progress notes and may be out of date. Trust the code over them.

## Commands

Run these from `TraceProject/`, the project directory:

```powershell
dotnet build
dotnet run                     # http profile: http://localhost:5297 (https profile: https://localhost:7209)
dotnet ef migrations add <Name> --output-dir Data/Migrations
dotnet ef database update
```

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

**REST API.** `Controllers/` are `[ApiController]`, `[Route("api/[controller]")]`, `[Authorize]` controllers. They inject `ApplicationDbContext` directly (there is no repository layer) and use DTOs from `Dtos/`. JWT bearer auth is configured in `Program.cs` from `AppSettings:{Token,Issuer,Audience}`. Controllers push real-time events such as `EmployeeCheckedIn` through `IHubContext<NotificationHub>` to the SignalR hub at `/hubs/notifications`. CORS allows only `http://localhost:5173`, the frontend dev server (`C:UsersALIENWARETRACE	race9-ui`, React + Vite). `UseHttpsRedirection` is skipped in Development so that frontend can use the http profile.

**Video → recognition → attendance pipeline.** Background services, all wired in `Program.cs`, each built in its own "phase":

1. `Workers/CameraCaptureWorker`, with `CameraConnectionManager` (reconnect/backoff, from `Connection` options), drives `IVideoSource` = `Services/VideoSources/RtspVideoSource` (LibVLCSharp). LibVLC memory-rendering callbacks decode frames as **BGRA**, capped at 1280 px wide and 15 fps, and only the latest frame is kept. It does nothing when `Camera:Enabled` is false or `Camera:Password` is empty.
2. `Services/FrameProcessingService`, a singleton `BackgroundService` that is also exposed as `IFrameProcessingService`, polls `GetLatestFrame()` at `TargetProcessingFps`, resizes the frame, and runs:
   - `IFaceDetector` → `YuNetFaceDetector` (`FaceDetectorYN`, ONNX model in `Models/FaceDetection/`)
   - `IFaceRecognizer` → `SFaceRecognizer` (`Models/FaceRecognition/`), matching against an in-memory cache of `FaceEmbeddings` loaded in `InitializeAsync(dbContext)` and refreshed every `EmbeddingCacheRefreshSeconds`
   - on a match, `IAttendancePersistenceService.PersistRecognitionResultAsync`, which writes `Attendance` rows with a per-employee `Attendance:CooldownMinutes`
3. `FaceEnrollmentService` (scoped) computes and stores embeddings for employees.
4. `CamerasController` serves the latest frame as MJPEG (`GET /api/Cameras/{id}/stream`) and as a JPEG (`/snapshot`), plus `GET /api/Cameras/status/live`. Only the camera whose DB id equals `Camera:Id` has a feed. Browsers can't set headers on `<img>`, so these two endpoints and the SignalR hub accept the JWT as `?access_token=` (`JwtBearerEvents.OnMessageReceived` in `Program.cs`). Request logging redacts it.

Both hosted services are wrapped in `Workers/SafeHostedServiceWrapper`, which swallows startup exceptions. They are registered with `AddSingleton<IHostedService>`, not `AddHostedService`: the latter de-duplicates by implementation type and would silently drop the second wrapper. Face detector and recognizer initialization failures in `Program.cs` are also only logged. **A broken pipeline usually shows up as log errors, not a crash**, so check the startup logs. The recognizer is optional in `FrameProcessingService`. Attendance is saved through a new DI scope per save (`IServiceScopeFactory`), because `AttendancePersistenceService` is scoped (DbContext). Don't inject scoped services into the singletons; Development scope validation rejects it.

Each pipeline stage is configured through an options class in `Configuration/` bound to an `appsettings.json` section: `Camera`, `VideoProcessing`, `Connection`, `FaceDetection`, `FaceRecognition`, `Attendance`. The `.onnx` models and `HlkHelpers.dll` are copied to the output directory by csproj rules. Model paths are relative to the output directory.

Models come from opencv_zoo: `face_detection_yunet_2023mar.onnx` runs through `cv::FaceDetectorYN` (it does YuNet decoding and NMS; OpenCvSharp has no `SetInputSize`, so the detector is recreated when the input size changes). `face_recognition_sface_2021dec.onnx` runs as a plain DNN net. OpenCvSharp doesn't expose `FaceRecognizerSF`, so the detector aligns each face to SFace's 112x112 five-landmark template itself. `DetectedFace.FaceImageData` is that aligned face as **raw packed BGR bytes**, not an encoded image. SFace input is raw 0–255 pixels with BGR→RGB (no mean/scale), and the match threshold is cosine ≥ 0.363 (OpenCV's recommended value). Model files under 1 KB are treated as placeholders ("demo mode").

PostgreSQL `timestamptz` columns reject `DateTime` values without a timezone. Clients must send ISO dates with `Z` or an offset.
