using System.Text.Json.Serialization;

namespace LucaLights.Core.GameInput.Modules;

// ---------------------------------------------------------------------------
// tosu /websocket/v2 response models
// ---------------------------------------------------------------------------

public sealed class TosuV2Data
{
    [JsonPropertyName("game")]       public TosuGame       Game       { get; set; } = new();
    [JsonPropertyName("state")]      public TosuState      State      { get; set; } = new();
    [JsonPropertyName("beatmap")]    public TosuBeatmap    Beatmap    { get; set; } = new();
    [JsonPropertyName("play")]       public TosuPlay       Play       { get; set; } = new();
    [JsonPropertyName("folders")]    public TosuFolders    Folders    { get; set; } = new();
    [JsonPropertyName("directPath")] public TosuDirectPath DirectPath { get; set; } = new();
}

public sealed class TosuGame
{
    [JsonPropertyName("focused")] public bool Focused { get; set; }
    [JsonPropertyName("paused")]  public bool Paused  { get; set; }
}

public sealed class TosuState
{
    [JsonPropertyName("number")] public int    Number { get; set; }
    [JsonPropertyName("name")]   public string Name   { get; set; } = string.Empty;
}

public static class TosuStateNumber
{
    public const int MainMenu       = 0;
    public const int Edit           = 1;
    public const int Playing        = 2;
    public const int Exit           = 3;
    public const int SelectEdit     = 4;
    public const int SelectPlay     = 5; // Song select (play)
    public const int Ranking        = 7; // Results screen
}

public sealed class TosuBeatmap
{
    [JsonPropertyName("isKiai")]    public bool           IsKiai   { get; set; }
    [JsonPropertyName("isBreak")]   public bool           IsBreak  { get; set; }
    [JsonPropertyName("checksum")]  public string         Checksum { get; set; } = string.Empty;
    [JsonPropertyName("mode")]      public TosuMode       Mode     { get; set; } = new();
    [JsonPropertyName("time")]      public TosuBeatmapTime Time    { get; set; } = new();
    [JsonPropertyName("artist")]    public string         Artist   { get; set; } = string.Empty;
    [JsonPropertyName("title")]     public string         Title    { get; set; } = string.Empty;
    [JsonPropertyName("version")]   public string         Version  { get; set; } = string.Empty;
    [JsonPropertyName("stats")]     public TosuBeatmapStats Stats  { get; set; } = new();
}

public sealed class TosuMode
{
    [JsonPropertyName("number")] public int    Number { get; set; }
    [JsonPropertyName("name")]   public string Name   { get; set; } = string.Empty;
}

public static class TosuModeNumber
{
    public const int Standard = 0;
    public const int Taiko    = 1;
    public const int Catch    = 2;
    public const int Mania    = 3;
}

public sealed class TosuBeatmapTime
{
    [JsonPropertyName("live")]        public int Live        { get; set; }
    [JsonPropertyName("firstObject")] public int FirstObject { get; set; }
    [JsonPropertyName("lastObject")]  public int LastObject  { get; set; }
    [JsonPropertyName("mp3Length")]   public int Mp3Length   { get; set; }
}

public sealed class TosuBeatmapStats
{
    [JsonPropertyName("stars")]    public TosuStars     Stars    { get; set; } = new();
    [JsonPropertyName("cs")]       public TosuStatValue Cs       { get; set; } = new();
    [JsonPropertyName("bpm")]      public TosuBpm       Bpm      { get; set; } = new();
    [JsonPropertyName("maxCombo")] public int           MaxCombo { get; set; }
}

public sealed class TosuStars
{
    [JsonPropertyName("live")]  public float Live  { get; set; }
    [JsonPropertyName("total")] public float Total { get; set; }
}

public sealed class TosuStatValue
{
    [JsonPropertyName("original")] public float Original { get; set; }
}

public sealed class TosuBpm
{
    [JsonPropertyName("realtime")] public float Realtime { get; set; }
    [JsonPropertyName("common")]   public float Common   { get; set; }
    [JsonPropertyName("min")]      public float Min      { get; set; }
    [JsonPropertyName("max")]      public float Max      { get; set; }
}

