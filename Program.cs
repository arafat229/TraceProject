using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TraceProject.Data;
using TraceProject.Hubs;
using TraceProject.Models;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------------------
// 1. DATABASE: register EF Core with SQL Server, using the connection string
//    from appsettings.json. This registers ApplicationDbContext so it can be
//    "injected" into any controller's constructor (that's how the
//    AccountController you showed me got `ApplicationDbContext dbContext`
//    for free — ASP.NET Core creates and hands it one automatically).
// ----------------------------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ----------------------------------------------------------------------------
// 2. IDENTITY: registers user/role management (UserManager, RoleManager,
//    SignInManager — the three services your AccountController already uses).
//    <User, Role> tells Identity to use OUR custom classes instead of its
//    generic defaults, so we can add fields like FullName and IsActive.
// ----------------------------------------------------------------------------
builder.Services.AddIdentity<User, Role>(options =>
    {
        // Password rules enforced automatically on every CreateAsync call.
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>() // tells Identity to save users/roles via EF Core
    .AddDefaultTokenProviders();                       // needed for password-reset tokens later

// ----------------------------------------------------------------------------
// 3. JWT AUTHENTICATION: this is what makes [Authorize] on a controller
//    actually check the token your React frontend sends. Every rule here
//    matches something GenerateJwtToken() puts INTO the token, so the two
//    must always agree.
// ----------------------------------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero, // don't give expired tokens a few extra minutes of grace
        };
    });

// ----------------------------------------------------------------------------
// 4. AUTHORIZATION POLICIES: this is what [Authorize("AdminOrManager")] in
//    your AccountController refers to. Without defining it here, that
//    attribute would throw an exception at startup.
// ----------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOrManager", policy =>
        policy.RequireRole("Administrator", "HROfficer"));

    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Administrator"));
});

// ----------------------------------------------------------------------------
// 5. CORS: by default, browsers block your React app (localhost:5173) from
//    calling your API (localhost:5000-ish) because they're different "origins".
//    This explicitly allows your frontend's dev server through. You'll add
//    your real deployed frontend URL here too once you host it.
// ----------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("TraceFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Vite's default dev port
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // required for SignalR to work cross-origin
    });
});

// ----------------------------------------------------------------------------
// 6. SIGNALR: registers the real-time hub infrastructure. The actual hub
//    class (NotificationsHub) is mapped further down, after the app is built.
// ----------------------------------------------------------------------------
builder.Services.AddSignalR();

// Standard controller + Swagger setup.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ----------------------------------------------------------------------------
// MIDDLEWARE PIPELINE: order matters here. Each request passes through these
// top-to-bottom, so authentication must happen before authorization, and CORS
// must be set up before anything that needs to respond to the frontend's calls.
// ----------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(); // browse to /swagger to test endpoints interactively
}

app.UseHttpsRedirection();

app.UseCors("TraceFrontend");   // must come before Authentication/Authorization

app.UseAuthentication();        // "who are you?" — reads and validates the JWT
app.UseAuthorization();         // "are you allowed to do this?" — checks [Authorize]/roles

app.MapControllers();           // wires up every [ApiController] you write

app.MapHub<NotificationsHub>("/hubs/notifications"); // matches the URL the React SignalR client connects to

app.Run();
