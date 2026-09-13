using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Auditing;
using StudentConnect.Api.Data;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Notifications;
using StudentConnect.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Plain ADO.NET (Npgsql) data access — no ORM. See database/schema.sql for the hand-written
// schema and Data/Stores/*.cs for the SQL each controller calls into.
builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<ReferenceDataStore>();
builder.Services.AddScoped<AcademicYearStore>();
builder.Services.AddScoped<TermStore>();
builder.Services.AddScoped<SectionStore>();
builder.Services.AddScoped<StudentStore>();
builder.Services.AddScoped<TeacherStore>();
builder.Services.AddScoped<AttendanceStore>();
builder.Services.AddScoped<AssessmentStore>();
builder.Services.AddScoped<MarkStore>();
builder.Services.AddScoped<HomeworkStore>();
builder.Services.AddScoped<HomeworkSubmissionStore>();
builder.Services.AddScoped<LearningMaterialStore>();
builder.Services.AddScoped<ActivityStore>();
builder.Services.AddScoped<ActivityRegistrationStore>();
builder.Services.AddScoped<UserStore>();
builder.Services.AddScoped<ParentStore>();
builder.Services.AddScoped<NotificationStore>();
builder.Services.AddScoped<AnalyticsStore>();
builder.Services.AddScoped<NotificationRecipientResolver>();
builder.Services.AddScoped<AuditLogStore>();
builder.Services.AddScoped<SearchStore>();

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
builder.Services.AddScoped<IAuditService, AuditService>();

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

// Applies database/schema.sql (idempotent CREATE TABLE IF NOT EXISTS / CREATE INDEX IF NOT
// EXISTS) and database/seed.sql (ON CONFLICT DO NOTHING) on startup — the hand-written-SQL
// equivalent of "dotnet ef database update", but as plain, reviewable .sql files instead of
// an ORM migration history table.
await ApplyDatabaseScriptsAsync(app.Services.GetRequiredService<Database>(), app.Environment, app.Logger);

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

static async Task ApplyDatabaseScriptsAsync(Database db, IWebHostEnvironment environment, ILogger logger)
{
    var databaseFolder = Path.Combine(environment.ContentRootPath, "..", "database");
    var schemaPath = Path.Combine(databaseFolder, "schema.sql");
    var seedPath = Path.Combine(databaseFolder, "seed.sql");

    if (!File.Exists(schemaPath))
    {
        logger.LogWarning("database/schema.sql not found at {Path} — skipping schema bootstrap.", schemaPath);
        return;
    }

    await db.ExecuteAsync(await File.ReadAllTextAsync(schemaPath), bind: null);
    logger.LogInformation("Applied database/schema.sql.");

    if (File.Exists(seedPath))
    {
        await db.ExecuteAsync(await File.ReadAllTextAsync(seedPath), bind: null);
        logger.LogInformation("Applied database/seed.sql.");
    }
}
