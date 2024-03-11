using System.Collections;
using System.Dynamic;
using System.Reflection;
using Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Entities;

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

        if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            return null;

        var dataShapedObject = AuditEntry(entry, entity);

        return dataShapedObject;
    }

    private ExpandoObject? AuditEntry<T>(EntityEntry entry, T entity)
        where T : class
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
            var navigationAudit = ResolveNavigationAudit(navigationType, entity);

            if (navigationAudit is not null)
                dataShapedObject.Add(navigationName, navigationAudit);
        }

        return (dataShapedObject as ExpandoObject)!;
    }

    private IEnumerable<ExpandoObject>? ResolveNavigationAudit<T>(
        Type navigationType, 
        T entity)
        where T : class
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

        entries = entries
            .Where(entry =>
                entry.Metadata.GetForeignKeyProperties()
                    .Select(fk => entry.Property(fk.Name).CurrentValue)
                    .Contains((entity as Entity)!.Id));

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

    private static Audit AuditEntryAdded(PropertyEntry property)
        => Audit.EntryAdded(property, builder => 
            builder
                .CreatedBy(
                    null == null // CreatedBy property is not implemented
                    ? Guid.NewGuid()
                    : Guid.Empty)
                .WithNewValue(property.CurrentValue));

    private static Audit AuditEntryDeleted(PropertyEntry property)
        => Audit.EntryDeleted(property, builder => 
            builder
                .LastModifiedBy(
                    null == null // LastModifiedBy property is not implemented
                    ? Guid.NewGuid()
                    : Guid.Empty)
                .WithOldValue(property.OriginalValue));

    private static Audit? AuditEntryModified(PropertyEntry property)
        => property.IsModified
        ? Audit.EntryModified(property, builder =>  
            builder
                .LastModifiedBy(
                    null == null // LastModifiedBy property is not implemented
                    ? Guid.NewGuid()
                    : Guid.Empty)
                .WithNewValue(property.CurrentValue)
                .WithOldValue(property.OriginalValue))
        : null;
}