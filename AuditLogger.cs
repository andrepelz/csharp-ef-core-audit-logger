using System.Collections;
using System.Dynamic;
using System.Reflection;
using Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Entities;
using Extensions;

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

    private ExpandoObject? AuditEntry(EntityEntry entry)
    {
        if(_visitedEntries.Any(e => e == entry.Entity)) return null;
        if(entry.State == EntityState.Detached || entry.State == EntityState.Unchanged) return null;

        _visitedEntries.Add(entry.Entity);
        
        var dataShapedObject = new ExpandoObject() as IDictionary<string, object?>;

        dataShapedObject.Add("AuditState", entry.State switch
        {
            EntityState.Added => Audit.State.Added,
            EntityState.Deleted => Audit.State.Deleted,
            EntityState.Modified =>Audit.State.Modified,
            _ => null,
        });

        foreach(var property in entry.Properties)
        {
            string propertyName = property.Metadata.Name;

            var propertyAudit = AuditProperty(property, entry);

            if (propertyAudit is not null)
                dataShapedObject.Add(propertyName, propertyAudit);
        }

        foreach(var navigation in entry.Navigations)
        {
            var navigationName = navigation.Metadata.Name;
            var navigationType = navigation.Metadata.FieldInfo!.FieldType;
            var navigationAudit = AuditNavigation(navigationType, entry);

            if (navigationAudit is not null)
                dataShapedObject.Add(navigationName, navigationAudit);
        }

        return (dataShapedObject as ExpandoObject)!;
    }

    private object? AuditProperty(PropertyEntry property, EntityEntry entry)
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
        EntityEntry parentEntry)
    {
        // if(typeof(IAggregateRoot).IsAssignableFrom(navigationType)) return null;

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
                    .Contains(parentPrimaryKey));

        foreach(var item in entries)
        {
            var itemAudit = AuditEntry(item);

            if(itemAudit is not null)
                resultCollection.Add(itemAudit);
        }
        
        return resultCollection.Count > 0 ? resultCollection : null;
    }

    private static MethodInfo BuildChangeTrackerEntriesMethod(Type genericType)
        => typeof(ChangeTracker)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m =>
                m.Name == nameof(ChangeTracker.Entries) &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters().Length == 0)
            .MakeGenericMethod(genericType);

    private static object? GetEntryPrimaryKey(EntityEntry entry)
        => entry.KeyValuesOf()
            .Where(pair => pair.Key == "Id")
            .First().Value;

    private static Audit AuditEntryAdded(PropertyEntry property)
        => Audit.EntryAdded(property);

    private static Audit AuditEntryDeleted(PropertyEntry property)
        => Audit.EntryDeleted(property);

    private static Audit? AuditEntryModified(PropertyEntry property)
        => property.IsModified
        ? Audit.EntryModified(property)
        : null;
}
