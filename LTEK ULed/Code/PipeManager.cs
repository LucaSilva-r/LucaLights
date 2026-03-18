using LTEK_ULed.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;

namespace LTEK_ULed.Code
{
    public static class PipeManager
    {
        private const int FULL_SEXTET_COUNT = 33;

        private static PipeThread? _pipeThread;
        private static Thread? thread;

        private static CancellationTokenSource run = new CancellationTokenSource();

        public static void Start(string pipeName = "StepMania-Lights-SextetStream")
        {
            run?.Cancel();
            _pipeThread?.CloseActiveStream();
            thread?.Join(2000);

            run = new CancellationTokenSource();
            _pipeThread = new PipeThread(run.Token, pipeName);

            thread = new Thread(new ThreadStart(_pipeThread.Run));
            thread.IsBackground = true;
            thread.Start();
        }

        public static void Stop()
        {
            run?.Cancel();
            _pipeThread?.CloseActiveStream();
        }

        public class PipeThread
        {

            byte[] buffer = new byte[FULL_SEXTET_COUNT];
            string pipename;
            CancellationToken token;
            private Stream? _activeStream;
            private readonly object _streamLock = new object();

            public PipeThread(CancellationToken token, string pipename = "StepMania-Lights-SextetStream")
            {
                this.token = token;
                if (pipename.StartsWith("~"))
                    pipename = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + pipename.Substring(1);
                this.pipename = pipename;
            }

            public void CloseActiveStream()
            {
                lock (_streamLock)
                {
                    try
                    {
                        _activeStream?.Dispose();
                    }
                    catch { }
                    _activeStream = null;
                }
            }

            private void SetActiveStream(Stream? stream)
            {
                lock (_streamLock)
                {
                    _activeStream = stream;
                }
            }

            public void Run()
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    RunWindows();
                }
                else
                {
                    RunLinux();
                }
            }

            private void RunWindows()
            {
                IAsyncResult result;
                NamedPipeServerStream pipe;

                while (!token.IsCancellationRequested)
                {
                    pipe = new NamedPipeServerStream(pipename, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 1000000, 100000);
                    SetActiveStream(pipe);

                    result = pipe.BeginWaitForConnection(null, this);

                    while (!pipe.IsConnected && !token.IsCancellationRequested)
                    {
                        Thread.Sleep(100);
                    }

                    if (token.IsCancellationRequested)
                    {
                        SetActiveStream(null);
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
                            SetActiveStream(null);
                            pipe.Dispose();
                            return;
                        }
                    }
                    Debug.WriteLine("Pipe Connected");

                    ReadStream(pipe);

                    SetActiveStream(null);
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

            private void RunLinux()
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Create FIFO if it doesn't exist
                        if (!File.Exists(pipename))
                        {
                            string? dir = Path.GetDirectoryName(pipename);
                            if (dir != null && !Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }
                            int ret = mkfifo(pipename, 0b110_110_100); // 0664
                            if (ret != 0)
                            {
                                Debug.WriteLine($"Failed to create FIFO at {pipename}");
                                Thread.Sleep(1000);
                                continue;
                            }
                            Debug.WriteLine($"Created FIFO at {pipename}");
                        }

                        Debug.WriteLine($"Waiting for writer on FIFO {pipename}...");

                        // Opening a FIFO for reading blocks until a writer opens the other end
                        var stream = new FileStream(pipename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        SetActiveStream(stream);

                        Debug.WriteLine("FIFO Connected");

                        ReadStream(stream);

                        SetActiveStream(null);
                        stream.Dispose();

                        if (!token.IsCancellationRequested)
                        {
                            Debug.WriteLine("FIFO writer disconnected, restarting");
                            lock (GameState.gameState)
                            {
                                GameState.gameState.SetConnectionStatus(false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Debug.WriteLine("FIFO thread cancelled, exiting");
                            return;
                        }
                        Debug.WriteLine($"FIFO error: {ex.Message}, retrying...");
                        Thread.Sleep(1000);
                    }
                }
            }

            private void ReadStream(Stream stream)
            {
                int counter = 0;
                int currentData;
                while (!token.IsCancellationRequested)
                {
                    if (!GameState.gameState.Connected)
                    {
                        lock (GameState.gameState)
                        {
                            GameState.gameState.SetConnectionStatus(true);
                        }
                    }
                    try
                    {
                        currentData = stream.ReadByte();
                    }
                    catch
                    {
                        // Stream was disposed (shutdown or restart)
                        break;
                    }
                    if (currentData == -1)
                    {
                        // End of stream (writer closed)
                        break;
                    }
                    if (currentData == (byte)'\n')
                    {
                        counter = buffer.Length;
                    }
                    else if (counter < buffer.Length)
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
            }

            [DllImport("libc", SetLastError = true)]
            private static extern int mkfifo(string pathname, uint mode);
        }
    }
}
