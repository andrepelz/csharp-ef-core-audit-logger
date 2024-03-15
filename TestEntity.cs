namespace Entities;

public sealed class TestEntity : Entity, IAggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public ICollection<InnerEntity> InnerEntities { get; set; } = new List<InnerEntity>();
    public TestValueObject ValueObject { get; set; } = default!;
    public ICollection<OtherEntity> OtherEntities { get; set; } = [];
    public ICollection<TestEntityOtherEntity> TestEntityOtherEntities { get; set; } = [];
}

public sealed class InnerEntity : Entity
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public Guid TestEntityId { get; set; }
    public TestEntity? TestEntity { get; set; }
}

public sealed record TestValueObject : ValueObject
{
    public string Name { get; init; } = string.Empty;
    public int Price { get; init; }
    public NestedValueObject NestedValueObject { get; init; } = default!;

    private TestValueObject() { }
    public TestValueObject(string name, int price, NestedValueObject nestedValueObject)
    {
        Name = name;
        Price = price;
        NestedValueObject = nestedValueObject;
    }
}

public sealed record NestedValueObject : ValueObject
{
    public string Value1 { get; init; } = string.Empty;
    public string Value2 { get; init; } = string.Empty;

    private NestedValueObject() { }
    public NestedValueObject(string value1, string value2) 
    {
        Value1 = value1;
        Value2 = value2;
    }
};

public class TestEntityOtherEntity
{
    public Guid TestEntityId { get; set; }
    public Guid OtherEntityId { get; set; }
    public OtherEntity OtherEntity { get; set; } = default!;
    public TestEntity TestEntity { get; set; } = default!;

    public TestEntityOtherEntity() { }

    public TestEntityOtherEntity(OtherEntity otherEntity)
    {
        OtherEntityId = otherEntity.Id;
        OtherEntity = otherEntity;
    }
}

public class OtherEntity : Entity, IAggregateRoot
{
    public string Name { get; set; } = string.Empty;
    public ICollection<TestEntity> TestEntities { get; set; } = [];
    public ICollection<TestEntityOtherEntity> TestEntityOtherEntities { get; set; } = [];
}

public abstract record ValueObject;

public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
}

public interface IAggregateRoot { }
