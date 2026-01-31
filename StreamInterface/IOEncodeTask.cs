using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Streams
{
    public sealed class IoEncodeTask : IoTask
    {
        public override byte[] Buffer => throw new WrongTaskTypeException("Encode task does not return a buffer.");
        public override EncodedValue Value => throw new WrongTaskTypeException("Encode task does not return a value.");

        private readonly EncodedValue _ev;

        internal IoEncodeTask(IoTask previous, EncodedValue ev) : base(previous)
        {
            this._ev = ev;
        }

        protected override void DoAction(Stream stream)
        {
            try
            {
                _ev.Write(stream);
            }
            finally
            {
                EndPosition = new StreamPosition(stream.Position, SeekOrigin.Begin);
            }
        }
    }
}