using System;
using System.IO;
using MandalaLogics.Encoding;
using MandalaLogics.Streams;

namespace MandalaLogics.Packing
{
    public class KnotHeader : IEncodable, IEquatable<KnotHeader>
    {
        public static readonly int EncodedSize = (int)new KnotHeader(Stitch.Null).Encode().WriteToMemoryStream().Length;
        
        public Stitch Stitch { get; }
        public bool Disused { get; internal set; }
        public int UsedBytes { get; internal set; }
        public uint StrandId { get; internal set; }
        public int Ordinal { get; internal set; }

        internal KnotHeader(Stitch stitch)
        {
            Stitch = stitch;
        }

        public KnotHeader(DecodingHandle handle)
        {
            Stitch = handle.Next<Stitch>();
            Disused = handle.Next<bool>();
            UsedBytes = handle.Next<int>();
            StrandId = handle.Next<uint>();
            Ordinal = handle.Next<int>();
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(Stitch);
            handle.Append(Disused);
            handle.Append(UsedBytes);
            handle.Append(StrandId);
            handle.Append(Ordinal);
        }

        internal void WriteSelf(StreamHandle handle)
        {
            handle.Seek(Stitch.Shove(-EncodedSize).Start, SeekOrigin.Begin);

            handle.Write(this.Encode());
        }
        
        internal void WriteSelf(Stream stream)
        {
            stream.Seek(Stitch.Shove(-EncodedSize).Start, SeekOrigin.Begin);

            this.Encode().Write(stream);
        }

        public bool Equals(KnotHeader? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Stitch.Equals(other.Stitch);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((KnotHeader)obj);
        }

        public override int GetHashCode()
        {
            return Stitch.GetHashCode();
        }
    }
}