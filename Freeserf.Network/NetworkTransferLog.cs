using System;
using System.Diagnostics;
using System.Threading;

namespace Freeserf.Network
{
    // Diagnostic only: never changes deadlines, packet sizes or stream operations.
    // No packet contents or rejoin tokens are logged.
    internal sealed class NetworkTransferLog : IDisposable
    {
#if DEBUG
        static long nextId;
        readonly long id = Interlocked.Increment(ref nextId);
        readonly string context;
        readonly Stopwatch elapsed = Stopwatch.StartNew();
        readonly Timer timer;
        readonly object sync = new();
        string phase = "start";
        long expected;
        long completed;
        long lastProgressMs;
        bool disposed;
#endif
        internal NetworkTransferLog(string context)
        {
#if DEBUG
            this.context = context;
            Write("begin");
            timer = new Timer(_ =>
            {
                lock (sync)
                {
                    if (!disposed)
                        Write("pending");
                }
            }, null, 1000, 1000);
#endif
        }

        internal void Phase(string name, long length)
        {
#if DEBUG
            lock (sync)
            {
                phase = name;
                expected = length;
                completed = 0;
                lastProgressMs = elapsed.ElapsedMilliseconds;
                Write("phase");
            }
#endif
        }

        internal void Progress(int count)
        {
#if DEBUG
            lock (sync)
            {
                completed += count;
                lastProgressMs = elapsed.ElapsedMilliseconds;
            }
#endif
        }

        internal void Failed(Exception error)
        {
#if DEBUG
            lock (sync)
                Write($"failed exception={error.GetType().Name} message={error.Message}");
#endif
        }

#if DEBUG
        void Write(string status)
        {
            Log.Verbose.Write(ErrorSystemType.Network,
                $"TCP utc={DateTime.UtcNow:O} transfer={id} {context} status={status} phase={phase} completedBytes={completed}/{expected} elapsedMs={elapsed.ElapsedMilliseconds} sinceCompletionMs={elapsed.ElapsedMilliseconds - lastProgressMs}");
        }
#endif

        public void Dispose()
        {
#if DEBUG
            lock (sync)
            {
                disposed = true;
                Write("end");
            }
            timer.Dispose();
#endif
        }
    }
}
