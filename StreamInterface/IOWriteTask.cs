using System;
using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Streams
{
    public sealed class IoWriteTask : IoTask
    {
        private readonly MemoryStream _buffer;
        public override byte[] Buffer => throw new WrongTaskTypeException("Write task does not return a buffer.");
        public override EncodedValue Value => throw new WrongTaskTypeException("Write task does not return a value.");

        internal IoWriteTask(IoTask previous, byte[] buffer, int offset, int count) : base(previous)
        {
            if (offset < 0 || offset >= buffer.Length) { throw new ArgumentException("offset must be positive and less than the size of the buffer."); }
            else if (count < 0) { throw new ArgumentException("Count cannot be less than zero."); }
            else if (count + offset > buffer.Length) { throw new ArgumentException("The sum of count and offset are beyond the end of the buffer."); }

            _buffer = new MemoryStream(buffer, offset, count, false);
        }

        protected override void DoAction(Stream stream)
        {
            _buffer.CopyTo(stream);

            EndPosition = new StreamPosition(stream.Position, SeekOrigin.Begin);
        }
    }
}