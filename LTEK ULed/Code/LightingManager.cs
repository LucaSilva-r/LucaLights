using Avalonia.Controls.Documents;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ColorMine;
using ColorMine.ColorSpaces;
using Extensions;
using System.Xml.Serialization;
using System.Text.Json;
using LTEK_ULed.Views;
using LTEK_ULed.ViewModels;
using Avalonia.Media;
using Avalonia.Threading;

namespace LTEK_ULed.Code
{
    public static class LightingManager
    {
        private static LightThread? _lightThread;
        private static Thread? thread;

        private static CancellationTokenSource? run;



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
            private Dictionary<GameButton, List<Segment>> buttonMappings = new();
            private Dictionary<CabinetLight, List<Segment>> cabinetMappings = new();

            int counter;
            float[] inputTimers = new float[4];
            float[] visualTimers = new float[4];
            float decreaseSpeed = 0.2f;
            float increaseSpeed = 0.5f;

            //static Device[] devices;

            Stopwatch animationGlobalTimer = new Stopwatch();

            CancellationToken token;


            int wait = 1000 / 60;

            public LightThread(CancellationToken token)
            {
                this.token = token;
                animationGlobalTimer.Start();
                Setup();
            }


            private void Setup()
            {
                buttonMappings = new();
                cabinetMappings = new();

                int totalSegments = 0;
                foreach (Device device in Settings.Instance!.devices)
                {
                    device.Recalculate();
                    totalSegments += device.segments.Count();

                    foreach (Segment segment in device.segments)
                    {
                        foreach (GameButton i in Enum.GetValues(typeof(GameButton)))
                        {
                            if (segment.buttonMapping.HasFlag(i) && i != 0)
                            {
                                if (!buttonMappings.ContainsKey(i))
                                {
                                    buttonMappings.Add(i, new List<Segment>());
                                }
                                if (!buttonMappings[i].Contains(segment))
                                {
                                    buttonMappings[i].Add(segment);
                                }
                            }
                        }
                        foreach (CabinetLight i in Enum.GetValues(typeof(CabinetLight)))
                        {
                            if (segment.cabinetMapping.HasFlag(i) && i != 0)
                            {
                                if (!cabinetMappings.ContainsKey(i))
                                {
                                    cabinetMappings.Add(i, new List<Segment>());
                                }
                                cabinetMappings[i].Add(segment);
                            }
                        }
                    }
                }

                inputTimers = new float[totalSegments];
                visualTimers = new float[totalSegments];

                Settings.Instance!.ClearDirty();
            }


