
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using DotNetEnv;
using PdfService.Services;
using PdfService.Models;
using PdfService.Documents;
using PdfService.Configuration;
using StackExchange.Redis;

QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.EnableDebugging = true; 
Env.Load();

try 
{
    var fontFiles = new[] { "Inter-Regular.ttf", "Inter-Bold.ttf" };
    foreach (var fontName in fontFiles)
    {
        if (File.Exists(fontName))
        {
            using var stream = File.OpenRead(fontName);
            QuestPDF.Drawing.FontManager.RegisterFont(stream);
            Console.WriteLine($"✅ Font loaded: {fontName}");
        }
        else
        {
            Console.WriteLine($"⚠️ Font NOT found: {fontName} (PDF texts might look wrong)");
        }
    }
} 
catch (Exception ex) 
{ 
    Console.WriteLine($"❌ Error loading fonts: {ex.Message}"); 
}



var builder = WebApplication.CreateBuilder(args);

// Configure JSON to use camelCase for API (matches JavaScript/TypeScript convention)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Redis configuration
var redisConfig = new RedisConfiguration
{
    ConnectionString = Env.GetString("REDIS_URL") ?? Env.GetString("REDIS_CONNECTION_STRING", "localhost:6379"),
    JobTtlHours = int.Parse(Env.GetString("REDIS_JOB_TTL_HOURS", "1")),
    FailedJobTtlHours = int.Parse(Env.GetString("REDIS_FAILED_JOB_TTL_HOURS", "24")),
    ProcessorConcurrency = int.Parse(Env.GetString("JOB_PROCESSOR_CONCURRENCY", "5")),
    ProcessorPollIntervalSeconds = int.Parse(Env.GetString("JOB_PROCESSOR_POLL_INTERVAL_SECONDS", "2"))
};
builder.Services.AddSingleton(redisConfig);

// Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<RedisConfiguration>();
    return ConnectionMultiplexer.Connect(config.ConnectionString);
});

// Repositories and services
builder.Services.AddSingleton<IJobRepository, RedisJobRepository>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<ClaudeService>(sp =>
{
    return new ClaudeService(Env.GetString("ANTHROPIC_API_KEY"));
});

// Background processor
builder.Services.AddHostedService<JobProcessorBackgroundService>();

var app = builder.Build();

var internalApiKey = Env.GetString("INTERNAL_API_KEY");

// Authentication middleware - apply to both /generate and /jobs endpoints
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/generate") ||
        context.Request.Path.StartsWithSegments("/jobs"))
    {
        var requestKey = context.Request.Headers["X-Internal-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(requestKey) || requestKey != internalApiKey)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized: Invalid or missing X-Internal-Key");
            return;
        }
    }

    await next();
});

var logoBytes = File.Exists("logo.png") ? File.ReadAllBytes("logo.png") : [];


// New async job endpoints
app.MapPost("/jobs/proposal", async (ProposalRequest request, IJobService jobService) =>
{
    var response = await jobService.CreateJobAsync(request);
    return Results.Ok(response);
});

app.MapGet("/jobs/{id}/status", async (string id, IJobService jobService) =>
{
    var status = await jobService.GetJobStatusAsync(id);
    if (status == null)
        return Results.NotFound(new { error = "Job not found" });
    return Results.Ok(status);
});

app.MapGet("/jobs/{id}/download", async (string id, IJobService jobService) =>
{
    var pdfBytes = await jobService.GetJobResultAsync(id);
    if (pdfBytes == null)
        return Results.NotFound(new { error = "Job not found or not completed" });
    return Results.File(pdfBytes, "application/pdf", $"proposal-{id}.pdf");
});

// Legacy sync endpoint (kept for backward compatibility)
app.MapPost("/generate/proposal", async (ProposalRequest request, ClaudeService claudeService) =>
{
    var proposalData = await claudeService.GenerateProposal(request);
    var document = new ProposalDocument(proposalData, logoBytes);
    var pdfBytes = document.GeneratePdf();

    return Results.File(pdfBytes, "application/pdf", "proposal.pdf");
});

// Health check endpoint
app.MapGet("/health", async (IConnectionMultiplexer redis) =>
{
    try
    {
        var db = redis.GetDatabase();
        await db.PingAsync();
        return Results.Ok(new { status = "healthy", redis = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", redis = "disconnected", error = ex.Message }, statusCode: 503);
    }
});

app.MapGet("/", () => "Proposal Generator Ready (Async Job Queue Enabled)");

app.Run();
