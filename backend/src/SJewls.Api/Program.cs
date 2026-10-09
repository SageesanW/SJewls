using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using Swashbuckle.AspNetCore.SwaggerGen;
using SJewls.Api.Endpoints;
using SJewls.Application.Common;
using SJewls.Application.Interfaces;
using SJewls.Domain.Entities;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway / Container Port Binding (listens on 0.0.0.0:PORT when PORT is set)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 1. Database Configuration (Supabase PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

builder.Services.AddDbContext<SJewlsDbContext>(options =>
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("SJewls.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
        });
    }
});

// 2. Email Configuration (Gmail SMTP with STARTTLS)
builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Email:Host is required.")
    .Validate(options => options.Port > 0 && options.Port <= 65535, "Email:Port must be between 1 and 65535.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "Email:Username is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.FromAddress), "Email:FromAddress is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.FromName), "Email:FromName is required.")
    .ValidateOnStart();

// 3. SMS Configuration (Text.lk SMS Gateway)
builder.Services.AddOptions<SmsOptions>()
    .Bind(builder.Configuration.GetSection(SmsOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddHttpClient<TextLkSmsSender>();
builder.Services.AddScoped<MockSmsSender>();

// Register ISmsSender (TextLkSmsSender for real delivery, MockSmsSender if explicitly set to Mock)
builder.Services.AddScoped<ISmsSender>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config["Sms:Provider"] ?? "TextLk";
    if (provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
    {
        return sp.GetRequiredService<MockSmsSender>();
    }
    return sp.GetRequiredService<TextLkSmsSender>();
});

// 4. Register Application & Infrastructure Services
builder.Services.AddScoped<EmailOtpProvider>();
builder.Services.AddScoped<IEmailOtpProvider>(sp => sp.GetRequiredService<EmailOtpProvider>());
builder.Services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<EmailOtpProvider>()); // Real Gmail SMTP email delivery via MailKit
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<ICustomerAuthService, CustomerAuthService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Staff Authentication & Password Management
builder.Services.AddScoped<IPasswordHasher<Staff>, PasswordHasher<Staff>>();
builder.Services.AddScoped<IStaffAuthService, StaffAuthService>();
builder.Services.AddScoped<IAdminCustomerService, AdminCustomerService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddHttpClient<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddScoped<IJewelleryCategoryService, JewelleryCategoryService>();


// Rate Limiting for Auth Endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AuthLimiter", opt =>
    {
        opt.PermitLimit = 15;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// 3. Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "super-secret-key-that-must-be-at-least-32-characters-long-sjewls-dev";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SJewls.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SJewls.App";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<SJewlsDbContext>();
            var user = context.Principal;
            if (user == null) return;

            var tokenType = user.FindFirst("token_type")?.Value;
            var subStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            if (!Guid.TryParse(subStr, out var id)) return;

            if (tokenType == "staff")
            {
                var stampClaim = user.FindFirst("security_stamp")?.Value;
                var staff = await db.StaffMembers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
                if (staff == null || !staff.IsActive || (!string.IsNullOrEmpty(stampClaim) && staff.SecurityStamp != stampClaim))
                {
                    context.Fail("Staff account is deactivated, invalid, or session has been revoked.");
                }
            }
            else
            {
                var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                if (customer == null || !customer.IsActive)
                {
                    context.Fail("Customer account is deactivated or closed.");
                }
            }
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StaffOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("token_type", "staff");
    });
    options.AddPolicy("SuperAdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("token_type", "staff");
        policy.RequireRole("Super Admin");
    });
    options.AddPolicy("BranchAdminOrSuperAdmin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("token_type", "staff");
        policy.RequireRole("Super Admin", "Branch Admin");
    });
});

// 4. OpenAPI & Swagger with JWT Security Definitions
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("mobile", new OpenApiInfo
    {
        Title = "SJewls Customer Mobile API",
        Version = "v1",
        Description = "API endpoints for the SJewls Customer Mobile Application — Contact Verification, OTP Request & Verification, Profile Completion, Self-Service Account Closure, and Active Branches."
    });

    options.SwaggerDoc("admin", new OpenApiInfo
    {
        Title = "SJewls Admin & Staff Portal API",
        Version = "v1",
        Description = "API endpoints for the SJewls Admin Web Portal — Staff Authentication, Password Recovery, Staff User Management, Customer Management with Full Unmasked NIC, and Branch Statistics."
    });

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SJewls Complete Unified API",
        Version = "v1",
        Description = "Unified API catalog containing all Customer Mobile and Admin Web endpoints."
    });

    options.DocInclusionPredicate((docName, apiDesc) =>
    {
        var path = apiDesc.RelativePath?.ToLowerInvariant() ?? "";
        if (docName == "mobile")
        {
            // Exclude admin endpoints
            return !path.Contains("api/v1/admin");
        }
        if (docName == "admin")
        {
            // Include admin endpoints plus shared public endpoints
            return path.Contains("api/v1/admin") || path == "api/v1/health" || path == "api/v1/branches";
        }
        return true;
    });

    options.AddServer(new OpenApiServer
    {
        Url = "http://localhost:5230",
        Description = "Local Development Server"
    });
    options.AddServer(new OpenApiServer
    {
        Url = "/",
        Description = "Current Origin Server"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT Bearer token (e.g., Bearer eyJhbGci...)"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    options.OperationFilter<EndpointMetadataFilter>();
});

