using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LTEK_ULed.Code.Utils
{
    public class ColorJsonConverter : JsonConverter<Color>
    {
        // This method handles writing the C# Color object to JSON.
        // The default behavior is already what we want, but it's good practice
        // to implement it for completeness.
        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("A", value.A);
            writer.WriteNumber("R", value.R);
            writer.WriteNumber("G", value.G);
            writer.WriteNumber("B", value.B);
            writer.WriteEndObject();
        }

        // This is the crucial method that fixes your problem.
        // It reads the JSON object and constructs a new Color struct.
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            byte a = 255; // Default to opaque if 'A' is not specified
            byte r = 0;
            byte g = 0;
            byte b = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    // We have read all the properties.
                    // Now create the Color using the constructor.
                    return Color.FromArgb(a, r, g, b);
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string? propertyName = reader.GetString();
                    reader.Read(); // Move to the property value

                    switch (propertyName)
                    {
                        case "A":
                            a = reader.GetByte();
                            break;
                        case "R":
                            r = reader.GetByte();
                            break;
                        case "G":
                            g = reader.GetByte();
                            break;
                        case "B":
                            b = reader.GetByte();
                            break;
                    }
                }
            }

            throw new JsonException("Expected EndObject token");
        }
    }
}
