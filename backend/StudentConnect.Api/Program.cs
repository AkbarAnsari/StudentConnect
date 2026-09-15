using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Auditing;
using StudentConnect.Infrastructure.Auth;
using StudentConnect.Infrastructure.Data;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Email;
using StudentConnect.Infrastructure.Health;
using StudentConnect.Infrastructure.Notifications;
using StudentConnect.Infrastructure.Storage;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Plain ADO.NET (Npgsql) data access — no ORM. See database/schema.sql for the hand-written
// schema and Data/Stores/*.cs for the SQL each controller calls into.
builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<ReferenceDataStore>();
builder.Services.AddScoped<SchoolStore>();
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
builder.Services.AddScoped<PasswordResetTokenStore>();
builder.Services.AddScoped<EmailVerificationTokenStore>();
builder.Services.AddScoped<RefreshTokenStore>();
builder.Services.AddScoped<NotificationStore>();
builder.Services.AddScoped<AnalyticsStore>();
builder.Services.AddScoped<NotificationRecipientResolver>();
builder.Services.AddScoped<AuditLogStore>();
builder.Services.AddScoped<SearchStore>();
builder.Services.AddScoped<ReportStore>();
builder.Services.AddScoped<EmailOutboxStore>();
builder.Services.AddScoped<AnnouncementStore>();
builder.Services.AddScoped<GradingBandStore>();
builder.Services.AddScoped<TeacherSubjectAssignmentStore>();

// Today files live on local disk; a future AzureBlobStorage class could implement the
// same IFileStorage interface without any controller/service code changing.
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("FileStorage"));
builder.Services.Configure<AzureBlobStorageOptions>(builder.Configuration.GetSection("AzureBlobStorage"));

// Storage:Provider selects the IFileStorage implementation — "Local" (default, for
// day-to-day development) or "AzureBlob" (Azure Blob Storage). Controllers depend
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

// Development-only JWT authentication — a production deployment would replace this with a
// identity provider (e.g. Microsoft Entra ID) would replace this with.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService, EmailNotificationService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Email:Smtp:Host selects the real SmtpEmailSender; left blank (the default) uses
// LoggingEmailSender so local dev works without a mail server. Either way, email is
// enqueued to EmailOutbox and delivered by the EmailOutboxProcessor background service —
// never sent inline on the request that triggered a notification.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Smtp"));
var smtpHost = builder.Configuration["Smtp:Host"];
if (!string.IsNullOrWhiteSpace(smtpHost))
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
}
builder.Services.AddHostedService<EmailOutboxProcessor>();
builder.Services.AddHostedService<NotificationDigestProcessor>();

// Unauthenticated /health endpoint (see below) — the kind of thing a load balancer or
// container orchestrator polls, so it deliberately bypasses the FallbackPolicy below.
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// Throttles the genuinely brute-forceable, unauthenticated credential endpoints (login,
// forgot/reset-password) per client IP — a real, simple brute-force/abuse guard. A separate,
// far more permissive policy covers the other AuthController endpoints (refresh/logout/
// verify-email/resend-verification) — those aren't credential-guessing targets, and sharing
// the tight "auth" budget with them could lock a legitimate user out of LOGIN ITSELF after a
// handful of unrelated actions (see [EnableRateLimiting] placement on AuthController).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Always return a JSON body on rejection so the frontend can show a real message instead
    // of guessing (the default 429 response otherwise has an empty body).
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many requests. Please wait a few minutes and try again." },
            ct);
    };

    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
        }));

    options.AddPolicy("auth-relaxed", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
        }));
});

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

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

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
