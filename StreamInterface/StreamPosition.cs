using System.IO;

namespace MandalaLogics.Streams
{
    public readonly struct StreamPosition
    {
        public SeekOrigin Origin {get;}
        public long Offset {get;}

        public StreamPosition(long offset, SeekOrigin origin)
        {
            Origin = origin;
            Offset = offset;
        }
    }
}