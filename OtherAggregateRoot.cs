namespace Entities;

public sealed class OtherAggregateRoot : Entity, IAggregateRoot
{
    public IEnumerable<TestEntity> TestEntities { get; set; } = new List<TestEntity>();
    public string Name { get; set; } = string.Empty;
}
