using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace FluxKit.App;

/// <summary>
/// User-visible text from Strings/en-US/Resources.resw. XAML uses x:Uid; code uses <see cref="Get"/> and
/// <see cref="Format"/> with whole sentences as format strings, never glued-together pieces.
/// </summary>
internal static class Strings
{
    private static readonly ResourceLoader Loader = new();

    /// <summary>The string for <paramref name="key"/>, or the key itself if it is missing (so gaps are easy to spot).</summary>
    public static string Get(string key)
    {
        string value = Loader.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
