using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using MandalaLogics.Encoding;
using MandalaLogics.Locking;

namespace MandalaLogics.Packing
{
    public sealed partial class Ribbon : IReadOnlyCollection<uint>
    {
        private const int MinKnotSize = 4 * 1024;
        private const int MaxKnotSize = 32 * 1024;

        private static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(500);

        private readonly Stream _stream;
        private readonly RibbonHeader _header = null!;
        
        private readonly List<KnotHeader> _knots;
        private readonly HashSet<uint> _strands = new HashSet<uint>();

        private readonly ReaderWriterLockSlim _streamLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly ReaderWriterLockSlim _enumLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private readonly Leaser<uint, RibbonStrand> _openStrands = new Leaser<uint, RibbonStrand>();

        public int Count => _strands.Count;

        public bool Disposed { get; private set; } = false;
        
        static Ribbon()
        {
            EncodingRegister.RegisterAll(Assembly.GetAssembly(typeof(Stitch)));
        }

        public Ribbon(Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);

            _stream = stream;

            var newRibbon = false;

            try
            {
                EncodedValue.Read(_stream, out var ev);

                if (ev.Value is RibbonHeader rh)
                {
                    _header = rh;
                }
            }
            catch (EndOfStreamException)
            {
                if (stream.Length != 0) throw new RibbonNotValidException("Failed to decode header.");
                
                newRibbon = true;
            }
            catch (EncodingException e)
            {
                throw new RibbonNotValidException("Failed to decode header.", e);
            }

            if (newRibbon)
            {
                _header = new RibbonHeader();
                _header.WriteSelf(_stream);
            }
            
            _knots = new List<KnotHeader>(_header?.KnotCount ?? 0);
            
            ReadAllKnots();
            
            foreach (var knot in _knots)
            {
                if (knot.StrandId != 0U) _strands.Add(knot.StrandId);
            }
        }

        private void ReadAllKnots()
        {
            try
            {
                do
                {
                    EncodedValue.Read(_stream, out var ev);

                    if (ev.Value is KnotHeader kh)
                    {
                        _knots.Add(kh);

                        _stream.Seek(kh.Stitch.Length, SeekOrigin.Current);
                    }
                    
                } while (true);
            }
            catch (EndOfStreamException) {}
            catch (EncodingException e)
            {
                throw new RibbonNotValidException("Failed to decode knot header.", e);
            }
        }

        private void EnterWriteLock()
        {
            if (!_enumLock.TryEnterWriteLock(WaitTime))
            {
                throw new InvalidOperationException("Ribbon cannot be modified while it is being enumerated.");
            }
            
            _streamLock.EnterWriteLock();
        }

