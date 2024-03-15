using System.Collections;
using System.Dynamic;
using System.Reflection;
using Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Entities;
using Extensions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Experimental;

public class AuditLogger<TContext>(TContext context)
    where TContext : DbContext
{
    private readonly ICollection<object> _visitedEntries = new List<object>();
    private readonly TContext _context = context;


    public ExpandoObject? CreateAuditLog<T>(T entity)
        where T : IAggregateRoot
    {
        _visitedEntries.Clear();

        var entry = _context.Entry(entity);

        var dataShapedObject = AuditEntry(entry, root: true);

        return dataShapedObject;
    }


    private ExpandoObject? AuditEntry(EntityEntry entry, bool root = false)
    {
        if(_visitedEntries.Any(e => e == entry.Entity)) return null;
        if(entry.State == EntityState.Detached || entry.State == EntityState.Unchanged && !root) return null;

        _visitedEntries.Add(entry.Entity);

        bool changesDetected = false;
        
        var dataShapedObject = new ExpandoObject() as IDictionary<string, object?>;

        dataShapedObject.Add("AuditState", ResolveAuditState(entry.State));

        foreach(var property in entry.Properties)
        {
            string propertyName = property.Metadata.Name;

            var propertyAudit = AuditProperty(property, entry);

            if (propertyAudit is not null)
            {
                dataShapedObject.Add(propertyName, propertyAudit);

                if(propertyName != "Id")
                {
                    changesDetected = true;
                }
            }
        }

        foreach(var navigation in entry.Metadata.GetNavigations())
        {
            var navigationName = navigation.Name;
            var navigationForeignKey = navigation.ForeignKey;
            var navigationType = navigation.FieldInfo!.FieldType;
            var navigationAudit = AuditNavigation(navigationType, navigationForeignKey, entry);

            if (navigationAudit is not null)
            {
                dataShapedObject.Add(navigationName, navigationAudit);
                changesDetected = true;
            }
        }

        return changesDetected ? (dataShapedObject as ExpandoObject)! : null;
    }


    private static object? AuditProperty(PropertyEntry property, EntityEntry entry)
    {
        if (property.Metadata.IsPrimaryKey())
            return property.CurrentValue;

        if (property.Metadata.IsForeignKey())
            return AuditForeignKey(property);

        var propertyAudit = entry.State switch
        {
            EntityState.Added => AuditEntryAdded(property),
            EntityState.Deleted => AuditEntryDeleted(property),
            EntityState.Modified => AuditEntryModified(property),
            _ => null,
        };

        return propertyAudit;
    }

    private static object? AuditForeignKey(PropertyEntry property)
    {
        var result = new List<ExpandoObject>();

        var oldReference = new ExpandoObject() as IDictionary<string, object?>;
        var newReference = new ExpandoObject() as IDictionary<string, object?>;

        if(!property.IsModified) return null;

        if(((Guid) property.OriginalValue!) != Guid.Empty)
        {
            oldReference["AuditState"] = Audit.State.ReferenceSevered;
            oldReference["Id"] = property.OriginalValue;
            result.Add((ExpandoObject) oldReference);
        }

        if(((Guid) property.CurrentValue!) != Guid.Empty)
        {
            newReference["AuditState"] = Audit.State.ReferenceAdded;
            newReference["Id"] = property.CurrentValue;
            result.Add((ExpandoObject) newReference);
        }

        return result;
    }

    private IEnumerable<ExpandoObject>? AuditNavigation(
        Type navigationType,
        IForeignKey foreignKey,
        EntityEntry parentEntry)
    {
        var resultCollection = new List<ExpandoObject>();

        if (typeof(IEnumerable).IsAssignableFrom(navigationType))
            navigationType = navigationType.GetGenericArguments()[0];

        var getChangeTrackerEntriesMethod = BuildChangeTrackerEntriesMethod(navigationType);

        var entries = (IEnumerable<EntityEntry>) getChangeTrackerEntriesMethod  
            .Invoke(_context.ChangeTracker, null)!;

        var parentPrimaryKey = GetEntryPrimaryKey(parentEntry);

        if(foreignKey.IsOwnership)
        {
            entries = entries
                .Where(entry =>
                    entry.Metadata.GetForeignKeyProperties()
                        .Select(fk => entry.Property(fk.Name).CurrentValue)
                        .Contains(parentPrimaryKey.Value));
        }
        else
        {
            entries = entries
                .Where(entry =>
                    entry.Metadata.GetForeignKeyProperties()
                        .Select(fk => entry.Property(fk.Name))
                        .Any(property =>
                            Equals(property.CurrentValue, parentPrimaryKey.Value) ||
                            Equals(property.OriginalValue, parentPrimaryKey.Value)));
        }

        foreach (var item in entries)
        {
            // navigation not being ownership means it is a nested aggregate root
            var itemAudit = foreignKey.IsOwnership 
                ? AuditEntry(item)
                : AuditNestedAggregateRoot(item, foreignKey, parentPrimaryKey);

            if(itemAudit is not null)
                resultCollection.Add(itemAudit);
        }

        if(typeof(ValueObject).IsAssignableFrom(navigationType))
            resultCollection = ResolveValueObjectAudit(resultCollection, navigationType).ToList();

        return resultCollection.Count > 0 ? resultCollection : null;
    }

    private ExpandoObject? AuditNestedAggregateRoot(EntityEntry entry, IForeignKey foreignKey, KeyValuePair<string, object?> parentKey)
    {
        if (_visitedEntries.Any(e => e == entry.Entity)) return null;
        if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged) return null;

        _visitedEntries.Add(entry.Entity);

        var dataShapedObject = new ExpandoObject() as IDictionary<string, object?>;

        var auditState = ResolveAggregateRootAuditState(entry, foreignKey.Properties[0].Name, parentKey);

        dataShapedObject.Add("AuditState", auditState);

        foreach (var key in GetEntryCompositePrimaryKey(entry))
            dataShapedObject.Add(key);

        return dataShapedObject as ExpandoObject;
    }

    private IEnumerable<ExpandoObject> ResolveValueObjectAudit(IEnumerable<ExpandoObject>? valueObjectAudit, Type valueObjectType)
    {
        var oldValues = valueObjectAudit!.FirstOrDefault(audit => 
            (Audit.State) ((IDictionary<string, object?>) audit)["AuditState"]! == Audit.State.Deleted)
            as IDictionary<string, object?>;

        var newValues = valueObjectAudit!.FirstOrDefault(audit => 
            (Audit.State) ((IDictionary<string, object?>) audit)["AuditState"]! == Audit.State.Added)
            as IDictionary<string, object?>;

        if(oldValues is null || newValues is null) return [];
        
        var resultAudit = new ExpandoObject() as IDictionary<string, object?>;
        resultAudit["AuditState"] = Audit.State.Modified;

        foreach(var property in valueObjectType.GetProperties())
        {
            var propertyType = property.PropertyType;
            dynamic oldValue = Convert.ChangeType(((Audit) oldValues[property.Name]!).OldValue, propertyType)!;
            dynamic newValue =  Convert.ChangeType(((Audit) newValues[property.Name]!).NewValue, propertyType)!;

            if(oldValue != newValue)
                resultAudit[property.Name] = Audit.FieldModified(oldValue, newValue);
        }

        return [ (resultAudit as ExpandoObject)! ];
    }


    private static KeyValuePair<string, object?> GetEntryPrimaryKey(EntityEntry entry)
        => entry.KeyValuesOf()
            .Where(pair => pair.Key == "Id")
            .First();

    private static IEnumerable<KeyValuePair<string, object?>> GetEntryCompositePrimaryKey(EntityEntry entry)
        => entry.KeyValuesOf();


    private static Audit.State? ResolveAuditState(EntityState state)
        => state switch
        {
            EntityState.Added => Audit.State.Added,
            EntityState.Deleted => Audit.State.Deleted,
            EntityState.Modified => Audit.State.Modified,
            _ => Audit.State.Modified,
        };

    private Audit.State ResolveAggregateRootAuditState(EntityEntry entry, string foreignKeyName, KeyValuePair<string, object?> parentKey)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                return Audit.State.ReferenceAdded;
            case EntityState.Deleted:
                return Audit.State.ReferenceSevered;
            case EntityState.Modified:
                var foreignKey = entry.Property(foreignKeyName);

                if (foreignKey.CurrentValue is not null && (Guid) foreignKey.CurrentValue == (Guid) parentKey.Value!)
                {
                    return Audit.State.ReferenceAdded;
                }

                return Audit.State.ReferenceSevered;
            default:
                return Audit.State.Detached;
        }
    }


    private static MethodInfo BuildChangeTrackerEntriesMethod(Type genericType)
        => typeof(ChangeTracker)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m =>
                m.Name == nameof(ChangeTracker.Entries) &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters().Length == 0)
            .MakeGenericMethod(genericType);


    private static Audit AuditEntryAdded(PropertyEntry property)
        => Audit.EntryAdded(property);

    private static Audit? AuditEntryDeleted(PropertyEntry property)
        => typeof(ValueObject).IsAssignableFrom(property.EntityEntry.Entity.GetType())
        ? Audit.EntryDeleted(property)
        : null;

    private static Audit? AuditEntryModified(PropertyEntry property)
        => property.IsModified
        ? Audit.EntryModified(property)
        : null;
}
