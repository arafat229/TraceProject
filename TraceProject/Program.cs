
using HlkHelpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using TraceProject.Data;
using TraceProject.Models;


try
{
    //Log.Information("Starting application ...");

    var builder = WebApplication.CreateBuilder(args);

    //builder.Host.UseSerilog((ctx, lc) => {
    //    lc.ReadFrom.Configuration(ctx.Configuration);
    //});

    var secret = builder.Configuration.GetSection("AppSettings:Token").Value ?? "";

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        // switch provider to SQLite (local file-based DB)
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
        options.LogTo(Log.Debug);
        options.EnableDetailedErrors(true);
        //options.EnableSensitiveDataLogging(true);
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

      });


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


    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddCors();



    var app = builder.Build();
    var env = app.Environment;

    //// run migrations and seed data
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();
            await SeedData.Initialize(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex.Message, "An error occurred seeding the DB.");
            logger.LogError(ex, "{action} failed: {error}", "seedData", ex.GetFullErrorMessage());
        }
    }



    //// Configure the HTTP request pipeline.
    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.MapOpenApi();

    app.UseRouting();

    app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    app.UseHttpsRedirection();

    //the order of this 2 below matters, u have to authenticate before authorize
    app.UseAuthentication();
    app.UseAuthorization();

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
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}


