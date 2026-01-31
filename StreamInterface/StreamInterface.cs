using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace MandalaLogics.Streams
{
    public sealed class StreamInterface
    {
        private static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(50);

        private readonly Stream _stream;

        public bool Disposed {get; private set;} = false;

        private volatile bool _disposing = false;
        private readonly Thread _loopThread;
        private readonly BlockingCollection<IoTask> _tasks = new BlockingCollection<IoTask>(new ConcurrentQueue<IoTask>());

        public StreamInterface(Stream stream)
        {
            try
            {
                _ = stream.Position;
                _ = stream.Length;
            }
            catch (ObjectDisposedException)
            {
                throw new ArgumentException("Stream is disposed.");
            }
            catch (NotSupportedException)
            {
                throw new ArgumentException("Stream must be finite and support providing position.");
            }

            if (!stream.CanRead || !stream.CanSeek || !stream.CanWrite) { throw new ArgumentException("Stream must support reading, writing and seeking."); }

            this._stream = stream;

            _loopThread = new Thread(Loop);
            _loopThread.Start();
        }

        public StreamHandle GetHandle()
        {
            return _disposing ? throw new ObjectDisposedException("StreamInterface") : new StreamHandle(this);
        }

        internal void Enqueue(IoTask task)
        {
            if (_disposing) { throw new ObjectDisposedException("FileInterface"); }

            try
            {
                _tasks.Add(task);
            }
            catch (InvalidOperationException)
            {
                throw new ObjectDisposedException("FileInterface");
            }
        }

        private void Loop()
        {
            while (!_disposing)
            {
                while (_tasks.TryTake(out var task, WaitTime))
                {
                    task.PerformAction(_stream);
                }
                
                //_stream.Flush();
            }

            while (_tasks.TryTake(out var t, WaitTime))
            {
                t.PerformAction(_stream);
            }

            _stream.Flush();
            _stream.Dispose();
            Disposed = true;
        }

        public void Dispose()
        {
            _disposing = true;

            _tasks.CompleteAdding();

            _loopThread.Join();
        }
    }
}
