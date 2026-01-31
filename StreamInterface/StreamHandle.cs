using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MandalaLogics.Encoding;

namespace MandalaLogics.Streams
{
    public sealed class StreamHandle : IDisposable
    {
        public StreamInterface Owner {get;}
        public int Count
        {
            get
            {
                lock (this)
                {
                    return _tasks.Count;
                }
            }
        }

        private IoTask _lastTask;
        private readonly Queue<IoTask> _tasks = new Queue<IoTask>();

        internal StreamHandle(StreamInterface owner)
        {
            Owner = owner;
            _lastTask = IoSeekTask.InitialTask;
        }

        private IoTask DeliverTask(IoTask task)
        {
            _lastTask = task;

            _tasks.Enqueue(task);
            Owner.Enqueue(task);

            return task;
        }

        public void WaitAll()
        {
            while (GetNext() is { } task)
            {
                task.Wait(TimeSpan.MaxValue);
            }
        }

        public async Task WaitAllAsync()
        {
            while (GetNext() is { } task)
            {
                await task.Task;
            }
        }

        public IoTask? GetNext()
        {
            IoTask task;

            lock (this)
            {
                if (_tasks.Count == 0) { return null; }

                task = _tasks.Dequeue();
            }

            return task;
        }

        public IoTask WaitNext()
        {
            if (WaitNext(TimeSpan.MaxValue) is { } task)
            {
                return task;
            }
            else
            {
                throw new PlaceholderException();
            }
        }

        public IoTask? WaitNext(TimeSpan timeout)
        {
            IoTask task;

            lock (this)
            {
                if (_tasks.Count == 0) { return null; }

                task = _tasks.Dequeue();
            }

            return task.Task.Wait(timeout) ? task : null;
        }

        public IoTask Seek(long offset, SeekOrigin origin)
        {
            lock (this)
            {
                return DeliverTask(new IoSeekTask(_lastTask, offset, origin));
            }
        }
        
        public IoTask Encode(EncodedValue ev)
        {
            lock (this)
            {
                return DeliverTask(new IoEncodeTask(_lastTask, ev));
            }
        }

        public IoTask Write(EncodedValue ev)
        {
            lock (this)
            {
                return DeliverTask(new IoEncodeTask(_lastTask, ev));
            }
        }

        public IoTask SetStreamLength(long length)
        {
            lock (this)
            {
                return DeliverTask(new IoSetLengthTask(_lastTask, length));
            }
        }
        
        public long GetStreamLength()
        {
            IoTask t;
            
            lock (this)
            {
                t = DeliverTask(new IoGetLengthTask(_lastTask));
            }
            
            return (long)t.Value.Value;
        }

        public IoTask Write(byte[] buffer, int offset, int count)
        {
            lock (this)
            {
                return DeliverTask(new IoWriteTask(_lastTask, buffer, offset, count));
            }
        }

        public IoTask Read(int bytes)
        {
            lock (this)
            {
                return DeliverTask(new IoReadTask(_lastTask, bytes));
            }
        }

        public IoTask Decode()
        {
            lock (this)
            {
                return DeliverTask(new IoDecodeTask(_lastTask));
            }
        }

        public void Dispose()
        {
            WaitAll();
        }
    }
}