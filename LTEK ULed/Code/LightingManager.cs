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

namespace LTEK_ULed.Code
{
    public static class LightingManager
    {
        public static Color[] leds = new Color[144];
        private static Pad[] squares = new Pad[4];

        private static LightThread? _lightThread;
        private static Thread? thread;

        private static CancellationTokenSource run;

        //temporary
        const int SEGMENT_LEN = 9;
        const int SQUARE_LEN = SEGMENT_LEN * 4;

        const int UP = 2;
        const int UP_OFFSET = SQUARE_LEN;

        const int RIGHT = 1;
        const int RIGHT_OFFSET = SQUARE_LEN * 3;

        const int LEFT = 0;
        const int LEFT_OFFSET = 0;

        const int DOWN = 3;
        const int DOWN_OFFSET = SQUARE_LEN * 2;
        //temporary

        public static void Setup()
        {

        }

        public static void Start()
        {
            squares[0] = new Pad(4, SEGMENT_LEN, UP_OFFSET, UP);
            squares[1] = new Pad(4, SEGMENT_LEN, LEFT_OFFSET, LEFT);
            squares[2] = new Pad(4, SEGMENT_LEN, RIGHT_OFFSET, RIGHT);
            squares[3] = new Pad(4, SEGMENT_LEN, DOWN_OFFSET, DOWN);

            run?.Cancel();

            run = new CancellationTokenSource();
            _lightThread = new LightThread(run.Token, PipeManager.light_events, leds, squares);

            thread = new Thread(new ThreadStart(_lightThread.Run));
            thread.Start();
        }

        public static void Stop() {
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

            byte[] light_events;

            Stopwatch animationGlobalTimer = new Stopwatch();

            CancellationToken token;

            Color[] leds;
            Pad[] squares;

            public LightThread(CancellationToken token, byte[] light_events, Color[] leds, Pad[] squares)
            {
                this.token = token;
                animationGlobalTimer.Start();
                this.light_events = light_events;
                this.leds = leds;
                this.squares = squares;
            }

            public void Run()
            {
                Debug.WriteLine("Started light thread.");
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(16);
                    lock (leds)
                    {
                        for (int i = 0; i < leds.Length; i++)
                        {
                            leds[i] = Color.Black;
                        }
                        ProcessVisual();
                        ProcessInput();
                    }
                }
                Debug.WriteLine("LightThread Exited");
            }

            void ProcessVisual()
            {
                foreach (Pad i in squares)
                {
                    if (IsBitSet(light_events[0], i.bitMask))
                    {
                        visualTimers[i.bitMask] = Math.Clamp(visualTimers[i.bitMask] + increaseSpeed, 0, 100);
                        FillSquare(i.offset, i.segmentLen, i.nSegments, RainbowAnimation(animationGlobalTimer.ElapsedMilliseconds / 10f));

                    }
                    else if (inputTimers[i.bitMask] > 1)
                    {
                        visualTimers[i.bitMask] = 1;
                    }
                    else
                    {
                        visualTimers[i.bitMask] = Math.Clamp(visualTimers[i.bitMask] - decreaseSpeed, 0, 100);
                    }
                }
            }

            void ProcessInput()
            {
                foreach (Pad i in squares)
                {
                    if (IsBitSet(light_events[3], i.bitMask))
                    {
                        inputTimers[i.bitMask] = Math.Clamp(inputTimers[i.bitMask] + increaseSpeed, 0, 100); ; ;
                    }
                    else if (inputTimers[i.bitMask] > 1)
                    {
                        inputTimers[i.bitMask] = 1;
                    }
                    else
                    {
                        inputTimers[i.bitMask] = Math.Clamp(inputTimers[i.bitMask] - decreaseSpeed, 0, 100);
                    }

                    CollapsingAnimation(i.offset, i.segmentLen, i.nSegments, inputTimers[i.bitMask], FireAnimation(inputTimers[i.bitMask]));
                }
            }

            void CollapsingAnimation(int offset, int segmentLength, int numSegments, float time, Color color)
            {
                time = Math.Clamp(time, 0, 1);
                int length = (int)Math.Ceiling(Extension.Map(time, 0, 1, 0, segmentLength));

                for (int i = 0; i < numSegments; i++)
                {
                    for (int j = offset + segmentLength * i; j < offset + segmentLength * i + length; j++)
                    {
                        leds[j] = leds[j].Sum(color);
                    }
                }
            }

            void FillSquare(int offset, int segmentLength, int numSegments, Color color)
            {

                for (int i = 0; i < numSegments; i++)
                {
                    for (int j = offset + segmentLength * i; j < offset + segmentLength * (i + 1); j++)
                    {
                        leds[j] = leds[j].Sum(color);
                    }
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

            bool IsBitSet(byte b, int pos)
            {
                return (b & (1 << pos)) != 0;
            }
        }

        struct Pad
        {
            public int nSegments;
            public int segmentLen;
            public int offset;
            public int bitMask;

            public Pad(int nSegments, int segmentLen, int offset, int bitMask)
            {
                this.nSegments = nSegments;
                this.segmentLen = segmentLen;
                this.offset = offset;
                this.bitMask = bitMask;
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

