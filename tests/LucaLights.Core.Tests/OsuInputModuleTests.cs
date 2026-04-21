using System.Reflection;
using LucaLights.Core.GameInput;
using LucaLights.Core.GameInput.Modules;

namespace LucaLights.Core.Tests;

public sealed class OsuInputModuleTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void DefinitionIncludesMusicPlayingSystemChannel()
    {
        var module = new OsuInputModule("ws://127.0.0.1:24050", autoManageProcess: false);

        var channel = Assert.Single(module.GetDefinition().Channels,
            channel => channel.Key == "raw.osu.music_playing");

        Assert.Equal("Music Playing", channel.Label);
        Assert.Equal("System", channel.Group);
        Assert.Equal("Raw / System", channel.Category);
    }

    [Fact]
    public void MusicPlayingTracksBeatmapTimeMovementOnly()
    {
        var module = new OsuInputModule("ws://127.0.0.1:24050", autoManageProcess: false);

        try
        {
            var first = ProcessV2(module, V2(liveTimeMs: 1_000, gamePaused: true));
            Assert.False(first.GetBool("raw.osu.music_playing"));
            Assert.True(GetField<bool>(module, "_noteEnginePaused"));

            var moving = ProcessV2(module, V2(liveTimeMs: 1_100, gamePaused: true));
            Assert.True(moving.GetBool("raw.osu.music_playing"));
            Assert.True(moving.GetBool("raw.osu.paused"));
            Assert.False(GetField<bool>(module, "_noteEnginePaused"));

            var stopped = ProcessV2(module, V2(liveTimeMs: 1_100, gamePaused: false));
            Assert.False(stopped.GetBool("raw.osu.music_playing"));
            Assert.False(stopped.GetBool("raw.osu.paused"));
            Assert.True(GetField<bool>(module, "_noteEnginePaused"));
        }
        finally
        {
            Invoke(module, "StopNoteEngine");
        }
    }

    private static InputSnapshot ProcessV2(OsuInputModule module, TosuV2Data data)
    {
        SetField(module, "_v2Connected", true);
        SetField(module, "_latestV2", data);
        Invoke(module, "OnV2DataReceived", data);

        var syncRoot = GetField<object>(module, "_syncRoot");
        lock (syncRoot)
        {
            return Invoke<InputSnapshot>(module, "BuildSnapshot");
        }
    }

    private static TosuV2Data V2(int liveTimeMs, bool gamePaused)
    {
        return new TosuV2Data
        {
            Game = new TosuGame
            {
                Paused = gamePaused
            },
            State = new TosuState
            {
                Number = TosuStateNumber.Playing
            },
            Beatmap = new TosuBeatmap
            {
                Checksum = "test-checksum",
                Artist = "Artist",
                Title = "Title",
                Version = "Version",
                Mode = new TosuMode
                {
                    Number = TosuModeNumber.Standard
                },
                Time = new TosuBeatmapTime
                {
                    Live = liveTimeMs,
                    Mp3Length = 120_000
                }
            },
            Folders = new TosuFolders(),
            DirectPath = new TosuDirectPath()
        };
    }

    private static void SetField<T>(object target, string name, T value)
    {
        var field = target.GetType().GetField(name, PrivateInstance)
                    ?? throw new MissingFieldException(target.GetType().FullName, name);
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, PrivateInstance)
                    ?? throw new MissingFieldException(target.GetType().FullName, name);
        return (T)field.GetValue(target)!;
    }

    private static void Invoke(object target, string name, params object[] args)
    {
        _ = Invoke<object?>(target, name, args);
    }

    private static T Invoke<T>(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, PrivateInstance)
                     ?? throw new MissingMethodException(target.GetType().FullName, name);
        return (T)method.Invoke(target, args)!;
    }
}
