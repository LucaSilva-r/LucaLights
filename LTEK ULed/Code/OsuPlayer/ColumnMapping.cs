using System.Text.Json.Serialization;

namespace LTEK_ULed.Code.OsuPlayer;

public class ColumnMapping
{
    [JsonPropertyName("column")]
    public int Column { get; set; }

    [JsonPropertyName("gameButtons")]
    public int GameButtons { get; set; }

    [JsonPropertyName("cabinetLights")]
    public int CabinetLights { get; set; }
}
