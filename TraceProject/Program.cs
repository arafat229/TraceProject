
using HlkHelpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using TraceProject.Configuration;
using TraceProject.Data;
using TraceProject.Hubs;
using TraceProject.Models;
using TraceProject.Services;
using TraceProject.Services.Interfaces;
using TraceProject.Services.FaceDetection;
using TraceProject.Services.FaceRecognition;
using TraceProject.Services.VideoSources;
using TraceProject.Workers;


try
{
    //Log.Information("Starting application ...");

    var builder = WebApplication.CreateBuilder(args);

    //builder.Host.UseSerilog((ctx, lc) => {
    //    lc.ReadFrom.Configuration(ctx.Configuration);
    //});

    builder.Host.UseSerilog((ctx, lc) => lc
   .WriteTo.Console()
   .ReadFrom.Configuration(ctx.Configuration));


    var secret = builder.Configuration.GetSection("AppSettings:Token").Value ?? "";

    // Secrets live in User Secrets (Development) or environment variables, never in appsettings.json
    if (string.IsNullOrWhiteSpace(secret))
        throw new InvalidOperationException(
            "AppSettings:Token is not set. Run in the TraceProject folder: dotnet user-secrets set \"AppSettings:Token\" \"<long random string>\"");
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is not set. Run in the TraceProject folder: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"Host=localhost;Port=5432;Database=TraceProject;Username=postgres;Password=...\"");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        // Use PostgreSQL
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
        // Uncomment for debugging:
        // options.LogTo(Console.WriteLine);
        // options.EnableDetailedErrors(true);
        // options.EnableSensitiveDataLogging(true);
    });


    builder.Services.Configure<IdentityOptions>(options =>
    {
        // Password settings.
        options.Password = new PasswordOptions()
        {
            RequireDigit = false,
            RequiredLength = 5,
            RequireLowercase = false,
            RequireUppercase = false,
            RequireNonAlphanumeric = false,
            RequiredUniqueChars = 1
        };

        // Lockout settings.
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(600);
        options.Lockout.MaxFailedAccessAttempts = 50;
        options.Lockout.AllowedForNewUsers = true;

        // User settings.
        options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._+?!@#$%^&*";
        options.User.RequireUniqueEmail = false;

    });

    JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear(); // => remove default claims


    // configure token generation
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
          //
          options.RequireHttpsMetadata = false;
          options.SaveToken = true;
          options.TokenValidationParameters = new TokenValidationParameters
          {
              ValidateIssuerSigningKey = true,
              IssuerSigningKey = new SymmetricSecurityKey(Utils.StringToByteArray(secret)),
              //
              ValidateIssuer = true,
              ValidIssuer = builder.Configuration["AppSettings:Issuer"],
              //
              ValidateAudience = true,
              AudienceValidator = (aud, token, valdationParam) => aud.All(x => x == builder.Configuration["AppSettings:Audience"] || (x.Length == 15 && long.TryParse(x, out _))),
              //ValidAudience = builder.Configuration["AppSettings:Audience"],
              ValidateLifetime = true,
              ClockSkew = TimeSpan.Zero // remove delay of token when expire
          };

          // Browsers cannot send an Authorization header on WebSocket/SSE connections or <img> tags,
          // so SignalR and the camera stream pass the JWT as ?access_token= instead.
          options.Events = new JwtBearerEvents
          {
              OnMessageReceived = context =>
              {
                  var accessToken = context.Request.Query["access_token"];
                  var path = context.HttpContext.Request.Path;
                  var isCameraFeed = path.StartsWithSegments("/api/Cameras", StringComparison.OrdinalIgnoreCase)
                      && (path.Value!.EndsWith("/stream", StringComparison.OrdinalIgnoreCase)
                          || path.Value.EndsWith("/snapshot", StringComparison.OrdinalIgnoreCase));

                  if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs") || isCameraFeed))
                  {
                      context.Token = accessToken;
                  }
                  return Task.CompletedTask;
              }
          };

      })
      // Desktop agent (TraceAgent): "Authorization: Device <token>", only accepted by AgentController
      .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DeviceTokenAuthenticationHandler>(
          DeviceTokenAuthenticationHandler.SchemeName, null);


    builder.Services.AddAuthorization();

    builder.Services.AddEndpointsApiExplorer();
    // needed for swagger api versioning
    builder.Services.AddControllers(options => options.Conventions.Add(new SwaggerApiGrouping()));

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "TraceProject API", Version = "v1" });

        var secScheme = new OpenApiSecurityScheme()
        {
            Description = "Jwt Authorization: login with creds and copy and paste jwt token into box below",
            Name = "Authorization",
            In = ParameterLocation.Header,
            BearerFormat = "JWT",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
        };

        c.AddSecurityDefinition("Bearer", secScheme);

        c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("Bearer", doc), Array.Empty<string>().ToList() }
        });
    });



    builder.Services.AddSingleton<BasicUserManager<User>>();

    builder.Services.AddSignalR();

    // Register camera configuration options
    builder.Services.Configure<CameraOptions>(builder.Configuration.GetSection("Camera"));
    builder.Services.Configure<VideoProcessingOptions>(builder.Configuration.GetSection("VideoProcessing"));
    builder.Services.Configure<ConnectionOptions>(builder.Configuration.GetSection("Connection"));
    builder.Services.Configure<FaceDetectionOptions>(builder.Configuration.GetSection("FaceDetection"));
    builder.Services.Configure<FaceRecognitionOptions>(builder.Configuration.GetSection("FaceRecognition"));
    builder.Services.Configure<ActivityMonitoringOptions>(builder.Configuration.GetSection("ActivityMonitoring"));

    // Both hosted services are wrapped in SafeHostedServiceWrapper. They are registered with
    // AddSingleton<IHostedService> because AddHostedService de-duplicates by implementation
    // type and would silently drop the second wrapper (FrameProcessingService).
    // Register camera services (Phase 1 + Phase 2: RTSP video capture with metrics)
    builder.Services.AddSingleton<ICameraMetricsService, CameraMetricsService>();  // Phase 2: Metrics service
    builder.Services.AddSingleton<IVideoSource, RtspVideoSource>();
    builder.Services.AddSingleton<CameraStateService>();
    builder.Services.AddSingleton<IHostedService>(sp =>
    {
        var worker = new CameraCaptureWorker(
            sp,
            sp.GetRequiredService<ILogger<CameraCaptureWorker>>(),
            sp.GetRequiredService<IOptions<CameraOptions>>());
        return new SafeHostedServiceWrapper(
            worker,
            sp.GetRequiredService<ILogger<SafeHostedServiceWrapper>>(),
            "CameraCaptureWorker");
    });

    // Register face detection services (Phase 3: Real face detector via YuNet)
    // Use factory to catch any initialization errors
    builder.Services.AddSingleton<IFaceDetector>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        try
        {
            logger.LogDebug("Creating YuNetFaceDetector instance");
            var options = sp.GetRequiredService<IOptions<FaceDetectionOptions>>();
            var faceDetectorLogger = sp.GetRequiredService<ILogger<YuNetFaceDetector>>();
            var detector = new YuNetFaceDetector(options, faceDetectorLogger);
            logger.LogDebug("✓ YuNetFaceDetector created successfully");
            return detector;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create YuNetFaceDetector. This might be due to missing model files or invalid configuration.");
            throw;
        }
    });

    // Register face recognition services (Phase 4: Face recognition via SFace)
    // Use factory to catch any initialization errors
    builder.Services.AddSingleton<IFaceRecognizer>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        try
        {
            logger.LogDebug("Creating SFaceRecognizer instance");
            var options = sp.GetRequiredService<IOptions<FaceRecognitionOptions>>();
            var faceRecognizerLogger = sp.GetRequiredService<ILogger<SFaceRecognizer>>();
            var recognizer = new SFaceRecognizer(options, faceRecognizerLogger,
                sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<SystemSettingsService>());
            logger.LogDebug("✓ SFaceRecognizer created successfully");
            return recognizer;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create SFaceRecognizer. This might be due to missing model files or invalid configuration.");
            throw;
        }
    });

    builder.Services.AddScoped<FaceEnrollmentService>();  // For enrollment operations
    builder.Services.AddSingleton<SystemSettingsService>();  // Settings page values, cached in memory
    builder.Services.AddSingleton<UnknownFaceService>();     // Saves unmatched faces for review

    // Desktop monitoring agent: report ingestion, and Offline detection for silent devices
    builder.Services.AddScoped<ActivityIngestionService>();
    builder.Services.AddHostedService<DeviceStatusWatcher>();

    // Register attendance persistence service (Phase 5: Store recognition results as attendance)
    builder.Services.AddScoped<IAttendancePersistenceService, AttendancePersistenceService>();

    // Register FrameProcessingService as singleton for both interface and hosted service use
    builder.Services.AddSingleton<FrameProcessingService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<FrameProcessingService>>();
        try
        {
            logger.LogDebug("Resolving FrameProcessingService dependencies");
            var videoSource = sp.GetRequiredService<IVideoSource>();
            logger.LogDebug("✓ IVideoSource resolved");

            var faceDetector = sp.GetRequiredService<IFaceDetector>();
            logger.LogDebug("✓ IFaceDetector resolved");

            var videoOptions = sp.GetRequiredService<IOptions<VideoProcessingOptions>>();
            logger.LogDebug("✓ IOptions<VideoProcessingOptions> resolved");

            var faceRecognizer = sp.GetService<IFaceRecognizer>();
            logger.LogDebug("✓ IFaceRecognizer resolved (optional: {hasValue})", faceRecognizer != null);

            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            var cameraOptions = sp.GetService<IOptions<CameraOptions>>();
            logger.LogDebug("✓ IOptions<CameraOptions> resolved (optional: {hasValue})", cameraOptions != null);

            logger.LogDebug("All dependencies for FrameProcessingService resolved successfully");
            return new FrameProcessingService(
                videoSource,
                faceDetector,
                videoOptions,
                logger,
                faceRecognizer,
                scopeFactory,
                cameraOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve dependencies for FrameProcessingService");
            throw;
        }
    });
    builder.Services.AddSingleton<IFrameProcessingService>(sp => sp.GetRequiredService<FrameProcessingService>());
    builder.Services.AddSingleton<IHostedService>(sp =>
    {
        var frameProcessor = sp.GetRequiredService<FrameProcessingService>();
        return new SafeHostedServiceWrapper(
            frameProcessor,
            sp.GetRequiredService<ILogger<SafeHostedServiceWrapper>>(),
            "FrameProcessingService");
    });

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddCors();

    // Frontend origins allowed to call the API (Cors:AllowedOrigins in appsettings)
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } origins
        ? origins
        : new[] { "http://localhost:5173" };



    Log.Information("Building web application host...");
    WebApplication app;
    try
    {
        app = builder.Build();
        Log.Information("Web application host built successfully");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to build web application host. This usually indicates a dependency injection or configuration error.");
        throw;
    }

    var env = app.Environment;

    app.UseSerilogRequestLogging(option =>
    {
        option.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "");
            diagnosticContext.Set("RequestFullPath", RedactAccessToken(httpContext.Request.GetDisplayUrl()));
            diagnosticContext.Set("RequestQueryString", RedactAccessToken(httpContext.Request.QueryString.Value));
        };
        // Query strings can carry the JWT (?access_token=), so they are logged only via the redacted property above
        option.IncludeQueryInRequestPath = false;

    });

    //// run migrations and seed data
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Attempting to connect to database and run migrations...");
            context.Database.Migrate();
            logger.LogInformation("Database migrations completed successfully");

            await SeedData.Initialize(scope.ServiceProvider);
            await scope.ServiceProvider.GetRequiredService<SystemSettingsService>().LoadAsync();
            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Database initialization failed. Please ensure:");
            logger.LogError("1. PostgreSQL is running on localhost:5432");
            logger.LogError("2. Database 'TraceProject' exists");
            logger.LogError("3. ConnectionStrings:DefaultConnection in User Secrets has the right credentials");
            logger.LogError("Application will continue but database features will not work");
        }
    }

    // Initialize Phase 3: Face detector
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var faceDetector = scope.ServiceProvider.GetRequiredService<IFaceDetector>();
            if (faceDetector is YuNetFaceDetector yunetDetector)
            {
                await yunetDetector.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to initialize face detector on startup");
            // Don't rethrow - allow app to continue without face detection
            // Phase 3 is not critical for Phase 1/2 to work
        }
    }

    // Initialize Phase 4: Face recognizer
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var faceRecognizer = scope.ServiceProvider.GetRequiredService<IFaceRecognizer>();
            if (faceRecognizer is SFaceRecognizer sfaceRecognizer)
            {
                await sfaceRecognizer.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Failed to initialize face recognizer on startup");
            // Don't rethrow - allow app to continue without face recognition
            // Phase 4 is optional; app will still work with detection only
        }
    }



    //// Configure the HTTP request pipeline.
    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.MapOpenApi();

    app.UseRouting();

    app.UseCors(x => x
     .WithOrigins(allowedOrigins)
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials());

    // In development the frontend talks to the plain-http profile (localhost:5297); redirecting
    // there would break CORS preflight requests, which cannot follow redirects.
    if (!env.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    //the order of this 2 below matters, u have to authenticate before authorize
    app.UseAuthentication();
    app.UseAuthorization();

    // Map SignalR hub for real-time notifications
    app.MapHub<NotificationHub>("/hubs/notifications");

    app.UseSwagger();

    app.UseSwaggerUI(c =>
    {
        //c.RoutePrefix = "docs";
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TraceProject API Docs V1");
    });

    app.MapControllers();

    app.Run();

}
catch (Exception ex)
{
    // Print to console FIRST before trying to use Log
    Console.WriteLine("=============================================================");
    Console.WriteLine("CRITICAL ERROR DURING APPLICATION STARTUP");
    Console.WriteLine("=============================================================");
    Console.WriteLine($"Exception Type: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"Stack Trace:");
    Console.WriteLine(ex.StackTrace);

    if (ex is AggregateException aggEx)
    {
        Console.WriteLine($"\nAggregateException with {aggEx.InnerExceptions.Count} inner exceptions:");
        int i = 1;
        foreach (var innerEx in aggEx.InnerExceptions)
        {
            Console.WriteLine($"\n--- Inner Exception #{i} ---");
            Console.WriteLine($"Type: {innerEx.GetType().Name}");
            Console.WriteLine($"Message: {innerEx.Message}");
            Console.WriteLine($"Stack: {innerEx.StackTrace}");
            i++;
        }
    }

    Console.WriteLine("=============================================================");

    // Then try Serilog
    // Handle AggregateException to show all inner exceptions
    if (ex is AggregateException aggExLog)
    {
        Log.Fatal("AggregateException with {count} inner exceptions:", aggExLog.InnerExceptions.Count);
        foreach (var innerEx in aggExLog.InnerExceptions)
        {
            Log.Fatal(innerEx, "Inner exception: {message}", innerEx.Message);
        }
    }
    else
    {
        Log.Fatal(ex, "Unhandled exception");
    }
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

static string RedactAccessToken(string? queryString) =>
    System.Text.RegularExpressions.Regex.Replace(queryString ?? "", "(access_token=)[^&]*", "$1***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
