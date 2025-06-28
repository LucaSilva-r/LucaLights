using Avalonia.Media;
using Avalonia.Threading;
using ColorMine.ColorSpaces;
using LTEK_ULed.Code.Utils;
using LTEK_ULed.ViewModels;
using LTEK_ULed.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Xml.XPath;

namespace LTEK_ULed.Code
{
    public static class LightingManager
    {
        private static LightThread? _lightThread;
        private static Thread? thread;

        private static CancellationTokenSource? run;

        public const int targetFps = 60;


        public static void Start()
        {
            run?.Cancel();

            run = new CancellationTokenSource();
            _lightThread = new LightThread(run.Token);

            thread = new Thread(new ThreadStart(_lightThread.Run));
            thread.Start();
        }

        public static void Stop()
        {
            run?.Cancel();
            thread?.Join();
        }

        private class LightThread
        {
            //static Device[] devices;

            Stopwatch animationGlobalTimer = new Stopwatch();

            CancellationToken token;

            public LightThread(CancellationToken token)
            {
                this.token = token;
                animationGlobalTimer.Start();

                lock (Settings.Instance!)
                {
                    foreach (LightEffect effect in Settings.Instance!.Effects)
                    {
                        effect.Recalculate();
                    }
                }
            }

            bool cleared = true;

            private void Setup()
            {
                foreach (LightEffect effect in Settings.Instance!.Effects)
                {
                    effect.Recalculate();
                }
                Dispatcher.UIThread.Post(() => MainWindow.Instance!.UpdateLeds(true));
                Settings.Instance!.ClearDirty();

            }

            public void Run()
            {
                const int targetFrameTimeMs = 1000 / targetFps; // 16 ms per frame approx

                Stopwatch sw = new Stopwatch();
                //Stopwatch logTimer = new Stopwatch();

                Debug.WriteLine("Started light thread.");


                //int frameCount = 0;
                //long totalFrameTime = 0;

                //logTimer.Start();

                while (!token.IsCancellationRequested)
                {
                    sw.Restart();

                    while ((!GameState.gameState.Connected && !MainViewModel.Instance!.debug && !token.IsCancellationRequested))
                    {
                        Thread.Sleep(1);
                        if (!cleared)
                        {
                            lock (Settings.Lock)
                            {
                                foreach (Device device in Settings.Instance!.Devices)
                                {
                                    foreach (var segment in device.Segments)
                                    {
                                        Array.Clear(segment.leds);
                                    }
                                    device.Send();

                                }

                                foreach (var item in Settings.Instance.Effects)
                                {
                                    item.Clear();
                                }

                                Dispatcher.UIThread.Post(() => MainWindow.Instance!.UpdateLeds());

                                cleared = true;
                            }
                        }
                    }

                    cleared = false;

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }


                    GameButton gameButton;
                    CabinetLight cabinetLight;

                    lock (GameState.gameState)
                    {
                        gameButton = GameState.gameState.state.gameButton;
                        cabinetLight = GameState.gameState.state.cabinetLight;
                    }

                    lock (Settings.Lock)
                    {
                        foreach (Device device in Settings.Instance!.Devices)
                        {
                            foreach (var segment in device.Segments)
                            {
                                Array.Clear(segment.leds);
                            }
                        }
                        foreach (LightEffect effect in Settings.Instance.Effects)
                        {
                            effect.Render(gameButton, cabinetLight);
                            for (int i = 0; i < effect.Segments.Count; i++)
                            {
                                for (int j = 0; j < effect.Segments[i].leds.Length; j++)
                                {
                                    Color col = effect.Segments[i].leds[j];
                                    Color col2 = effect.leds[i][j];

                                    effect.Segments[i].leds[j] = Color.FromRgb(
                                        (byte)Math.Clamp(col.R + col2.R, 0, 255),
                                        (byte)Math.Clamp(col.G + col2.G, 0, 255),
                                        (byte)Math.Clamp(col.B + col2.B, 0, 255)
                                        );
                                }
                            }
                        }

                        if (Settings.Instance!.Dirty)
                        {
                            Setup();

                        }
                        if (MainViewModel.Instance!.lightOutput)
                        {
                            foreach (Device device in Settings.Instance!.Devices)
                            {
                                device.Send();
                            }
                        }
                    }

                    Dispatcher.UIThread.Post(() => MainWindow.Instance!.UpdateLeds());


                    // Calculate time to wait
                    long elapsed = sw.ElapsedMilliseconds;
                    int remaining = targetFrameTimeMs - (int)elapsed;

                    // Sleep most of the remaining time
                    if (remaining > 1)
                    {
                        Thread.Sleep(remaining - 1);
                    }

                    // Spin for the last ~1 ms to maintain accuracy
                    while (sw.ElapsedMilliseconds < targetFrameTimeMs)
                    {
                        Thread.SpinWait(64);
                    }

                    //// Logging average frame time
                    //totalFrameTime += sw.ElapsedMilliseconds;
                    //frameCount++;

                    //if (logTimer.ElapsedMilliseconds >= 1000)
                    //{
                    //    double avg = totalFrameTime / (double)frameCount;
                    //    Debug.WriteLine($"Average frame time: {avg:F2} ms ({frameCount} frames)");
                    //    frameCount = 0;
                    //    totalFrameTime = 0;
                    //    logTimer.Restart();
                    //}
                }
                sw.Reset();

                Debug.WriteLine("LightThread Exited");
            }
        }
    }
}