// 5. CORS for Next.js Admin & React Native Mobile
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAllOrigins");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Serve OpenAPI JSON at /openapi/v1.json as required by spec Section 14
app.UseSwagger(options =>
{
    options.RouteTemplate = "openapi/{documentName}.json";
    options.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
    {
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new() { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}", Description = "Current Host" },
            new() { Url = "http://localhost:5230", Description = "Local Server" }
        };
    });
});

// Serve Scalar API reference at /scalar/v1
app.MapScalarApiReference(options =>
{
    options.WithTitle("SJewls API Explorer")
           .WithTheme(ScalarTheme.Moon)
           .WithOpenApiRoutePattern("/openapi/{documentName}.json")
           .WithEndpointPrefix("/scalar/{documentName}");
});

// Redirect root to scalar documentation
app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

// Health check endpoint
app.MapGet("/api/v1/health", async (SJewlsDbContext? db) =>
{
    var dbConnected = false;
    string? dbError = null;

    if (db != null)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1;");
            dbConnected = true;
        }
        catch (Exception ex)
        {
            dbError = ex.Message;
        }
    }

    return Results.Ok(new
    {
        status = "Healthy",
        timestampUtc = DateTimeOffset.UtcNow,
        databaseConnected = dbConnected,
        databaseError = dbError,
        environment = app.Environment.EnvironmentName
    });
})
.WithName("HealthCheck")
.WithSummary("System and database health status")
.WithTags("System");

// Map Customer Authentication & Registration Endpoints
app.MapAuthEndpoints();
app.MapCustomerEndpoints();

// Map Admin Authentication, User, Customer, Branch & Jewellery Category Management Endpoints
app.MapAdminAuthEndpoints();
app.MapAdminUserEndpoints();
app.MapAdminCustomerEndpoints();
app.MapBranchEndpoints();
app.MapJewelleryCategoryEndpoints();


// Seed initial roles, default branch, and initial Super Admin if configured
try
{
    using var scope = app.Services.CreateScope();
    var staffAuthService = scope.ServiceProvider.GetRequiredService<IStaffAuthService>();
    await staffAuthService.SeedInitialSuperAdminAsync();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Failed to run initial Super Admin and roles seeding on startup.");
}

if (app.Environment.IsDevelopment())
{
    EnsurePortAvailable(5230, app.Logger);
}

app.Run();

static void EnsurePortAvailable(int port, ILogger logger)
{
    try
    {
        using var testSocket = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp);

        try
        {
            testSocket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, port));
            testSocket.Close();
            return;
        }
        catch (System.Net.Sockets.SocketException)
        {
            logger.LogWarning("Port {Port} is in use. Checking for stale processes to terminate...", port);
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "lsof",
                Arguments = $"-ti :{port}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(1000);

                var pids = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var pidStr in pids)
                {
                    if (int.TryParse(pidStr, out var pid) && pid != Environment.ProcessId)
                    {
                        try
                        {
                            var targetProc = System.Diagnostics.Process.GetProcessById(pid);
                            logger.LogWarning("Terminating stale process {ProcessName} (PID {Pid}) occupying port {Port}...", targetProc.ProcessName, pid, port);
                            targetProc.Kill();
                            targetProc.WaitForExit(1500);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Could not terminate process {Pid}", pid);
                        }
                    }
                }
            }

            System.Threading.Thread.Sleep(500);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to auto-free port {Port}. Proceeding with standard startup.", port);
    }
}

/// <summary>
/// Swagger operation filter to copy minimal API .WithSummary() and .WithDescription() into OpenAPI operations for Scalar.
/// </summary>
public class EndpointMetadataFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var summaryMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Http.Metadata.IEndpointSummaryMetadata>()
            .LastOrDefault();
        if (summaryMetadata != null && string.IsNullOrEmpty(operation.Summary))
        {
            operation.Summary = summaryMetadata.Summary;
        }

        var descriptionMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Http.Metadata.IEndpointDescriptionMetadata>()
            .LastOrDefault();
        if (descriptionMetadata != null && string.IsNullOrEmpty(operation.Description))
        {
            operation.Description = descriptionMetadata.Description;
        }
    }
}
