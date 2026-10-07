using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SJewls.Infrastructure.Data;
using SJewls.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration (Supabase / PostgreSQL)
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

// 2. OpenAPI & Swagger Generation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SJewls API",
        Version = "v1",
        Description = "API backend for SJewls Jewellery and Chitu Plans"
    });
});

// 3. CORS for Next.js Admin & React Native Mobile
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 4. Controllers & Authorization
builder.Services.AddControllers();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors("AllowAllOrigins");

// Serve OpenAPI JSON at /openapi/v1.json as required by spec Section 14
app.UseSwagger(options =>
{
    options.RouteTemplate = "openapi/{documentName}.json";
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

// Quick Branches endpoint for testing
app.MapGet("/api/v1/branches", async (SJewlsDbContext db) =>
{
    var branches = await db.Branches.Where(b => b.IsActive).ToListAsync();
    return Results.Ok(branches);
})
.WithName("GetBranches")
.WithSummary("List active branches")
.WithTags("Branches");

app.Run();
