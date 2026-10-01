// File: Program.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using System.IO;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Force the physical path of the current project directory as the content root
    ContentRootPath = Directory.GetCurrentDirectory()
});

// 1. Add services for API controllers (needed for ExamController)
builder.Services.AddControllers();

// 2. Native OpenAPI configuration for .NET
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request processing pipeline
if (app.Environment.IsDevelopment())
{
    // Enable native OpenAPI endpoint in development mode
    app.MapOpenApi();
}

// Enforce HTTPS redirection for secure communication
app.UseHttpsRedirection();

// 3. Automatically search and serve default files like index.html when accessing the root path (/)
app.UseDefaultFiles();

// 4. Explicitly map static files to the physical 'wwwroot' folder (ensures index.html and test.html are served)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
    RequestPath = ""
});

app.UseAuthorization();

// 5. Map API controller endpoints (enables /api/exam routes)
app.MapControllers();

app.Run();