        public Lease<RibbonStrand> LeaseStrand(uint id)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Ribbon));
            
            if (id == 0U) throw new ArgumentOutOfRangeException(nameof(id));
            
            _streamLock.EnterReadLock();

            try
            {
                if (_openStrands.TryTakeLease(id, out var lease))
                {
                    lease.Value.Position = 0L;
                    
                    return lease;
                }
                else
                {
                    var s = new RibbonStrand(this, id);

                    return _openStrands.AddAndTakeLease(id, s);
                }
            }
            finally
            {
                _streamLock.ExitReadLock();
            }
        }
        
        public RibbonStrand GetStrand(uint id)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Ribbon));
            
            if (id == 0U) throw new ArgumentOutOfRangeException(nameof(id));
            
            _streamLock.EnterReadLock();

            try
            {
                if (_openStrands.TryGet(id, out var strand))
                {
                    strand.Position = 0L;
                    
                    return strand;
                }
                else
                {
                    var s = new RibbonStrand(this, id);

                    _openStrands.TryAdd(id, s);

                    return s;
                }
            }
            finally
            {
                _streamLock.ExitReadLock();
            }
        }

        public RibbonStrand CreateStrand(Stream stream)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Ribbon));
            
            EnterWriteLock();

            try
            {
                var knot = GetFreeKnot((int)stream.Length);

                var id = ++_header.LastStrandId;
                
                _header.WriteSelf(_stream);

                knot.Disused = false;
                knot.Ordinal = 0;
                knot.StrandId = id;
                knot.UsedBytes = 0;
                
                knot.WriteSelf(_stream);
                
                _strands.Add(id);

                var strand = new RibbonStrand(this, id);
                
                stream.CopyTo(strand);

                strand.Seek(0L, SeekOrigin.Begin);
                
                _openStrands.TryAdd(id, strand);

                strand.StrandLock.Wait();

                return strand;
            }
            finally
            {
                _stream.Flush();
                
                _enumLock.ExitWriteLock();
                _streamLock.ExitWriteLock();
            }
        }

        public RibbonStrand CreateStrand(IEncodable obj)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Ribbon));
            
            EnterWriteLock();

            try
            {
                var ms = obj.Encode().WriteToMemoryStream();
            
                var knot = GetFreeKnot((int)ms.Length);

                var id = ++_header.LastStrandId;
                
                _header.WriteSelf(_stream);

                knot.Disused = false;
                knot.Ordinal = 0;
                knot.StrandId = id;
                knot.UsedBytes = 0;
                
                knot.WriteSelf(_stream);
                
                _strands.Add(id);

                var strand = new RibbonStrand(this, id);
                
                ms.CopyTo(strand);

                strand.Seek(0L, SeekOrigin.Begin);
                
                _openStrands.TryAdd(id, strand);
                
                strand.StrandLock.Wait();

                return strand;
            }
            finally
            {
                _stream.Flush();
                
                _enumLock.ExitWriteLock();
                _streamLock.ExitWriteLock();
            }
        }

        public RibbonStrand CreateStrand()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Ribbon));
            
            EnterWriteLock();

            try
            {
                var knot = GetFreeKnot(MinKnotSize);

                var id = ++_header.LastStrandId;
                
                _header.WriteSelf(_stream);

                knot.Disused = false;
                knot.Ordinal = 0;
                knot.StrandId = id;
                knot.UsedBytes = 0;
                
                knot.WriteSelf(_stream);
                
                _strands.Add(id);

                var strand = new RibbonStrand(this, id);
                
                _openStrands.TryAdd(id, strand);
                
                strand.StrandLock.Wait();

                return strand;
            }
            finally
            {
                _stream.Flush();
                
                _enumLock.ExitWriteLock();
                _streamLock.ExitWriteLock();
            }
        }
        
        public void Clear()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Ribbon));
            
            EnterWriteLock();

            try
            {
                foreach (var knot in _knots)
                {
                    if (knot.Disused) continue;
                    
                    knot.Disused = true;
                    knot.StrandId = 0;
                    knot.Ordinal = 0;
                    knot.UsedBytes = 0;

                    knot.WriteSelf(_stream);
                }
                
                _strands.Clear();
                _openStrands.Clear();
            }
            finally
            {
                _stream.Flush();
                
                _enumLock.ExitWriteLock();
                _streamLock.ExitWriteLock();
            }
        }

        public void DestroyStrand(uint id)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Ribbon));
            
            if (!_strands.Contains(id)) throw new InvalidOperationException("Stand ID not found.");
            
            EnterWriteLock();

            try
            {
                foreach (var knot in _knots)
                {
                    if (knot.Disused || knot.StrandId != id) continue;
                    
                    knot.Disused = true;
                    knot.StrandId = 0;
                    knot.Ordinal = 0;
                    knot.UsedBytes = 0;

                    knot.WriteSelf(_stream);
                }

                _strands.Remove(id);
                _openStrands.Remove(id);
            }
            finally
            {
                _stream.Flush();
                
                _enumLock.ExitWriteLock();
                _streamLock.ExitWriteLock();
            }
        }

        private KnotHeader GetFreeKnot(int desiredSize)
        {
            if (desiredSize < MinKnotSize) desiredSize = MinKnotSize;
            else if (desiredSize > MaxKnotSize) desiredSize = MaxKnotSize;
            
            foreach (var knot in _knots)
            {
                if (!knot.Disused) continue;
                
                if ((knot.Stitch.Length > desiredSize && desiredSize > knot.Stitch.Length * 0.6) || desiredSize * 0.6 < knot.Stitch.Length)
                {
                    return knot;
                }
            }

            return CreateKnot(desiredSize);
        }

        private KnotHeader CreateKnot(int knotSize)
        {
            if (knotSize < MinKnotSize) knotSize = MinKnotSize;
            else if (knotSize > MaxKnotSize) knotSize = MaxKnotSize;
            
            var knot = new KnotHeader(new Stitch(_stream.Length + KnotHeader.EncodedSize, knotSize))
            {
                Disused = true,
                StrandId = 0U,
                Ordinal = 0,
                UsedBytes = 0
            };
            
            _knots.Add(knot);
                
            _header.KnotCount++;
            
            _header.WriteSelf(_stream);
            
            knot.WriteSelf(_stream);
            
            _stream.SetLength(knot.Stitch.End);

            return knot;
        }

        public void Dispose()
        {
            if (Disposed) return;
            
            Disposed = true;
            
            _stream.Flush();
            _stream.Dispose();
        }

        public IEnumerator<uint> GetEnumerator()
        {
            return new RibbonEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}