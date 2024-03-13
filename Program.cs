using System.Text.Json;
using System.Text.Json.Serialization;
using DbContexts;
using Entities;
using Experimental;
using Microsoft.EntityFrameworkCore;

using var db = new TestDbContext();

var otherEntities = new List<OtherEntity>()
{
    new OtherEntity{ Name = "Other1" },
    new OtherEntity{ Name = "Other2" },
    new OtherEntity{ Name = "Other3" }
};

db.AddRange(otherEntities);

var entity1 = new TestEntity()
{
    Name = "Initial",
    InnerEntities = new List<InnerEntity>
    {
        new()
        {
            Name = "Inner1",
            Quantity = 1
        },
        new()
        {
            Name = "Inner2",
            Quantity = 2
        }
    },
    ValueObject = new("ValueObject1", 1),
    OtherEntities = [ otherEntities[1] ]
};

db.Add(entity1);
db.SaveChanges();

var entity = db.TestEntities
    .Include(t => t.OtherEntities)
    .Include(t => t.TestEntityOtherEntities)
        .ThenInclude(o => o.OtherEntity)
    .FirstOrDefault(e => e.Id == entity1.Id);

entity!.Name = "Changed";

entity.InnerEntities.Add(new() { Name = "Inner3", Quantity = 3 });
entity.InnerEntities.Remove(entity.InnerEntities.First());
entity.InnerEntities.First().Name = "ModifiedInner";

entity.ValueObject = new("ValueObject2", 2);

entity.TestEntityOtherEntities.Remove(entity.TestEntityOtherEntities.Where(t => t.OtherEntityId == otherEntities[1].Id).FirstOrDefault()!);
entity.TestEntityOtherEntities.Add(new TestEntityOtherEntity(otherEntities[2]));
entity.OtherEntities.First().Name = "ModifiedOther";

var newEntity = new TestEntity()
{
    Name = "New",
    InnerEntities = new List<InnerEntity>
    {
        new()
        {
            Name = "NewInner1",
            Quantity = 1
        },
        new()
        {
            Name = "NewInner2",
            Quantity = 2
        }
    },
    ValueObject = new("NewValueObject1", 1)
};

var auditLogger = new AuditLogger<TestDbContext>(db);

var auditLog = auditLogger.CreateAuditLog(entity);

Console.WriteLine(JsonSerializer.Serialize(
    auditLog,
    new JsonSerializerOptions()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    }));

var changes = db.ChangeTracker.Entries();