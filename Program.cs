using System.Text.Json;
using System.Text.Json.Serialization;
using DbContexts;
using Entities;
using Experimental;

using var db = new TestDbContext();

var entity = new TestEntity()
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
    ValueObject = new("ValueObject1", 1)
};

db.Add(entity);
db.SaveChanges();

entity.Name = "Changed";

entity.InnerEntities.Add(new() { Name = "Inner3", Quantity = 3 });
entity.InnerEntities.Remove(entity.InnerEntities.First());
entity.InnerEntities.First().Name = "ModifiedInner";

entity.ValueObject = new("ValueObject2", 2);

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

db.Add(newEntity);

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

// foreach(var change in changes)
//     Console.WriteLine(change);