public sealed class TosuPlay
{
    [JsonPropertyName("failed")]       public bool           Failed       { get; set; }
    [JsonPropertyName("score")]        public int            Score        { get; set; }
    [JsonPropertyName("accuracy")]     public float          Accuracy     { get; set; }
    [JsonPropertyName("healthBar")]    public TosuHealthBar  HealthBar    { get; set; } = new();
    [JsonPropertyName("combo")]        public TosuCombo      Combo        { get; set; } = new();
    [JsonPropertyName("pp")]           public TosuPp         Pp           { get; set; } = new();
    [JsonPropertyName("unstableRate")] public float          UnstableRate { get; set; }
    [JsonPropertyName("playerName")]   public string         PlayerName   { get; set; } = string.Empty;
}

public sealed class TosuHealthBar
{
    [JsonPropertyName("normal")] public float Normal { get; set; }
    [JsonPropertyName("smooth")] public float Smooth { get; set; }
}

public sealed class TosuCombo
{
    [JsonPropertyName("current")] public int Current { get; set; }
    [JsonPropertyName("max")]     public int Max     { get; set; }
}

public sealed class TosuPp
{
    [JsonPropertyName("current")] public float Current { get; set; }
    [JsonPropertyName("fc")]      public float Fc      { get; set; }
}

public sealed class TosuFolders
{
    [JsonPropertyName("songs")] public string Songs { get; set; } = string.Empty;
}

public sealed class TosuDirectPath
{
    [JsonPropertyName("beatmapFile")]       public string BeatmapFile       { get; set; } = string.Empty;
    [JsonPropertyName("beatmapBackground")] public string BeatmapBackground { get; set; } = string.Empty;
}

// ---------------------------------------------------------------------------
// tosu /websocket/v2/precise response models
// ---------------------------------------------------------------------------

public sealed class TosuPreciseData
{
    [JsonPropertyName("currentTime")] public int                    CurrentTime { get; set; }
    [JsonPropertyName("keys")]        public TosuKeyOverlay         Keys        { get; set; } = new();
    [JsonPropertyName("hitErrors")]   public List<double>              HitErrors   { get; set; } = [];
    [JsonPropertyName("tourney")]     public List<TosuPreciseTourney> Tourney   { get; set; } = [];
}

public sealed class TosuPreciseTourney
{
    [JsonPropertyName("ipcId")]     public int           IpcId     { get; set; }
    [JsonPropertyName("keys")]      public TosuKeyOverlay Keys     { get; set; } = new();
    [JsonPropertyName("hitErrors")] public List<int>     HitErrors { get; set; } = [];
}

public sealed class TosuKeyOverlay
{
    [JsonPropertyName("k1")] public TosuKeyState K1 { get; set; } = new();
    [JsonPropertyName("k2")] public TosuKeyState K2 { get; set; } = new();
    [JsonPropertyName("m1")] public TosuKeyState M1 { get; set; } = new();
    [JsonPropertyName("m2")] public TosuKeyState M2 { get; set; } = new();
}

public sealed class TosuKeyState
{
    [JsonPropertyName("isPressed")] public bool IsPressed { get; set; }
    [JsonPropertyName("count")]     public int  Count     { get; set; }
}

// ---------------------------------------------------------------------------
// .osu hit object representation (all modes)
// ---------------------------------------------------------------------------

public sealed class OsuHitObject
{
    public int              Column      { get; set; } // mania: 0-based col; taiko: 0=don, 1=kat
    public int              StartTimeMs { get; set; }
    public int              EndTimeMs   { get; set; }
    public bool             IsHold      { get; set; }
    public OsuHitObjectType Type        { get; set; }
}

public enum OsuHitObjectType
{
    Circle,
    Slider,
    Spinner,
    Hold,        // mania hold note
    TaikoDon,
    TaikoKat,
    TaikoDrumroll, // yellow slider — roll the drum
    TaikoDenden,   // spinner — alternate don/kat
    CatchFruit,
}
