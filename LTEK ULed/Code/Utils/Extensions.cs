using Avalonia.Media;
using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace LTEK_ULed.Code.Utils
{


    public static class StringExt
    {
        public static string? Truncate(this string? value, int maxLength, string truncationSuffix = "…")
        {
            return value?.Length > maxLength
                ? value.Substring(0, maxLength) + truncationSuffix
                : value;
        }
    }

    public static class EnumExtensions
    {
        /// <summary>Returns the value of the DescriptionAttribute associated with the enum value,
        /// or the results of value.ToString() if it has no DescriptionAttribute.
        /// </summary>
        public static string GetDescription(this System.Enum value)
        {
            System.Reflection.FieldInfo fieldInfo = value.GetType().GetField(value.ToString());
            if (fieldInfo == null)
                return value.ToString();
            object[] attribArray = fieldInfo.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
            if (attribArray.Length == 0)
                return value.ToString();
            else
                return ((System.ComponentModel.DescriptionAttribute)attribArray[0]).Description;
        }
    }


    public static class Extension
    {
        public static Color Sum(this Color a, Color b)
        {
            return Color.FromRgb((byte)Clamp(a.R + b.R, 0, 255), (byte)Clamp(a.G + b.G, 0, 255), (byte)Clamp<int>(a.B + b.B, 0, 255));
        }

        public static Color SetBrightness(this Color a, float b)
        {
            return Color.FromRgb((byte)Clamp((int)(a.R * b), 0, 255), (byte)Clamp((int)(a.G * b), 0, 255), (byte)Clamp((int)(a.B * b), 0, 255));
        }


        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static T Map<T>(this T value, T fromSource, T toSource, T fromTarget, T toTarget) where T : INumber<T>
        {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }
    }

    public static class JsonOptions
    {
        public static readonly JsonSerializerOptions jsonSerializerOptionsForPropertyModel = new()
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            {
                Modifiers =
                {
                    ApplyCustomConverterToObjectProperties
                }
            }
        };

        public static readonly JsonSerializerOptions jsonSerializerOptionsForSaving = new()
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            {
                Modifiers =
                {
                    ApplyCustomConverterToObjectProperties
                }
            }
        };

        private static void ApplyCustomConverterToObjectProperties(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Type.FullName == typeof(GradientStop).FullName)
            {
                typeInfo.Properties.First(x => x.Name == nameof(Color)).CustomConverter = new ColorJsonConverter();
            }
        }
    }
}
