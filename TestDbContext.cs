using Entities;
using Microsoft.EntityFrameworkCore;

namespace DbContexts;

public class TestDbContext : DbContext
{
    public DbSet<TestEntity> TestEntities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
                .UseNpgsql("Host=localhost;Database=ListChangeTracking;Username=postgres;Password=pass123")
                .LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, Microsoft.Extensions.Logging.LogLevel.Information)
                .EnableSensitiveDataLogging();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntityOtherEntity>().HasKey(to => new { to.TestEntityId, to.OtherEntityId });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}