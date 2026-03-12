using AzureDevOpsAgents.Api.Data;
using AzureDevOpsAgents.Api.Hubs;
using AzureDevOpsAgents.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Core services ────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// ── Database ─────────────────────────────────────────────────────────────────
var dbPath = builder.Configuration["Database:Path"] ?? "ado-agents.db";
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Application services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<TokenEncryptionService>();
builder.Services.AddSingleton<McpServerManager>();
builder.Services.AddScoped<RepoCloneService>();
builder.Services.AddScoped<AnalystAgentService>();
builder.Services.AddScoped<DeveloperAgentService>();
builder.Services.AddSingleton<AssistantAgentService>();

// ── CORS — allow Angular dev server ──────────────────────────────────────────
builder.Services.AddCors(opt =>
    opt.AddPolicy("angular", p => p
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

// ── Migrate DB on startup ─────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    ctx.Database.Migrate();
}

// ── HTTP pipeline ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors("angular");
app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<AgentHub>("/hubs/agent");

app.Run();
