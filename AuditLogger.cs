using System.Collections;
using System.Dynamic;
using System.Reflection;
using Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Entities;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Linq;

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

            if (property.Metadata.IsPrimaryKey())
            {
                dataShapedObject.Add(propertyName, property.CurrentValue);
                continue;
            }

            var propertyAudit = entry.State switch
            {
                EntityState.Added => AuditEntryAdded(property),
                EntityState.Deleted => null,
                EntityState.Modified => AuditEntryModified(property),
                _ => null,
            };

            if (propertyAudit is not null)
                dataShapedObject.Add(propertyName, propertyAudit);
        }

        foreach(var navigation in entry.Metadata.GetNavigations())
        {
            var navigationName = navigation.Name;
            var navigationIsOwnership = navigation.ForeignKey.IsOwnership;
            var navigationType = navigation.FieldInfo!.FieldType;
            var navigationAudit = ResolveNavigationAudit(navigationType, navigationIsOwnership, entry);

            if (navigationAudit is not null && navigationAudit.Any())
                dataShapedObject.Add(navigationName, navigationAudit);
        }

        return (dataShapedObject as ExpandoObject)!;
    }

    private IEnumerable<ExpandoObject>? ResolveNavigationAudit(
        Type navigationType,
        bool navigationIsOwnership,
        EntityEntry parentEntry)
    {
        var getChangeTrackerEntriesMethod = typeof(ChangeTracker)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m =>
                m.Name == nameof(ChangeTracker.Entries) &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters().Length == 0);

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

        if(navigationIsOwnership)
        {
            foreach (var item in entries)
            {
                resultCollection.Add(
                    AuditEntry(item)!);
            }
        }
        else
        {
            foreach (var item in entries)
            {
                resultCollection.Add(
                    AuditEntryM2M(item)!);
            }
        }

        
        
        return resultCollection;
    }

    private ExpandoObject? AuditEntryM2M(EntityEntry entry)
    {
        if (_visitedEntries.Any(e => e == entry.Entity)) return null;

        _visitedEntries.Add(entry.Entity);

        var dataShapedObject = new ExpandoObject() as IDictionary<string, object?>;

        dataShapedObject.Add("AuditState", entry.State switch
        {
            EntityState.Added => Audit.State.Added,
            EntityState.Deleted => Audit.State.Deleted,
            EntityState.Modified => Audit.State.Modified,
            _ => null,
        });

        dataShapedObject.Add(entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => new KeyValuePair<string, object>(p.Metadata.Name, p.CurrentValue)).FirstOrDefault()!);

        return (ExpandoObject)dataShapedObject;
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
