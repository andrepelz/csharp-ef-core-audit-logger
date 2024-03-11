using System.Collections;
using System.Dynamic;
using System.Reflection;
using Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Entities;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Experimental;

public class AuditLogger<TContext>(TContext context)
    where TContext : DbContext
{
    private readonly ICollection<object> _visitedEntries = new List<object>();
    private readonly TContext _context = context;

    public ExpandoObject? CreateAuditLog<T>(T entity)
        where T : class
    {
        _visitedEntries.Clear();

        var entry = _context.Entry(entity);

        // entry.Metadata
        //     .GetKeys()
        //     .Where(key => key.IsPrimaryKey());

        if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            return null;

        var dataShapedObject = AuditEntry(entry);

        return dataShapedObject;
    }

    private ExpandoObject? AuditEntry(EntityEntry entry)
    {
        if(_visitedEntries.Any(e => e == entry.Entity)) return null;

        _visitedEntries.Add(entry.Entity);
        
        var dataShapedObject = new ExpandoObject() as IDictionary<string, object?>;

        foreach(var property in entry.Properties)
        {
            string propertyName = property.Metadata.Name;

            if (property.Metadata.IsPrimaryKey())
            {
                dataShapedObject.Add(propertyName, property.CurrentValue);
                continue;
            }

            var propertyAudit = entry.State switch
            {
                EntityState.Added => AuditEntryAdded(property),
                EntityState.Deleted => AuditEntryDeleted(property),
                EntityState.Modified => AuditEntryModified(property),
                _ => null,
            };

            if (propertyAudit is not null)
                dataShapedObject.Add(propertyName, propertyAudit);
        }

        foreach(var navigation in entry.Navigations)
        {
            var navigationName = navigation.Metadata.Name;
            var navigationType = navigation.Metadata.FieldInfo!.FieldType;
            var navigationAudit = ResolveNavigationAudit(navigationType, entry);

            if (navigationAudit is not null && navigationAudit.Any())
                dataShapedObject.Add(navigationName, navigationAudit);
        }

        return (dataShapedObject as ExpandoObject)!;
    }

    private IEnumerable<ExpandoObject>? ResolveNavigationAudit(
        Type navigationType, 
        EntityEntry parentEntry)
    {
        var getChangeTrackerEntriesMethod = typeof(ChangeTracker)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m =>
                m.Name == nameof(ChangeTracker.Entries) &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters().Length == 0);

        var auditEntryMethod = typeof(AuditLogger<TContext>)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(m =>
                m.Name == nameof(AuditEntry) &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters().Length == 2);

        var resultCollection = new List<ExpandoObject>();

        if(typeof(IEnumerable).IsAssignableFrom(navigationType))
            navigationType = navigationType.GetGenericArguments().First();

        var entries = (IEnumerable<EntityEntry>) getChangeTrackerEntriesMethod  
            .MakeGenericMethod(navigationType)
            .Invoke(_context.ChangeTracker, null)!;

        var parentPrimaryKey = parentEntry.Metadata
            .GetKeys()
            .Where(key => key.IsPrimaryKey())
            .Select(pk => pk.Properties.First())
            .Select(pk => parentEntry.Property(pk.Name).CurrentValue)
            .First();

        entries = entries
            .Where(entry =>
                entry.Metadata.GetForeignKeyProperties()
                    .Select(fk => entry.Property(fk.Name).CurrentValue)
                    .Contains(parentPrimaryKey));

        foreach(var item in entries)
        {
            var itemType = item.Entity.GetType();

            resultCollection.Add(
                (ExpandoObject) auditEntryMethod  
                    .MakeGenericMethod(itemType)
                    .Invoke(this, [item, item.Entity])!);
        }
        
        return resultCollection;
    }

    private Audit AuditEntryAdded(PropertyEntry property)
        => Audit.EntryAdded(property);

    private Audit AuditEntryDeleted(PropertyEntry property)
        => Audit.EntryDeleted(property);

    private Audit? AuditEntryModified(PropertyEntry property)
        => property.IsModified
        ? Audit.EntryModified(property)
        : null;
}
