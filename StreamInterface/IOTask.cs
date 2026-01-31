using System;
using System.IO;
using System.Threading.Tasks;
using MandalaLogics.Encoding;

namespace MandalaLogics.Streams
{
    public abstract class IoTask
    {
        public StreamPosition StartPosition {get; protected set;}
        public StreamPosition? EndPosition {get; protected set;} = null;

        public abstract byte[] Buffer {get;}
        public abstract EncodedValue Value {get;}
        public Task<IoTask> Task => _taskCompletionSource.Task;

        public bool Awaited { get; private set; } = false;

        internal IoTask? Previous {get; private set;}

        private readonly TaskCompletionSource<IoTask> _taskCompletionSource = new TaskCompletionSource<IoTask>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal IoTask(IoTask? previous)
        {
            Previous = previous;
            StartPosition = new StreamPosition(0L, SeekOrigin.Current);
        }

        public bool Wait(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero) throw new ArgumentException("Timeout cannot be less than zero.");

            if (Awaited) return true;
            
            int ms;

            if (timeout.TotalMilliseconds > int.MaxValue) ms = int.MaxValue;
            else ms = (int)timeout.TotalMilliseconds;

            try
            {
                return Task.Wait(ms);
            }
            catch (AggregateException e)
            {
                throw e.InnerException;
            }
            finally
            {
                Awaited = true;
            }
        }

        public Task<IoTask> WaitAsync()
        {
            return Task;
        }

        internal void PerformAction(Stream stream)
        {
            try
            {
                switch (StartPosition.Origin)
                {
                    case SeekOrigin.Begin:

                        stream.Seek(StartPosition.Offset, SeekOrigin.Begin);

                        break;

                    case SeekOrigin.Current:

                        if (Previous?.EndPosition is { } sp)
                        {
                            stream.Seek(sp.Offset, sp.Origin);
                        }
                        else
                        {
                            throw new ProgrammerException(
                                "Need to set previous and previous end position for 'current' seek.");
                        }

                        break;

                    case SeekOrigin.End:

                        stream.Seek(StartPosition.Offset, SeekOrigin.End);

                        break;

                    default:
                        throw new ProgrammerException("Invalid origin.");
                }

                DoAction(stream);

                if (EndPosition is null)
                {
                    throw new ProgrammerException("forgot to set end position.");
                }

                _taskCompletionSource.SetResult(this);

                Previous = null;
            }
            catch (Exception e)
            {
                _taskCompletionSource.SetException(e);
            }
        }

        protected abstract void DoAction(Stream stream);
    }
}