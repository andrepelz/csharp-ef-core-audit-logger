using System.Collections;
using System.Dynamic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Extensions;

namespace Experimental;

public class AuditLogger<TContext>(TContext context)
    where TContext : DbContext
{
    private readonly ICollection<object> _visitedEntries = new List<object>();
    private readonly TContext _context = context;

    public void CreateAuditLog<T>(T entity)
        where T : class
    {
        _visitedEntries.Clear();

        var entry = _context.Entry(entity);

        if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            return;

        var dataShapedObject = AuditEntry(entry, entity);

        Console.WriteLine(JsonSerializer.Serialize(
            dataShapedObject, 
            new JsonSerializerOptions()
            { 
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
            }));
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
            var navigationAudit = ResolveNavigationAudit(navigationName, navigationType, entity);

            if (navigationAudit is not null)
                dataShapedObject.Add(navigationName, navigationAudit);
        }

        return (dataShapedObject as ExpandoObject)!;
    }

    private object? ResolveNavigationAudit<T>(
        string navigationName, 
        Type navigationType, 
        T entity)
        where T : class
    {
        var getPropertyValueMethod = typeof(ObjectExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m =>
                m.Name == nameof(ObjectExtensions.GetPropertyValue) &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters().Length == 2);

        var navigationValue = getPropertyValueMethod  
            .MakeGenericMethod(navigationType)
            .Invoke(entity, [entity, navigationName]);

        var auditEntryMethod = typeof(AuditLogger<TContext>)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(m =>
                m.Name == nameof(AuditEntry) &&
                m.GetGenericArguments().Length == 1 &&
                m.GetParameters().Length == 2);

        if(navigationValue is null) return null;

        if(typeof(IEnumerable).IsAssignableFrom(navigationType))
        {
            // lista de navegacao
            var resultCollection = new List<ExpandoObject>();
            
            foreach(var item in (navigationValue as IEnumerable)!)
            {
                var itemEntry = _context.Entry(item);
                var itemType = item.GetType();

                resultCollection.Add(
                    (ExpandoObject) auditEntryMethod  
                        .MakeGenericMethod(itemType)
                        .Invoke(this, [itemEntry, item])!);
            }
            
            return resultCollection;
        }

        var navigationEntry = _context.Entry(navigationValue!);

        var result = (ExpandoObject) auditEntryMethod  
            .MakeGenericMethod(navigationType)
            .Invoke(this, [navigationEntry, navigationValue])!;
        
        return result;
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
