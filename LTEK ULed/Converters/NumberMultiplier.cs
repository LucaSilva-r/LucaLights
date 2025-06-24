using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LTEK_ULed.Converters
{
    internal class NumberMultiplier : IValueConverter
    {
        /// <summary>Convert from the enum value to an integer value.
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if(value is int intValue && parameter is string strMultiplier && double.TryParse(strMultiplier, out double multiplier))
            {
                // Multiply the integer value by the multiplier
                return intValue * multiplier;
            }
            else if (value is double doubleValue && parameter is string strMultiplier2 && double.TryParse(strMultiplier2, out double multiplier2))
            {
                // Multiply the double value by the multiplier
                return doubleValue * multiplier2;
            }
            return 0;
        }

        /// <summary>Convert from the integer value back to the enum value.
        /// </summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int intValue && parameter is string strMultiplier && double.TryParse(strMultiplier, out double multiplier))
            {
                // Multiply the integer value by the multiplier
                return intValue / multiplier;
            }
            else if (value is double doubleValue && parameter is string strMultiplier2 && double.TryParse(strMultiplier2, out double multiplier2))
            {
                // Multiply the double value by the multiplier
                return doubleValue / multiplier2;
            }
            return 0;
        }
    }
}
