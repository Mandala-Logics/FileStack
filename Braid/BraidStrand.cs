using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MandalaLogics.Locking;

namespace MandalaLogics.Packing
{
    /// <summary>
    /// A logical stream within a <see cref="Braid"/>.
    /// </summary>
    /// <remarks>
    /// A <see cref="BraidStrand"/> presents a normal seekable read/write <see cref="Stream"/> interface while its
    /// bytes are physically stored across one or more disjoint extents (knots) in the braid's backing stream.
    /// Internally it composes those extents into a <see cref="Seam"/> and uses seam slicing to implement
    /// <see cref="Read(byte[],int,int)"/> and <see cref="Write(byte[],int,int)"/> without manual cross-extent offset math.
    /// </remarks>
    public sealed partial class Braid
    {
        public class BraidStrand : Stream, ILeaseable<BraidStrand>
        {
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => true;
            public override long Length => _inUse.ByteLength;

            public override long Position 
            {
                get => _pos;
                set => Seek(value, SeekOrigin.Begin);
            }
            
            public uint Id { get; }
            
            public Braid Owner { get; }

            private List<KnotHeader> _myKnots;

            private readonly SyncLock _seamLock = new SyncLock();
            
            private Seam _capacity;
            private Seam _inUse;
            private long _pos = 0L;

            internal BraidStrand(Braid owner, uint id)
            {
                Owner = owner;
                Id = id;

                _myKnots = owner._knots.Where(k => !k.Disused && k.StrandId == id).ToList();

                if (_myKnots.Count == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(id));
                }
                
                _myKnots.Sort((k1, k2) => k1.Ordinal - k2.Ordinal);

                var stitches = new List<Stitch>(_myKnots.Count);

                foreach (var knot in _myKnots)
                {
                    stitches.Add(knot.Stitch);
                }

                _capacity = Seam.Build(stitches);

                _inUse = GetInUse();
            }

            private Seam GetInUse()
            {
                using var l = _seamLock.Take();
                
                long len = 0L;

                for (int x = 0; x < _myKnots.Count - 1; x++)
                {
                    len += _myKnots[x].Stitch.Length;
                }

                len += _myKnots[^1].UsedBytes;

                return _capacity.Slice(0, (int)len);
            }
            
            public Lease<BraidStrand> GetLease()
            {
                return new Lease<BraidStrand>(this, _seamLock.Take());
            }
        
            public override void Flush() {}

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (count < 0 || count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
                else if (offset + count > buffer.Length) throw new ArgumentException("the sum of offset and count is beyond the end of the buffer.");
                else if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                else if (count == 0) return 0;

                using var l = _seamLock.Take();
                
                using var handle = Owner._streamInterface.GetHandle();

                Seam tmp = _inUse.SliceToEnd((int)_pos);
                    
                if (tmp.IsEmpty) return 0;

                if (count < tmp.ByteLength) tmp = tmp.Slice(0, count);

                var ls = tmp.Read(handle);

                int n = 0;

                foreach (var task in ls)
                {
                    task.Wait(TimeSpan.MaxValue);
                        
                    Buffer.BlockCopy(task.Buffer, 0, buffer, offset + n, task.Buffer.Length);

                    n += task.Buffer.Length;
                }

                _pos += n;
                    
                return n;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                using var l = _seamLock.Take();
                
                long newPos;
                    
                switch (origin)
                {
                    case SeekOrigin.Begin:

                        newPos = offset;
                            
                        break;
                    case SeekOrigin.Current:

                        newPos = _pos + offset;
                            
                        break;
                    case SeekOrigin.End:

                        newPos = Length + offset;
                            
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
                }

                if (newPos < 0L) _pos = 0;
                else if (newPos > Length) _pos = Length;
                else _pos = newPos;

                return _pos;
            }

            public override void SetLength(long value)
            {
                using var l = _seamLock.Take();

                DoSetLength(value);
            }

            private void DoSetLength(long value)
            {
                if (value < 0L) throw new ArgumentOutOfRangeException(nameof(value));

                if (value == Length) return;
                
                using var handle = Owner._streamInterface.GetHandle();

                if (value < _capacity.ByteLength)
                {
                    var tmp = _capacity.Slice(0, (int)value);

                    if (_capacity.Count == tmp.Count) //we have the right amount of knots
                    {
                        _inUse = tmp;
                            
                        _myKnots[^1].UsedBytes = _inUse[^1].Length;
                            
                        _myKnots[^1].WriteSelf(handle);
                            
                        return;
                    }
                        
                    for (var x = tmp.Count; x < _capacity.Count; x++)
                    {
                        var knot = _myKnots[x];

                        knot.Disused = true;
                        knot.StrandId = 0U;
                        knot.Ordinal = 0;

                        knot.WriteSelf(handle);
                    }

                    _myKnots = _myKnots.Take(tmp.ByteLength).ToList();
                        
                    _capacity = Seam.Build(_myKnots.Select(knot => knot.Stitch));
                        
                    _inUse = _capacity.Slice(0, (int)value);
                        
                    _myKnots[^1].UsedBytes = _inUse[^1].Length;

                    _myKnots[^1].WriteSelf(handle);
                }
                else //need to add capacity
                {
                    var len = _capacity.ByteLength;

                    do
                    {
                        var knot = Owner.GetFreeKnot((int)(value - len));

                        len += knot.Stitch.Length;

                        knot.Disused = false;
                        knot.Ordinal = _myKnots.Count;
                        knot.StrandId = Id;
                            
                        knot.WriteSelf(handle);
                            
                        _myKnots.Add(knot);

                    } while (len < value);

                    _capacity = Seam.Build(_myKnots.Select(knot => knot.Stitch));

                    _inUse = _capacity.Slice(0, (int)value);

                    _myKnots[^1].UsedBytes = _inUse[^1].Length;
                        
                    _myKnots[^1].WriteSelf(handle);
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (count < 0 || count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
                else if (offset + count > buffer.Length) throw new ArgumentException("the sum of offset and count is beyond the end of the buffer.");
                else if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

                if (count == 0) return;
                
                using var l = _seamLock.Take();

                if (Length < count + _pos) DoSetLength(count + _pos);

                var tmp = _inUse.Slice((int)_pos, count);

                using var handle = Owner._streamInterface.GetHandle();

                var w = tmp.Write(buffer, offset, count, handle);

                if (w < count) throw new PlaceholderException();

                _pos += w;
            }
        }
    }
}