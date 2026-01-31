using System;
using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Streams
{
    public class IoGetLengthTask : IoTask
    {
        public override byte[] Buffer => throw new WrongTaskTypeException("GetLengthTask does not return a buffer");

        public override EncodedValue Value
        {
            get
            {
                if (_len is null)
                {
                    try
                    {
                        Task.Wait();
                    }
                    catch (AggregateException e)
                    {
                        throw e.InnerException;
                    }
                }

                return new EncodedPrimitive(_len);
            }
        }

        private long? _len;
        
        internal IoGetLengthTask(IoTask previous) : base(previous) {}
        
        protected override void DoAction(Stream stream)
        {
            EndPosition = StartPosition;
            _len = stream.Length;
        }
    }
}