            public void Run()
            {
                Stopwatch sw = new Stopwatch();

                Debug.WriteLine("Started light thread.");

                sw.Start();

                while (!token.IsCancellationRequested)
                {

                    while (sw.ElapsedMilliseconds < wait || (!GameState.gameState.Connected && !MainViewModel.Instance!.debug && !token.IsCancellationRequested)) { 

                        //Thread.Sleep(1); 
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    sw.Reset();
                    sw.Start();

                    GameButton gameButton;
                    CabinetLight cabinetLight;

                    lock (GameState.gameState)
                    {
                        gameButton = GameState.gameState.state.gameButton;
                        cabinetLight = GameState.gameState.state.cabinetLight;
                    }

                    ProcessVisual(cabinetLight);
                    ProcessInput(gameButton);

                    lock (Settings.Instance!)
                    {
                        if (Settings.Instance!.Dirty)
                        {
                            Setup();
                            Dispatcher.UIThread.Post(() => MainWindow.Instance!.UpdateLeds(true));
                        }
                        if (MainViewModel.Instance!.lightOutput)
                        {
                            foreach (Device device in Settings.Instance!.devices)
                            {
                                device.Send();
                            }
                        }
                    }

                    Dispatcher.UIThread.Post(()=>MainWindow.Instance!.UpdateLeds());

                }
                sw.Reset();

                Debug.WriteLine("LightThread Exited");
            }

            void ProcessVisual(CabinetLight cabinetLight)
            {

                foreach (KeyValuePair<CabinetLight, List<Segment>> entry in cabinetMappings)
                {
                    bool value = cabinetLight.HasFlag(entry.Key);

                    foreach (Segment segment in entry.Value)
                    {
                        if (value)
                        {
                            FillSegment(segment, RainbowAnimation(animationGlobalTimer.ElapsedMilliseconds / 10f));
                        }
                        else
                        {
                            FillSegment(segment, Color.FromRgb(0, 0, 0));
                        }
                    }
                }
            }


            void FillSegment(Segment segment, Color color)
            {
                for (int i = 0; i < segment.leds.Length; i++)
                {
                    segment.leds[i] = color;
                }
            }


            void ProcessInput(GameButton gameButton)
            {
                int indexy = 0;

                foreach (KeyValuePair<GameButton, List<Segment>> entry in buttonMappings)
                {
                    bool value = gameButton.HasFlag(entry.Key);

                    int indexj = 0;
                    foreach (Segment segment in entry.Value)
                    {

                        int index = indexj + indexy;
                        if (value)
                        {
                            inputTimers[index] = Math.Clamp(inputTimers[index] + increaseSpeed, 0, 100);
                        }
                        else if (inputTimers[index] > 1)
                        {
                            inputTimers[index] = 1;
                        }
                        else
                        {
                            inputTimers[index] = Math.Clamp(inputTimers[index] - decreaseSpeed, 0, 100);
                        }

                        FillSegment(segment, Color.FromRgb(0, 0, 0));
                        CollapsingAnimation(segment, 4, inputTimers[index], FireAnimation(inputTimers[index]));

                        indexj++;
                    }
                    indexy++;
                }
            }

            void CollapsingAnimation(Segment segment, int numSegments, float time, Color color)
            {
                time = Math.Clamp(time, 0, 1);
                int length = (int)Math.Ceiling(Extension.Map(time, 0, 1, 0, segment.leds.Length));

                for (int i = 0; i < length; i++)
                {
                    segment.leds[i] = color;
                }
            }

            Color FireAnimation(float t)
            {
                Hsv fire = new Hsv();
                fire.S = Extension.Map(Math.Clamp(t, 0, 20), 0, 20, 1, 0);
                fire.V = 1;
                fire.H = 0;
                IRgb c = fire.ToRgb();

                return Color.FromRgb((byte)c.R, (byte)c.G, (byte)c.B);
            }

            Color RainbowAnimation(float t)
            {
                Hsv rainbow = new Hsv();
                rainbow.S = 1;
                rainbow.V = 1;
                rainbow.H = t;
                IRgb c = rainbow.ToRgb();

                return Color.FromRgb((byte)c.R, (byte)c.G, (byte)c.B);
            }

            bool IsBitSet(int b, int pos)
            {
                return (b & (1 << pos)) != 0;
            }
        }

        struct Pad
        {
            public int nSegments;
            public int segmentLen;
            public int offset;
            public GameButton gameButton;

            public CabinetLight cabinetLight;

            public Pad(int nSegments, int segmentLen, int offset, GameButton gameButton, CabinetLight cabinetLight)
            {
                this.nSegments = nSegments;
                this.segmentLen = segmentLen;
                this.offset = offset;
                this.gameButton = gameButton;
                this.cabinetLight = cabinetLight;
            }
        }
    }
}

namespace Extensions
{
    using System.Drawing;
    using System.Numerics;

    public static class Extension
    {
        public static Color Sum(this Color a, Color b)
        {
            return Color.FromArgb(Clamp(a.R + b.R, 0, 255), Clamp(a.G + b.G, 0, 255), Clamp(a.B + b.B, 0, 255));
        }

        public static Color SetBrightness(this Color a, float b)
        {
            return Color.FromArgb(Clamp((int)(a.R * b), 0, 255), Clamp((int)(a.G * b), 0, 255), Clamp((int)(a.B * b), 0, 255));
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
}

