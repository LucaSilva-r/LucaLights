using Avalonia.Controls.Documents;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ColorMine;
using ColorMine.ColorSpaces;
using Extensions;
using System.Xml.Serialization;

namespace LTEK_ULed.Code
{
    public static class LightingManager
    {
        private static LightThread? _lightThread;
        private static Thread? thread;

        private static CancellationTokenSource run;

        private static Dictionary<GameButton, List<Segment>> buttonMappings = new();
        private static Dictionary<CabinetLight, List<Segment>> cabinetMappings = new();

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
            int counter;
            float[] inputTimers = new float[4];
            float[] visualTimers = new float[4];
            float decreaseSpeed = 0.2f;
            float increaseSpeed = 0.5f;

            static Device[] devices;

            Stopwatch animationGlobalTimer = new Stopwatch();

            CancellationToken token;


            int wait = 25;

            public LightThread(CancellationToken token)
            {
                this.token = token;
                animationGlobalTimer.Start();

                devices = [
                    new Device("192.168.1.20", new Segment[]
                        {
                            new Segment(36, GameButton.GAME_BUTTON_CUSTOM_01, 0),
                            new Segment(36, GameButton.GAME_BUTTON_CUSTOM_03, 0),
                            new Segment(36, GameButton.GAME_BUTTON_CUSTOM_04, 0),
                            new Segment(36, GameButton.GAME_BUTTON_CUSTOM_02, 0),

                        }),
                    new Device("192.168.1.21", [new Segment(1,0,CabinetLight.LIGHT_MARQUEE_UP_LEFT)]),
                    new Device("192.168.1.22", [new Segment(1,0,CabinetLight.LIGHT_BASS_RIGHT)]),
                    new Device("192.168.1.23", [new Segment(1,0,CabinetLight.LIGHT_MARQUEE_LR_RIGHT)]),
                    new Device("192.168.1.24", [new Segment(1,0,CabinetLight.LIGHT_BASS_LEFT)]),
                    new Device("192.168.1.25", [new Segment(1,0,CabinetLight.LIGHT_MARQUEE_LR_LEFT)]),
                    new Device("192.168.1.26", [new Segment(1,0,CabinetLight.LIGHT_MARQUEE_UP_RIGHT)])

                   ];


                int totalSegments = 0;
                foreach (Device device in devices)
                {
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
                                buttonMappings[i].Add(segment);
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

            }

            public void Run()
            {
                Stopwatch sw = new Stopwatch();

                Debug.WriteLine("Started light thread.");
                while (!token.IsCancellationRequested)
                {

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

                    foreach (Device device in devices)
                    {
                        device.Send();
                    }

                    while (sw.ElapsedMilliseconds < wait || (!GameState.gameState.Connected && !token.IsCancellationRequested));

                    sw.Reset();
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
                            FillSegment(segment, Color.Black);
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

                        int index = indexj +indexy; 
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

                        FillSegment(segment, Color.Black);
                        CollapsingAnimation(segment,4, inputTimers[index], FireAnimation(inputTimers[index]));

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

                return Color.FromArgb((int)c.R, (int)c.G, (int)c.B);
            }

            Color RainbowAnimation(float t)
            {
                Hsv rainbow = new Hsv();
                rainbow.S = 1;
                rainbow.V = 1;
                rainbow.H = t;
                IRgb c = rainbow.ToRgb();

                return Color.FromArgb((int)c.R, (int)c.G, (int)c.B);
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

