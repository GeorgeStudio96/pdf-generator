using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using DotNetEnv;
using PdfService.Services;
using PdfService.Models;
using PdfService.Documents;

QuestPDF.Settings.License = LicenseType.Community;
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Configure JSON to use camelCase for API (matches JavaScript/TypeScript convention)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

var internalApiKey = Env.GetString("INTERNAL_API_KEY");

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/generate"))
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
var claudeService = new ClaudeService(Env.GetString("ANTHROPIC_API_KEY"));

app.MapPost("/generate/proposal", async (ProposalRequest request) =>
{
    var proposalData = await claudeService.GenerateProposal(request);
    var document = new ProposalDocument(proposalData, logoBytes);
    var pdfBytes = document.GeneratePdf();

    return Results.File(pdfBytes, "application/pdf", "proposal.pdf");
});

app.MapGet("/", () => "Proposal Generator Ready");

app.Run();
