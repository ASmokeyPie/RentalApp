using System.Globalization;

namespace RentalApp.Converters;

/// <summary>
/// Converts a string to a boolean value
/// Returns true if the string is not null, empty, or whitespace
/// Returns false if the string is null, empty, or whitespace
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            return !string.IsNullOrWhiteSpace(stringValue);
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // ConvertBack is typically not used for this converter
        // but we'll implement it for completeness
        if (value is bool boolValue)
        {
            return boolValue ? "true" : string.Empty;
        }

        return string.Empty;
    }
}