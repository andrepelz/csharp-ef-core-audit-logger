namespace Extensions;

public static class ObjectExtensions
{
    public static object? GetPropertyValue(this object obj, string name)
    {
        if (obj == null) { return null; }

        Type type = obj.GetType();
        var info = type.GetProperty(name);
        if (info == null) { return null; }

        return info.GetValue(obj, null);
    }

    public static T? GetPropertyValue<T>(this object obj, string name)
    {
        var retval = GetPropertyValue(obj, name);
        if (retval == null) { return default; }

        return (T) retval;
    }
}