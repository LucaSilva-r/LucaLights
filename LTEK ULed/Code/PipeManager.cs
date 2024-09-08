using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LTEK_ULed.Code
{
    public static class PipeManager
    {
        public static readonly byte[] light_events = new byte[13];

        private static PipeThread? _pipeThread;
        private static Thread? thread;

        private static CancellationTokenSource run = new CancellationTokenSource();

        public static void Start()
        {
            run?.Cancel();
            thread?.Join();

            run = new CancellationTokenSource();
            _pipeThread = new PipeThread(run.Token);

            thread = new Thread(new ThreadStart(_pipeThread.Run));
            thread.Start();            
        }

        public static void Stop()
        {
            run?.Cancel();
        }

        public class PipeThread
        {
            byte[] buffer = new byte[13];
            string pipename;
            CancellationToken token;

            public PipeThread(CancellationToken token, string pipename = "StepMania-Lights-SextetStream")
            {
                this.token = token;
                this.pipename = pipename;
            }

            public void Run()
            {
                IAsyncResult result;
                NamedPipeServerStream pipe;

                pipe = new NamedPipeServerStream(pipename, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 1000000, 100000);

                result = pipe.BeginWaitForConnection(null, this);

                while (!pipe.IsConnected && !token.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }

                if (token.IsCancellationRequested) {
                    pipe.Dispose();
                    return;
                }
                try
                {
                    pipe.EndWaitForConnection(result);
                } catch
                {
                    if (token.IsCancellationRequested)
                    {
                        pipe.Dispose();
                        return;
                    }
                }
                Debug.WriteLine("Pipe Connected");

                int counter = 0;
                int currentData = -1;
                while (!token.IsCancellationRequested && pipe.IsConnected)
                {

                    currentData = pipe.ReadByte();
                    if (currentData == (byte)'\n')
                    {
                        counter = 0;
                    }
                    else if (currentData != -1 && counter < buffer.Length)
                    {
                        buffer[counter] = (byte)currentData;
                        counter++;
                    }
                    if (counter == buffer.Length)
                    {
                        for (int i = 0; i < light_events.Length; i++)
                        {
                            light_events[i] = buffer[i];
                        }
                    }
                }
                if (!token.IsCancellationRequested)
                {
                    Debug.WriteLine("Pipe was closed/stepmania terminated, restarting");
                    pipe.Dispose();
                    Run();
                }
                else
                {
                    pipe.Dispose();
                    Debug.WriteLine("Pipe has been disposed, exiting thread");
                    return;
                }
            }
        }
    }
}
