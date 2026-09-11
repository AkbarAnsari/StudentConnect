using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data;
using StudentConnect.Api.Notifications;
using StudentConnect.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// EF Core needs a connection string to know which database to talk to.
builder.Services.AddDbContext<StudentConnectDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Today files live on local disk; a future AzureBlobStorage class could implement the
// same IFileStorage interface without any controller/service code changing.
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.Configure<AzureBlobStorageOptions>(builder.Configuration.GetSection("AzureBlobStorage"));

// Storage:Provider selects the IFileStorage implementation — "Local" (default, for
// day-to-day development) or "AzureBlob" (see docs/phase-13.md). Controllers depend
// only on IFileStorage, so they never need to know which one is active.
var storageProvider = builder.Configuration["Storage:Provider"] ?? "Local";
if (string.Equals(storageProvider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IFileStorage, AzureBlobStorage>();
}
else
{
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
}

// Development-only JWT authentication — see docs/phase-10.md for what a production
// identity provider (e.g. Microsoft Entra ID) would replace this with.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
        };
    });

// Every endpoint requires an authenticated caller unless explicitly marked [AllowAnonymous]
// (only the login action is) — the API is the real security boundary, not the Angular app.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// The Angular app runs on a different origin (port/host) than this API, so the
// browser blocks calls unless we explicitly allow it via CORS. AllowedOrigins in
// config covers deployed environments (e.g. the App Service frontend URL); the local
// dev server origin is always allowed too so `ng serve` keeps working unmodified.
const string AngularDevOrigin = "http://localhost:4200";
var configuredOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
var corsOrigins = configuredOrigins.Append(AngularDevOrigin).Distinct().ToArray();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Apply any pending EF Core migrations on startup so the database schema is
// always up to date with our C# models — beginner-friendly, no manual step needed.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudentConnectDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AngularDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
