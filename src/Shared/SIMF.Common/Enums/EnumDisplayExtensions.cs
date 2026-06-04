using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace SIMF.Common.Enums;

/// <summary>
/// D-110: resolves the localised display name for any enum value carrying a
/// <see cref="DisplayAttribute"/> with <c>ResourceType</c> + <c>Description</c>.
/// Reads the resource string via the resource type's static <c>ResourceManager</c>,
/// using <see cref="CultureInfo.CurrentUICulture"/>. Falls back to the raw enum
/// name when no attribute / no resource is found, so a missing translation never
/// throws.
/// </summary>
public static class EnumDisplayExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var name = value.ToString();
        var field = value.GetType().GetField(name);
        var attr = field?.GetCustomAttribute<DisplayAttribute>();
        if (attr is null) { return name; }

        var resourceType = attr.ResourceType;
        var key = attr.Description ?? attr.Name;
        if (resourceType is null || string.IsNullOrEmpty(key))
        {
            return key ?? name;
        }

        var resourceManagerProperty = resourceType.GetProperty(
            "ResourceManager",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
        if (resourceManagerProperty?.GetValue(null) is not ResourceManager manager)
        {
            return key;
        }
        return manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
