using Entities;
using Microsoft.EntityFrameworkCore;

namespace DbContexts;

public class TestDbContext : DbContext
{
    public DbSet<TestEntity> TestEntities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseInMemoryDatabase("TestDb");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntityOtherEntity>().HasKey(to => new { to.TestEntityId, to.OtherEntityId });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}