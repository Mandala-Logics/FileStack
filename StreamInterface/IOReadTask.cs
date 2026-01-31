using System;
using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Streams
{
    public sealed class IoReadTask : IoTask
    {
        public override byte[] Buffer
        {
            get
            {
                if (_buffer is null)
                {
                    Wait(TimeSpan.MaxValue);
                }
                
                return _buffer;
            }
        }
        public override EncodedValue Value => throw new WrongTaskTypeException("Read task does not return a value.");

        public int BytesRequested {get;}

        private byte[]? _buffer;

        internal IoReadTask(IoTask previous, int count) : base(previous)
        {
            BytesRequested = count;
        }

        protected override void DoAction(Stream stream)
        {
            _buffer = new byte[BytesRequested];

            int r = stream.Read(_buffer);

            if (r == 0)
            {
                throw new EndOfStreamException();
            }

            _buffer = _buffer[.. r];

            EndPosition = new StreamPosition(stream.Position, SeekOrigin.Begin);
        }
    }
}