using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using SJewls.Api.Endpoints;
using SJewls.Application.Interfaces;
using SJewls.Infrastructure.Data;
using SJewls.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

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

// 2. Register Application & Infrastructure Services
builder.Services.AddScoped<ISmsSender, MockSmsSender>();
builder.Services.AddScoped<IEmailSender, MockEmailSender>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<ICustomerAuthService, CustomerAuthService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

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
});

builder.Services.AddAuthorization();

// 4. OpenAPI & Swagger with JWT Security Definitions
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SJewls API",
        Version = "v1",
        Description = "API backend for SJewls Jewellery and Chitu Plans — Customer Authentication & Registration Flow"
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
app.MapGet("/", () => Results.Redirect("/scalar/v1"));

// Health check endpoint
app.MapGet("/api/v1/health", async (SJewlsDbContext? db) =>
{
    var dbConnected = false;
    string? dbError = null;

    if (db != null)
    {
        try
        {
            dbConnected = await db.Database.CanConnectAsync();
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

// Quick Branches endpoint
app.MapGet("/api/v1/branches", async (SJewlsDbContext db) =>
{
    var branches = await db.Branches.Where(b => b.IsActive).ToListAsync();
    return Results.Ok(branches);
})
.WithName("GetBranches")
.WithSummary("List active branches")
.WithTags("Branches");

// Map Customer Authentication & Registration Endpoints
app.MapAuthEndpoints();
app.MapCustomerEndpoints();

app.Run();
