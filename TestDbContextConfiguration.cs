using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DbContexts.Configuration;

internal sealed class TestEntityConfiguration : IEntityTypeConfiguration<TestEntity>
{
    public void Configure(EntityTypeBuilder<TestEntity> builder)
    {
        builder.HasKey(e => e.Id);

        builder.OwnsMany(
            e => e.InnerEntities);

        builder.OwnsOne(
            e => e.ValueObject,
            builder => 
            {
                builder.Property<Guid>("Id");
                builder.HasKey("Id");
            });
    }
}
