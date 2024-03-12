// Source: https://stackoverflow.com/questions/30688909/how-to-get-primary-key-value-with-entity-framework-core
// (2024/03/12) Edited from: https://gist.github.com/mtherien/39803dd113180bccd7915f97e2ccb7d8
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Extensions;

public static class DbContextKeyExtensions
{
    private static readonly ConcurrentDictionary<Type, IProperty[]> KeyPropertiesByEntityType = new();

    public static IEnumerable<KeyValuePair<string, object>> KeyValuesOf(this EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var keyProperties = entry.Context.GetKeyProperties(entry.Entity.GetType());

        foreach (var keyProperty in keyProperties)
        {
            yield return new KeyValuePair<string, object>(keyProperty.Name, entry.Property(keyProperty.Name).CurrentValue!);
        }
    }

    public static IEnumerable<object> KeyOf<TEntity>(this DbContext context, TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        var entry = context.Entry(entity);
            return entry.KeyOf();
    }

    public static TKey KeyOf<TEntity, TKey>(this DbContext context, TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        var keyParts = context.KeyOf(entity).ToArray();
        if (keyParts.Length > 1)
        {
            throw new InvalidOperationException($"Key is composite and has '{keyParts.Length}' parts.");
        }

        return (TKey)keyParts[0];
    }

    public static IEnumerable<object> KeyOf(this EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var keyProperties = entry.Context.GetKeyProperties(entry.Entity.GetType());

        return keyProperties
            .Select(property => entry.Entity.GetPropertyValue(property.Name))
            .AsEnumerable();
    }

    public static TKey KeyOf<TKey>(this EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var keyParts = entry.KeyOf().ToArray();
        if (keyParts.Length == 0)
        {
            throw new InvalidOperationException($"Key is composite and has '{keyParts.Length}' parts.");
        }

        return (TKey)keyParts[0];
    }

    private static IEnumerable<IProperty> GetKeyProperties(this DbContext context, Type entityType)
    {
        var keyProperties = KeyPropertiesByEntityType.GetOrAdd(
            entityType,
            t => context.FindPrimaryKeyProperties(entityType).ToArray());
        return keyProperties;
    }

    private static IEnumerable<IProperty> FindPrimaryKeyProperties(this DbContext dbContext, Type entityType)
    {
        return dbContext.Model.FindEntityType(entityType)!.FindPrimaryKey()!.Properties;
    }

    private static object GetPropertyValue<T>(this T entity, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(propertyName);

        if (string.IsNullOrEmpty(propertyName))
        {
            throw new ArgumentException($"{nameof(propertyName)} must have value", nameof(propertyName));
        }

        return typeof(T).GetProperty(propertyName)?.GetValue(entity, null)!;
    }
}