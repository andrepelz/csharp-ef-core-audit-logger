using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DbContexts.Configuration;

internal sealed class TestEntityConfiguration : IEntityTypeConfiguration<TestEntity>
{
    public void Configure(EntityTypeBuilder<TestEntity> builder)
    {
        builder.HasKey(e => e.Id);

        builder.OwnsMany(e => e.InnerEntities);

        builder.OwnsOne(e => e.ValueObject);
    }
}

// internal sealed class InnerEntityConfiguration : IEntityTypeConfiguration<InnerEntity>
// {
//     public void Configure(EntityTypeBuilder<InnerEntity> builder)
//     {
//         builder.HasKey(e => e.Id);
//     }
// }