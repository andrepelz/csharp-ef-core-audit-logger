namespace Entities;

public sealed class TestEntity : Entity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<InnerEntity> InnerEntities { get; set; } = new List<InnerEntity>();
    public TestValueObject ValueObject { get; set; } = default!;
}

public sealed class InnerEntity : Entity
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public Guid TestEntityId { get; set; }
    public TestEntity? TestEntity { get; set; }
}

public sealed record TestValueObject(string Name, decimal Price);

public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
}