using System;
using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Streams
{
    public sealed class IoSeekTask : IoTask
    {
        public static readonly IoSeekTask InitialTask = new IoSeekTask();

        public override byte[] Buffer => throw new WrongTaskTypeException("Seek task does not return a buffer.");
        public override EncodedValue Value => throw new WrongTaskTypeException("Seek task does not return a value.");

        internal IoSeekTask(IoTask? previous, long offset, SeekOrigin origin) : base(previous)
        {
            if (origin == SeekOrigin.Begin && offset < 0) { throw new ArgumentOutOfRangeException("Cannot seek beyond the start of the file."); }

            StartPosition = new StreamPosition(offset, origin);
        }

        private IoSeekTask() : base(null)
        {
            EndPosition = StartPosition = new StreamPosition(0L, SeekOrigin.Begin);
        }

        protected override void DoAction(Stream stream)
        {
            EndPosition = StartPosition;
        }
    }
}