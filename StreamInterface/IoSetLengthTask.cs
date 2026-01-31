using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Streams
{
    public class IoSetLengthTask : IoTask
    {
        public override byte[] Buffer => throw new WrongTaskTypeException("SetLengthTask does not return buffer.");
        public override EncodedValue Value => throw new WrongTaskTypeException("SetLengthTask does not return value.");
        
        private readonly long _len;
        
        internal IoSetLengthTask(IoTask previous, long length) : base(previous)
        {
            _len = length;
        }
        
        protected override void DoAction(Stream stream)
        {
            EndPosition = StartPosition;
            
            stream.SetLength(_len);
        }
    }
}