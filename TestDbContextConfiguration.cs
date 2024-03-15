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

        builder.ComplexProperty(
            e => e.ValueObject,
            b => 
            {
                b.Property(v => v.Name).HasColumnName("Name");
                b.Property(v => v.Price).HasColumnName("Price");
                b.ComplexProperty(
                    n => n.NestedValueObject,
                    b =>
                    {
                        b.Property(v => v.Value1).HasColumnName("Value1");
                        b.Property(v => v.Value2).HasColumnName("Value2");
                    }); 
            });

        builder
            .HasMany(t => t.OtherEntities)
            .WithMany(o => o.TestEntities)
            .UsingEntity(typeof(TestEntityOtherEntity));
    }
}
