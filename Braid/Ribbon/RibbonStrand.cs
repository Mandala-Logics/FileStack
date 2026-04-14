using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MandalaLogics.Locking;

namespace MandalaLogics.Packing
{
    public sealed partial class Ribbon
    {
        public class RibbonStrand : Stream, ILeaseable<RibbonStrand>
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
            private readonly Ribbon _owner;

            private Seam _capacity;
            private Seam _inUse;
            private long _pos = 0L;
            private List<KnotHeader> _myKnots;

            private readonly SyncLock _lock = new SyncLock();

            internal readonly SemaphoreSlim StrandLock = new SemaphoreSlim(1);
        
            internal RibbonStrand(Ribbon owner, uint id)
            {
                _owner = owner;
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
                long len = 0L;

                for (int x = 0; x < _myKnots.Count - 1; x++)
                {
                    len += _myKnots[x].Stitch.Length;
                }

                len += _myKnots[^1].UsedBytes;

                return _capacity.Slice(0, (int)len);
            }
        
            public override void Flush()
            {
                _owner._stream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ThrowIfDisposed();
                
                if (count < 0 || count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
                else if (offset + count > buffer.Length) throw new ArgumentException("the sum of offset and count is beyond the end of the buffer.");
                else if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                else if (count == 0) return 0;
                
                using var l = _lock.Take();
                _owner.EnterWriteLock();

                try
                {
                    var tmp = _inUse.SliceToEnd((int)_pos);
                    
                    if (tmp.IsEmpty) return 0;

                    if (count < tmp.ByteLength) tmp = tmp.Slice(0, count);

                    var b = tmp.Read(_owner._stream);

                    Buffer.BlockCopy(b, 0, buffer, offset, b.Length);

                    _pos += b.Length;
                    
                    return b.Length;
                }
                finally
                {
                    _owner._stream.Flush();
                    
                    _owner._streamLock.ExitWriteLock();
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                ThrowIfDisposed();
                
                using var l = _lock.Take();

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
                ThrowIfDisposed();
                
                if (value < 0L) throw new ArgumentOutOfRangeException(nameof(value));

                if (value == Length) return;
                
                using var l = _lock.Take();
                _owner.EnterWriteLock();

                try
                {
                    if (value < _capacity.ByteLength)
                    {
                        var tmp = _capacity.Slice(0, (int)value);

                        if (_capacity.Count == tmp.Count) //we have the right amount of knots
                        {
                            _inUse = tmp;
                                
                            _myKnots[^1].UsedBytes = _inUse[^1].Length;
                                
                            _myKnots[^1].WriteSelf(_owner._stream);
                                
                            return;
                        }
                            
                        for (var x = tmp.Count; x < _capacity.Count; x++)
                        {
                            var knot = _myKnots[x];

                            knot.Disused = true;
                            knot.StrandId = 0U;
                            knot.Ordinal = 0;

                            knot.WriteSelf(_owner._stream);
                        }

                        _myKnots = _myKnots.Take(tmp.Count).ToList();
                            
                        _capacity = Seam.Build(_myKnots.Select(knot => knot.Stitch));
                            
                        _inUse = _capacity.Slice(0, (int)value);
                            
                        _myKnots[^1].UsedBytes = _inUse[^1].Length;

                        _myKnots[^1].WriteSelf(_owner._stream);
                    }
                    else
                    {
                        var len = _capacity.ByteLength;

                        do
                        {
                            var knot = _owner.GetFreeKnot((int)(value - len));

                            len += knot.Stitch.Length;

                            knot.Disused = false;
                            knot.Ordinal = _myKnots.Count;
                            knot.StrandId = Id;
                                
                            knot.WriteSelf(_owner._stream);
                                
                            _myKnots.Add(knot);

                        } while (len < value);

                        _capacity = Seam.Build(_myKnots.Select(knot => knot.Stitch));

                        _inUse = _capacity.Slice(0, (int)value);

                        _myKnots[^1].UsedBytes = _inUse[^1].Length;
                            
                        _myKnots[^1].WriteSelf(_owner._stream);
                    }
                }
                finally
                {
                    _owner._stream.Flush();
                    
                    _owner._streamLock.ExitWriteLock();
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                ThrowIfDisposed();
                
                if (count < 0 || count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
                else if (offset + count > buffer.Length) throw new ArgumentException("the sum of offset and count is beyond the end of the buffer.");
                else if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

                if (count == 0) return;

                using var l = _lock.Take();
                
                _owner.EnterWriteLock();

                try
                {
                    if (Length < count + _pos) SetLength(count + _pos);

                    var tmp = _inUse.Slice((int)_pos, count);

                    var w = tmp.Write(buffer, offset, count, _owner._stream);

                    if (w < count) throw new PlaceholderException();

                    _pos += w;
                }
                finally
                {
                    _owner._stream.Flush();

                    _owner._streamLock.ExitWriteLock();
                }
            }

            private void ThrowIfDisposed()
            {
                if (_owner.Disposed || !_owner._openStrands.ContainsKey(Id))
                    throw new ObjectDisposedException(nameof(RibbonStrand));
            }

            public Lease<RibbonStrand> GetLease()
            {
                return new Lease<RibbonStrand>(this, _lock.Take());
            }
        }
    }
}