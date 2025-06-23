using LTEK_ULed.ViewModels;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;

namespace LTEK_ULed.Code
{
    public static class PipeManager
    {
        private const int FULL_SEXTET_COUNT = 33;

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

            byte[] buffer = new byte[FULL_SEXTET_COUNT];
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

                while (true)
                {
                    pipe = new NamedPipeServerStream(pipename, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 1000000, 100000);

                    result = pipe.BeginWaitForConnection(null, this);

                    while (!pipe.IsConnected && !token.IsCancellationRequested)
                    {
                        Thread.Sleep(100);
                    }

                    if (token.IsCancellationRequested)
                    {
                        pipe.Dispose();
                        return;
                    }
                    try
                    {
                        pipe.EndWaitForConnection(result);
                    }
                    catch
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
                        if (!GameState.gameState.Connected)
                        {
                            lock (GameState.gameState)
                            {
                                GameState.gameState.SetConnectionStatus(true);
                            }
                        }
                        currentData = pipe.ReadByte();
                        if (currentData == (byte)'\n')
                        {
                            counter = buffer.Length;
                        }
                        else if (currentData != -1 && counter < buffer.Length)
                        {
                            buffer[counter] = (byte)currentData;
                            counter++;
                        }
                        if (counter == buffer.Length)
                        {
                            if (!MainViewModel.Instance!.debug)
                            {
                                GameState.gameState.Parse(buffer);

                            }
                            counter = 0;
                        }
                    }
                    if (!token.IsCancellationRequested)
                    {
                        Debug.WriteLine("Pipe was closed/stepmania terminated, restarting");
                        lock (GameState.gameState)
                        {
                            GameState.gameState.SetConnectionStatus(false);
                        }
                        pipe.Dispose();

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
}
