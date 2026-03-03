using AzureDevOpsAgents.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AzureDevOpsAgents.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AzureDevOpsConnection> Connections => Set<AzureDevOpsConnection>();
    public DbSet<ProjectRepo> Repos => Set<ProjectRepo>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AgentJob> AgentJobs => Set<AgentJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AzureDevOpsConnection>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Repos).WithOne(r => r.Connection).HasForeignKey(r => r.ConnectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ChatSessions).WithOne(s => s.Connection).HasForeignKey(s => s.ConnectionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectRepo>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.AgentJobs).WithOne(j => j.Repo).HasForeignKey(j => j.RepoId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Messages).WithOne(m => m.Session).HasForeignKey(m => m.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.AgentJobs).WithOne(j => j.Session).HasForeignKey(j => j.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasConversion<string>();
        });

        modelBuilder.Entity<AgentJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AgentType).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
        });

        modelBuilder.Entity<ProjectRepo>(e =>
        {
            e.Property(x => x.CloneStatus).HasConversion<string>();
        });
    }
}
