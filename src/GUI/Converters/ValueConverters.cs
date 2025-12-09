using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using PodPilot.Core.Models;

namespace GUI.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class DeviceStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DeviceStatus boolValue)
            switch (boolValue)
                {
                case DeviceStatus.Unpaired:
                    return Visibility.Collapsed;
                default:
                    return Visibility.Visible;
            }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts lid open/closed state to an icon using Segoe MDL2 Assets font.
/// </summary>
public class LidStatusConverter : IValueConverter
{
    // Segoe MDL2 Assets: OpenPane (\uE8A0) for open, ClosePane (\uE89F) for closed
    private const string OpenGlyph = "\uE8A0";
    private const string ClosedGlyph = "\uE89F";

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isOpen)
            return isOpen ? OpenGlyph : ClosedGlyph;
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class TimeAgoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dateTime)
        {
            var span = DateTime.Now - dateTime;
            if (span.TotalSeconds < 60)
                return "just now";
            if (span.TotalMinutes < 60)
                return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24)
                return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class BatteryPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int battery)
            return $"{battery}%";
        return "--";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class SignalStrengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int signalStrength)
            return $"{signalStrength} dBm";
        return "-- dBm";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class NullableBatteryToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int battery)
            return (double)battery;
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts charging state to a battery charging icon using Segoe MDL2 Assets font.
/// </summary>
public class ChargingIconConverter : IValueConverter
{
    // Segoe MDL2 Assets: Battery Charging (\uEA93) or Lightning Bolt (\uE945)
    private const string ChargingGlyph = "\uE945";

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isCharging && isCharging)
            return ChargingGlyph;
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts in-ear state to a headphone icon using Segoe MDL2 Assets font.
/// </summary>
public class InEarIconConverter : IValueConverter
{
    // Segoe MDL2 Assets: Headphone (\uE7F6) or Volume (\uE767)
    private const string InEarGlyph = "\uE7F6";

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool inEar && inEar)
            return InEarGlyph;
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isActive)
            return isActive ? 1.0 : 0.4;
        return 0.4;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class PairingTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool needsPairing && needsPairing)
            return "Device not paired. Click the icon to open Bluetooth settings.";
        return "Connect to this device";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
