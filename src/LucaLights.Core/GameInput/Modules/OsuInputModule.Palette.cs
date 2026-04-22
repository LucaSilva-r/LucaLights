namespace LucaLights.Core.GameInput.Modules;

public sealed partial class OsuInputModule
{
    // Palette state — updated on a background task after each beatmap change, guarded
    // by _syncRoot. Latest palette is published through ColorValues in BuildSnapshot.
    internal CoverArtPalette _palette           = CoverArtPalette.Empty;
    private  string          _paletteChecksum   = string.Empty;
    private  CancellationTokenSource? _paletteCts = null;

    // Called from LoadBeatmap when a new beatmap is detected. Fires a background task to
    // decode the cover art and extract the palette. Previous in-flight work is cancelled so
    // rapid song-select scrolling cannot queue up old extractions.
    //
    // backgroundPath is tosu's directPath.beatmapBackground — relative to songsFolder on
    // both stable (e.g. "1359962 .../snow.jpg") and lazer (e.g. "e/ee/eefe2e67..." content-
    // addressed hash), so the same combine works for either client.
    private void StartPaletteExtraction(string beatmapChecksum, string songsFolder, string backgroundPath)
    {
        if (string.IsNullOrEmpty(beatmapChecksum)) return;

        CancellationTokenSource cts;
        lock (_syncRoot)
        {
            if (string.Equals(_paletteChecksum, beatmapChecksum, StringComparison.Ordinal))
                return;
            _paletteCts?.Cancel();
            _paletteCts?.Dispose();
            cts = new CancellationTokenSource();
            _paletteCts = cts;
        }

        var ct = cts.Token;
        _ = Task.Run(() =>
        {
            try
            {
                if (ct.IsCancellationRequested) return;

                var palette = ExtractPalette(songsFolder, backgroundPath);
                if (ct.IsCancellationRequested) return;

                lock (_syncRoot)
                {
                    if (ct.IsCancellationRequested) return;
                    _palette         = palette;
                    _paletteChecksum = beatmapChecksum;
                }
                var c0 = palette[0];
                _log?.Invoke($"osu: palette ready — color_0=#{c0.R:X2}{c0.G:X2}{c0.B:X2}.");
                PublishCurrentSnapshot();
            }
            catch (Exception ex)
            {
                _log?.Invoke($"osu: palette extraction failed: {ex.Message}");
            }
        }, CancellationToken.None);
    }

    private CoverArtPalette ExtractPalette(string songsFolder, string backgroundPath)
    {
        if (string.IsNullOrEmpty(songsFolder) || string.IsNullOrEmpty(backgroundPath))
        {
            _log?.Invoke("osu: palette skipped — songs folder or background path missing from tosu.");
            return CoverArtPalette.Empty;
        }

        var imagePath = Path.Combine(songsFolder, backgroundPath);
        if (!File.Exists(imagePath))
        {
            _log?.Invoke($"osu: palette skipped — background image not found: {imagePath}");
            return CoverArtPalette.Empty;
        }

        _log?.Invoke($"osu: extracting palette from {backgroundPath}.");
        return CoverArtPaletteExtractor.FromFile(imagePath);
    }

    private void StopPaletteExtraction()
    {
        lock (_syncRoot)
        {
            _paletteCts?.Cancel();
            _paletteCts?.Dispose();
            _paletteCts      = null;
            _palette         = CoverArtPalette.Empty;
            _paletteChecksum = string.Empty;
        }
    }
}
