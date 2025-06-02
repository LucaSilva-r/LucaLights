using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LTEK_ULed.Converters
{
    public class FlagsEnumValueConverter : IValueConverter
    {
        private int targetValue;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int mask = (int)parameter!;
            this.targetValue = (int)value!;
            return ((mask & this.targetValue) != 0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            this.targetValue ^= (int)parameter!;
            return Enum.Parse(targetType, this.targetValue.ToString());
        }

    }
}
