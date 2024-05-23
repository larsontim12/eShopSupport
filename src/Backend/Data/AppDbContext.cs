﻿using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace eShopSupport.Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Ticket> Tickets { get; set; }

    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Ticket>().HasMany(t => t.Messages).WithOne().OnDelete(DeleteBehavior.Cascade);
    }

    public static async Task EnsureDbCreatedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Wait until the DB is ready
        var pipeline = new ResiliencePipelineBuilder().AddRetry(new RetryStrategyOptions { Delay = TimeSpan.FromSeconds(3) }).Build();
        var createdDb = await pipeline.ExecuteAsync(async (CancellationToken ct) =>
            await dbContext.Database.EnsureCreatedAsync(ct));
        
        if (createdDb)
        {
            var importDataFromDir = Environment.GetEnvironmentVariable("ImportInitialDataDir");
            if (!string.IsNullOrEmpty(importDataFromDir))
            {
                await ImportInitialData(dbContext, importDataFromDir);
            }
        }
    }

    private static async Task ImportInitialData(AppDbContext dbContext, string dirPath)
    {
        try
        {
            var tickets = JsonSerializer.Deserialize<Ticket[]>(
                File.ReadAllText(Path.Combine(dirPath, "tickets.json")))!;

            // Explicitly remove the IDs so they will be auto-generated by the DB
            foreach (var ticket in tickets)
            {
                ticket.TicketId = 0;
                foreach (var message in ticket.Messages)
                {
                    message.MessageId = 0;
                }
            }

            await dbContext.Tickets.AddRangeAsync(tickets);
            await dbContext.SaveChangesAsync();
        }
        catch
        {
            // If the initial import failed, we drop the DB so it will try again next time
            await dbContext.Database.EnsureDeletedAsync();
            throw;
        }
    }
}
