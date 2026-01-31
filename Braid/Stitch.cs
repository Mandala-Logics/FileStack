using System;
using System.IO;
using MandalaLogics.Encoding;
using MandalaLogics.Streams;

namespace MandalaLogics.Packing
{
    /// <summary>
    /// Represents a contiguous physical byte range in the underlying storage stream.
    /// </summary>
    /// <remarks>
    /// A <see cref="Stitch"/> defines a half-open interval <c>[Start, End)</c>, where
    /// <see cref="Start"/> is the absolute byte offset in the backing stream and
    /// <see cref="Length"/> is the number of bytes in the range.
    /// <para/>
    /// Stitches are the atomic units of storage mapping. Higher-level structures
    /// (such as seams and strands) compose multiple stitches to form logical streams
    /// without requiring cross-range offset arithmetic.
    /// <para/>
    /// <b>Invariants</b>:
    /// <list type="bullet">
    /// <item><description><see cref="Start"/> is non-negative.</description></item>
    /// <item><description><see cref="Length"/> is non-negative.</description></item>
    /// <item><description><see cref="End"/> equals <c>Start + Length</c>.</description></item>
    /// <item><description>A stitch with <see cref="Length"/> == 0 represents an empty range.</description></item>
    /// </list>
    /// <para/>
    /// All slicing operations (<see cref="Slice(int,int)"/>, <see cref="Take(int)"/>,
    /// <see cref="TakeEnd(int)"/>) return new <see cref="Stitch"/> instances and never
    /// mutate the original, preserving referential safety.
    /// </remarks>
    public readonly struct Stitch : IEncodable, IEquatable<Stitch>
    {
        public static readonly Stitch Null = new Stitch(0L, 0);
        
        public long Start { get; }
        public int Length { get; }
        public long End => Start + Length;
        public bool IsEmpty => Length == 0;

        public Stitch(long start, int length)
        {
            if (start < 0L) throw new ArgumentOutOfRangeException(nameof(start));

            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            
            Start = start;
            Length = length;
        }

        public Stitch(DecodingHandle handle)
        {
            Start = handle.Next<long>();
            Length = handle.Next<int>();
        }

        public Stitch Slice(int offset, int count)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            if (offset > 0)
            {
                if (offset > Length) throw new StitchException(StitchExceptionReason.OffsetOutOfBounds);

                if (offset + count > Length) throw new StitchException(StitchExceptionReason.CountTooLong);
            }
            else
            {
                if (count > Length) throw new StitchException(StitchExceptionReason.CountTooLong);
            }

            return new Stitch(Start + offset, count);
        }

        public Stitch Take(int count) => Slice(0, count);

        public Stitch TakeEnd(int count)
        {
            if (count > Length) throw new StitchException(StitchExceptionReason.CountTooLong);

            if (count == Length) return this;
            
            var offset = Length - count;

            return Slice(offset, count);
        }

        public Stitch Shove(int amount)
        {
            return new Stitch(Start + amount, Length);
        }

        public int Write(byte[] buffer, int offset, int count, StreamHandle handle)
        {
            if (count < 0 || count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            else if (offset + count > buffer.Length) throw new ArgumentException("the sum of offset and count is beyond the end of the buffer.");
            else if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

            if (IsEmpty) return 0;
            
            handle.Seek(Start, SeekOrigin.Begin);

            var w = Math.Min(count, Length);

            handle.Write(buffer, offset, w);

            return w;
        }
        
        public int Write(byte[] buffer, int offset, int count, Stream stream)
        {
            if (count < 0 || count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            else if (offset + count > buffer.Length) throw new ArgumentException("the sum of offset and count is beyond the end of the buffer.");
            else if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

            if (IsEmpty) return 0;
            
            stream.Seek(Start, SeekOrigin.Begin);

            var w = Math.Min(count, Length);

            stream.Write(buffer, offset, w);

            return w;
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(Start);
            handle.Append(Length);
        }

        public bool CollidesWith(Stitch other)
        {
            if (other.Start < Start)
            {
                return other.End > Start;
            }
            else
            {
                return other.Start < End;
            }
        }

        public bool Equals(Stitch other)
        {
            return Start == other.Start && Length == other.Length;
        }

        public override bool Equals(object? obj)
        {
            return obj is Stitch other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Start, Length);
        }
    }
}