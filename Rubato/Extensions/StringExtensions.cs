namespace Rubato.Extensions;

public static class StringExtensions
{
    public static string OrDefault(this string? str, string defaultStr) 
        => string.IsNullOrWhiteSpace(str) ? defaultStr : str;
}