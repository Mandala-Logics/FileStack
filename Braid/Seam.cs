using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MandalaLogics.Encoding;
using MandalaLogics.Streams;

namespace MandalaLogics.Packing
{
    // <summary>
    /// A logical byte sequence composed of one or more non-overlapping physical extents (<see cref="Stitch"/>).
    /// </summary>
    /// <remarks>
    /// A <see cref="Seam"/> is a view that treats multiple disjoint physical ranges as a single contiguous
    /// logical sequence, in the order they appear in <see cref="IReadOnlyList{T}"/>.
    /// <para/>
    /// Slicing operations (e.g. <see cref="Slice(int,int)"/>) are expressed purely in terms of segment slicing,
    /// avoiding manual offset arithmetic across extents.
    /// <para/>
    /// Invariants:
    /// <list type="bullet">
    /// <item><description>Stitches do not overlap (collision checks enforce this).</description></item>
    /// <item><description><see cref="ByteLength"/> equals the sum of stitch lengths.</description></item>
    /// </list>
    /// </remarks>
    public readonly struct Seam : IReadOnlyList<Stitch>
    {
        public long Start => _stitches[0].Start;
        public long End => _stitches[^1].End;
        private readonly Stitch[] _stitches;
        public bool IsEmpty => ByteLength == 0;
        public int ByteLength { get; }
        public int Count => _stitches.Length;
        public Stitch this[int index] => _stitches[index];

        private Seam(Stitch[] stitches)
        {
            _stitches = stitches;

            ByteLength = stitches.Sum(s => s.Length);
        }

        public Seam(Stitch stitch)
        {
            _stitches = new[] { stitch };
            ByteLength = stitch.Length;
        }

        private Seam(Stitch[] stitches, int byteLength)
        {
            _stitches = stitches;
            ByteLength = byteLength;
        }

        public readonly Seam Append(Stitch stitch)
        {
            if (_stitches is { })
            {
                foreach (var s in _stitches)
                {
                    if (s.CollidesWith(stitch)) throw new StitchException(StitchExceptionReason.Collision);
                }

                var tmp = new Stitch[_stitches.Length + 1];
            
                Array.Copy(_stitches, 0, tmp, 0, _stitches.Length);
                tmp[^1] = stitch;

                return new Seam(tmp, ByteLength + stitch.Length);
            }
            else
            {
                return new Seam(new Stitch[] { stitch }, stitch.Length);
            }
        }

        public readonly Seam Append(IEnumerable<Stitch> stitches)
        {
            if (_stitches is { })
            {
                var arr = stitches.Concat(_stitches).ToArray();

                int a, b;

                for (a = 0; a < arr.Length; a++)
                {
                    for (b = a + 1; b < arr.Length; b++)
                    {
                        if (arr[a].CollidesWith(arr[b])) throw new StitchException(StitchExceptionReason.Collision);
                    }
                }

                return new Seam(arr);
            }
            else
            {
                return new Seam(stitches.ToArray());
            }
        }

        public List<IoTask> Read(StreamHandle handle)
        {
            var ret = new List<IoTask>(_stitches.Length);
            
            foreach (var stitch in _stitches)
            {
                handle.Seek(stitch.Start, SeekOrigin.Begin);

                ret.Add(handle.Read(stitch.Length));
            }

            return ret;
        }

        public int Write(byte[] buffer, int offset, int count, Stream stream)
        {
            var n = 0;
            
            foreach (var stitch in _stitches)
            {
                n += stitch.Write(buffer, offset + n, count - n, stream);
            }

            return n;
        }

        public byte[] Read(Stream stream)
        {
            var buffer = new byte[ByteLength];
            var n = 0;
            
            foreach (var stitch in _stitches)
            {
                stream.Seek(stitch.Start, SeekOrigin.Begin);

                var r = stream.Read(buffer, n, stitch.Length);

                n += r;

                if (r < stitch.Length)
                {
                    return buffer[.. n];
                }
            }

            return buffer;
        }

        public int Write(byte[] buffer, int offset, int count, StreamHandle handle)
        {
            var n = 0;
            
            foreach (var stitch in _stitches)
            {
                n += stitch.Write(buffer, offset + n, count - n, handle);
            }

            return n;
        }

        public readonly Seam SliceToEnd(int offset)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            
            if (offset > ByteLength) throw new StitchException(StitchExceptionReason.OffsetOutOfBounds);

            return Slice(offset, ByteLength - offset);
        }

        public readonly Seam Slice(int offset, int count)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            
            if (offset > 0) //need to trim the start
            {
                if (offset > ByteLength) throw new StitchException(StitchExceptionReason.OffsetOutOfBounds);

                if (offset == ByteLength) return new Seam(new Stitch(End, 0));

                if (offset + count > ByteLength) throw new StitchException(StitchExceptionReason.CountTooLong);

                Stitch first = default;
                int x;
                var n = _stitches.Length;

                for (x = 0; x < _stitches.Length; x++)
                {
                    var stitch = _stitches[x];

                    if (offset < stitch.Length)
                    {
                        first = stitch.TakeEnd(stitch.Length - offset);
                        break;
                    }
                    else
                    {
                        offset -= stitch.Length;
                        n--;
                    }
                }

                var y = x;

                var tmp = new Stitch[n];

                tmp[0] = first;

                for (x = 1; x < tmp.Length; x++)
                {
                    tmp[x] = _stitches[++y];
                }

                return new Seam(tmp).Slice(0, count);
            }
            else
            {
                if (count > ByteLength) throw new StitchException(StitchExceptionReason.CountTooLong);

                if (count == ByteLength) return this;

                if (count == 0) return new Seam(new Stitch(_stitches[0].Start, 0));
                
                int x;
                var tmp = new List<Stitch>();

                for (x = 0; x < _stitches.Length; x++)
                {
                    var stitch = _stitches[x];

                    if (count <= stitch.Length)
                    {
                        tmp.Add(stitch.Slice(0, count));
                        break;
                    }
                    else
                    {
                        count -= stitch.Length;
                        tmp.Add(stitch);
                    }
                }

                return new Seam(tmp.ToArray());
            }
        }

        public static Seam Build(IEnumerable<Stitch> stitches)
        {
            var arr = stitches.ToArray();

            int a, b;

            for (a = 0; a < arr.Length; a++)
            {
                for (b = a + 1; b < arr.Length; b++)
                {
                    if (arr[a].CollidesWith(arr[b])) throw new StitchException(StitchExceptionReason.Collision);
                }
            }

            return new Seam(arr);
        }

        public IEnumerator<Stitch> GetEnumerator() => ((IEnumerable<Stitch>)_stitches).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        
    }
}