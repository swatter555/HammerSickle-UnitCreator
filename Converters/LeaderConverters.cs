using Avalonia.Data.Converters;
using Avalonia.Media;
using HammerAndSickle.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HammerSickle.UnitCreator.Converters
{
    /// <summary>
    /// Converts leader status (assignment + grade) to status indicator color
    /// </summary>
    public class LeaderStatusToColorConverter : IMultiValueConverter
    {
        public static readonly LeaderStatusToColorConverter Instance = new();

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 2 &&
                values[0] is bool isAssigned &&
                values[1] is CommandGrade grade)
            {
                if (isAssigned)
                {
                    return grade switch
                    {
                        CommandGrade.TopGrade => Colors.Gold,
                        CommandGrade.SeniorGrade => Colors.Orange,
                        CommandGrade.JuniorGrade => Colors.Blue,
                        _ => Colors.Gray
                    };
                }
                else
                {
                    return Colors.Green; // Available for assignment
                }
            }
            return Colors.Gray;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean assignment status to text
    /// </summary>
    public class BooleanToAssignmentTextConverter : IValueConverter
    {
        public static readonly BooleanToAssignmentTextConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is bool isAssigned ? (isAssigned ? "ASSIGNED" : "AVAILABLE") : "UNKNOWN";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString()?.ToUpper() == "ASSIGNED";
        }
    }

    /// <summary>
    /// Converts boolean assignment status to color
    /// </summary>
    public class BooleanToAssignmentColorConverter : IValueConverter
    {
        public static readonly BooleanToAssignmentColorConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is bool isAssigned ?
                (isAssigned ? Colors.OrangeRed : Colors.Green) :
                Colors.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}