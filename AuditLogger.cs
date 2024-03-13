using System.Collections;
using System.Dynamic;
using System.Reflection;
using Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Entities;
using Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

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

        var dataShapedObject = AuditEntry(entry);

        return dataShapedObject;
    }


    private ExpandoObject? AuditEntry(EntityEntry entry, bool nestedAggregateRoot = false)
    {
        if(_visitedEntries.Any(e => e == entry.Entity)) return null;
        if(entry.State == EntityState.Detached || entry.State == EntityState.Unchanged) return null;

        _visitedEntries.Add(entry.Entity);
        
        var dataShapedObject = new ExpandoObject() as IDictionary<string, object?>;

        dataShapedObject.Add("AuditState", ResolveAuditState(entry.State));

        if(nestedAggregateRoot)
        {
            foreach(var key in GetEntryCompositePrimaryKey(entry))
                dataShapedObject.Add(key);

            // only tracks new and severed references to aggregate roots (Added & Removed)
            return ((Audit.State) dataShapedObject["AuditState"]!) != Audit.State.Modified
                ? (ExpandoObject) dataShapedObject
                : null;
        }

        foreach(var property in entry.Properties)
        {
            string propertyName = property.Metadata.Name;

            var propertyAudit = AuditProperty(property, entry);

            if (propertyAudit is not null)
                dataShapedObject.Add(propertyName, propertyAudit);
        }

        foreach(var navigation in entry.Metadata.GetNavigations())
        {
            var navigationName = navigation.Name;
            var navigationIsOwnership = navigation.ForeignKey.IsOwnership;
            var navigationType = navigation.FieldInfo!.FieldType;
            var navigationAudit = AuditNavigation(navigationType, navigationIsOwnership, entry);

            if (navigationAudit is not null)
                dataShapedObject.Add(navigationName, navigationAudit);
        }

        return (dataShapedObject as ExpandoObject)!;
    }


    private static object? AuditProperty(PropertyEntry property, EntityEntry entry)
    {
        if (property.Metadata.IsPrimaryKey())
            return property.CurrentValue;

        var propertyAudit = entry.State switch
        {
            EntityState.Added => AuditEntryAdded(property),
            EntityState.Deleted => null,
            EntityState.Modified => AuditEntryModified(property),
            _ => null,
        };

        return propertyAudit;
    }

    private IEnumerable<ExpandoObject>? AuditNavigation(
        Type navigationType, 
        bool navigationIsOwnership,
        EntityEntry parentEntry)
    {
        var resultCollection = new List<ExpandoObject>();

        if(typeof(IEnumerable).IsAssignableFrom(navigationType))
            navigationType = navigationType.GetGenericArguments().First();

        var getChangeTrackerEntriesMethod = BuildChangeTrackerEntriesMethod(navigationType);

        var entries = (IEnumerable<EntityEntry>) getChangeTrackerEntriesMethod  
            .Invoke(_context.ChangeTracker, null)!;

        var parentPrimaryKey = GetEntryPrimaryKey(parentEntry);

        entries = entries
            .Where(entry =>
                entry.Metadata.GetForeignKeyProperties()
                    .Select(fk => entry.Property(fk.Name).CurrentValue)
                    .Contains(parentPrimaryKey.Value));

        foreach (var item in entries)
        {
            var itemAudit = AuditEntry(item, !navigationIsOwnership); // navigation not being ownership means it is a nested aggregate root

            if(itemAudit is not null)
                resultCollection.Add(itemAudit);
        }

        return resultCollection.Count > 0 ? resultCollection : null;
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
            _ => null,
        };

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

    private static Audit AuditEntryDeleted(PropertyEntry property)
        => Audit.EntryDeleted(property);

    private static Audit? AuditEntryModified(PropertyEntry property)
        => property.IsModified
        ? Audit.EntryModified(property)
        : null;
}
