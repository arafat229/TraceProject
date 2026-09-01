using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection.Metadata;
using System.Text;
using TraceProject.Data;
using TraceProject.Helpers;
using TraceProject.Model;
using TraceProject.Hubs;
using HlkHelpers;
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) =>
lc.WriteTo.Console()
.ReadFrom.Configuration(ctx.Configuration));

// Add services to the container.

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Trace API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {

        Description = "Jwt Authentication, Login with crds",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",

    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(document =>
    new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });

});

builder.Services.AddControllers();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password = new PasswordOptions()
    {
        RequireDigit = true,
        RequireNonAlphanumeric = true,
        RequireLowercase = true,
        RequireUppercase = true,
        RequiredLength = 8,
        RequiredUniqueChars = 4

    };
    options.Lockout = new LockoutOptions()
    {
        DefaultLockoutTimeSpan = TimeSpan.FromMinutes(50),
        MaxFailedAccessAttempts = 5,
        AllowedForNewUsers = true

    };
    options.User = new UserOptions()
    {
        AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+",
        RequireUniqueEmail= true 

    };
});

builder.Services.AddIdentityCore<User>()
 .AddRoles<Role>()
 .AddEntityFrameworkStores<ApplicationDbContext>().AddSignInManager<SignInManager<User>>()
 .AddDefaultTokenProviders().AddRoleValidator<RoleValidator<Role>>()
 .AddRoleManager<RoleManager<Role>>();


var secret = builder.Configuration["Appsettings:Token"];

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).
    AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["AppSettings:Issuer"],
            ValidAudience = builder.Configuration["AppSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes($"{secret}-{secret}")),
            ClockSkew = TimeSpan.FromSeconds(6),
        };
    });
builder.Services.AddSingleton<BasicUserManager<CompanyUser>>();
builder.Services.AddScoped<SeedData>();
builder.Services.AddAuthorization(options =>
{

    //options.AddPolicy("AdminOrManager", policy => policy.RequireRole(Constants.SuperAdminRole, Constants.ManagerRole));
    //options.AddPolicy("AdminOnly", policy => policy.RequireRole(Constants.SuperAdminRole));
    //options.AddPolicy("AdminOrManagerOrSupervisor", policy => policy.RequireRole(Constants.ManagerRole, Constants.SuperAdminRole, Constants.SupervisorRole));


});

builder.Services.AddCors(options =>
{
    options.AddPolicy("TraceFrontend", policy =>
    {

        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });

});

builder.Services.AddSignalR();


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();


var app = builder.Build();


await SeedData.Initialize(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.UseCors("TraceFrontend");

app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();

app.MapHub<TraceProject.Hubs.NotificationHub>("/hubs/notifications");

app.Run();
