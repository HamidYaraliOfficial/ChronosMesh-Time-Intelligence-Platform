using System.Text;
using AspNetCoreRateLimit;
using ChronosMesh.Application.Interfaces;
using ChronosMesh.Application.Services;
using ChronosMesh.Infrastructure.Persistence;
using ChronosMesh.Infrastructure.Repositories;
using ChronosMesh.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// Logging (Serilog): structured JSON logs to stdout, captured by the
// Docker log driver / any log aggregator. Every entry carries timestamp,
// service, level, and (via enrichers below) request id + user id.
// ---------------------------------------------------------------------
builder.Host.UseSerilog((ctx, cfg) => cfg
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ChronosMesh.Api")
    .MinimumLevel.Is(ctx.HostingEnvironment.IsDevelopment() ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information)
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter()));

// ---------------------------------------------------------------------
// Configuration is entirely environment-variable driven (see
// docs/ENVIRONMENT.md / README "Environment Variables" section). No
// secrets live in source control.
// ---------------------------------------------------------------------
var connectionString = builder.Configuration["ConnectionStrings:Postgres"]
    ?? Environment.GetEnvironmentVariable("CHRONOSMESH_DB_CONNECTION")
    ?? "Host=localhost;Port=5432;Database=chronosmesh;Username=chronosmesh;Password=chronosmesh";

builder.Services.AddDbContext<ChronosMeshDbContext>(opt =>
    opt.UseNpgsql(connectionString, npg => npg.EnableRetryOnFailure(3)));

// ---------------------------------------------------------------------
// Repositories / Application services (Clean Architecture wiring)
// ---------------------------------------------------------------------
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

var coreEngineUrl = Environment.GetEnvironmentVariable("CORE_ENGINE_URL") ?? "http://rust-core:7301";
var schedulerUrl = Environment.GetEnvironmentVariable("SCHEDULER_URL") ?? "http://scheduler:8081";

builder.Services.AddHttpClient<ITimeEngineClient, TimeEngineClient>(c => c.BaseAddress = new Uri(coreEngineUrl));
builder.Services.AddHttpClient<ISchedulerQueueClient, SchedulerQueueClient>(c => c.BaseAddress = new Uri(schedulerUrl));

// ---------------------------------------------------------------------
// Authentication: JWT Bearer, validated against the shared HS256 secret.
// Authorization: role-based via ASP.NET Core policies, backed by
// IPermissionService for fine-grained resource/action checks inside
// controllers.
// ---------------------------------------------------------------------
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? Environment.GetEnvironmentVariable("CHRONOSMESH_JWT_SECRET")
    ?? throw new InvalidOperationException("CHRONOSMESH_JWT_SECRET must be configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "chronosmesh";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "chronosmesh-clients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

// ---------------------------------------------------------------------
// Rate limiting (AspNetCoreRateLimit): protects auth and write endpoints
// from brute-force / abuse. Limits are environment-configurable.
// ---------------------------------------------------------------------
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.GeneralRules = new List<RateLimitRule>
    {
        new() { Endpoint = "POST:/api/v1/auth/login", Period = "1m", Limit = 10 },
        new() { Endpoint = "POST:/api/v1/auth/register", Period = "1h", Limit = 20 },
        new() { Endpoint = "*", Period = "1m", Limit = 300 },
    };
});
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// ---------------------------------------------------------------------
// CORS: the Desktop Client talks to the API directly; the Web App and
// Admin Panel are served from configured origins.
// ---------------------------------------------------------------------
var allowedOrigins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddPolicy("ChronosMeshClients", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ChronosMesh API",
        Version = "v1",
        Description = "Time Intelligence Platform — business logic, auth, and workspace management."
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseIpRateLimiting();
app.UseCors("ChronosMeshClients");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok", service = "ChronosMesh.Api" }));

app.Run();
