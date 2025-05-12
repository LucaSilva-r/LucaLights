
using Avalonia.Controls;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;

namespace LTEK_ULed.Code;

public static class SerialManager
{

    private static SerialPortThread? _serialPortThread;
    private static Thread? thread;

    private static CancellationTokenSource run;

    public static bool connected { get; private set; }

    public static void Connect(string port)
    {
        run?.Cancel();

        run = new CancellationTokenSource();
        //_serialPortThread = new SerialPortThread(port, 576000, run.Token, LightingManager.leds);

        if (thread != null)
        {
            while (thread.ThreadState != System.Threading.ThreadState.Stopped) ;
        }

        thread = new Thread(new ThreadStart(_serialPortThread.Run));
        thread.Start();
    }

    public static void Disconnect()
    {
        run?.Cancel();
    }

    private class SerialPortThread
    {
        byte[] data = new byte[6 + 144 * 3];
        string port;
        int baud;

        Color[] leds;
        CancellationToken token;

        public SerialPortThread(string port, int baud, CancellationToken token, Color[] leds)
        {
            this.baud = baud;
            this.port = port;
            this.leds = leds;
            this.token = token;
        }

        public void Run()
        {
            data[0] = (byte)'A';
            data[1] = (byte)'d';
            data[2] = (byte)'a';
            data[3] = (byte)(data.Length >> 8);
            data[4] = (byte)(data.Length & 0x00FF);
            data[5] = (byte)(data[3] ^ data[4] ^ 0x55);


            SerialPort mySerialPort = new SerialPort(port, baud);
            mySerialPort.DtrEnable = true;
            mySerialPort.RtsEnable = false;
            mySerialPort.ReadTimeout = 1000;
            mySerialPort.WriteBufferSize = data.Length;
            mySerialPort.Open();
            connected = true;

            string result = "";

            while (!token.IsCancellationRequested && !result.Contains("Ada"))
            {
                result = mySerialPort.ReadLine();
            }
            Debug.WriteLine("Pad connected");

            Stopwatch sw = new Stopwatch();

            while (!token.IsCancellationRequested)
            {
                for (int i = 0; i < leds.Length; i++)
                {
                    data[6 + i * 3] = leds[i].R;
                    data[6 + i * 3 + 1] = leds[i].G;
                    data[6 + i * 3 + 2] = leds[i].B;
                }
                mySerialPort.Write(data, 0, data.Length);

                sw.Start();
                while (sw.ElapsedMilliseconds < 5);
                sw.Reset();
            }
            sw.Reset();
            mySerialPort.Dispose();

            Debug.WriteLine("Serial thread terminated");
            connected = false;
        }
    }
}

