using System;
using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Streams
{
    public sealed class IoDecodeTask : IoTask
    {
        public override byte[] Buffer => throw new WrongTaskTypeException("Decode task does not return a buffer.");
        public override EncodedValue Value
        {
            get
            {
                if (_val is null)
                {
                    try
                    {
                        Wait(TimeSpan.MaxValue);
                    }
                    catch (AggregateException e)
                    {
                        throw e.InnerException;
                    }
                }
                
                return _val;
            }
        }

        private EncodedValue? _val = null;

        internal IoDecodeTask(IoTask previous) : base(previous) { }

        protected override void DoAction(Stream stream)
        {
            try
            {
                EncodedValue.Read(stream, out _val);
            }
            finally
            {
                EndPosition = new StreamPosition(stream.Position, SeekOrigin.Begin);
            }
        }
    }
}