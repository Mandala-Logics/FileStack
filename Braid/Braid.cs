using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using MandalaLogics.Encoding;
using MandalaLogics.Streams;

namespace MandalaLogics.Packing
{
    /// <summary>
    /// A container format built on a single backing stream that stores multiple logical streams ("strands")
    /// using reusable fixed-size (or bounded-size) extents ("knots").
    /// </summary>
    /// <remarks>
    /// <see cref="Braid"/> owns the backing stream and maintains an index of <see cref="KnotHeader"/> records.
    /// Each strand is mapped onto a sequence of knot extents and exposed as a <see cref="Stream"/> via <see cref="Strand"/>.
    /// </remarks>
    public sealed partial class Braid : IReadOnlyList<Braid.Strand>
    {
        public const int MinKnotSize = 4 * 1024;
        public const int MaxKnotSize = 32 * 1024;
        
        private readonly StreamInterface _streamInterface;
        private readonly BraidHeader _header;
        private readonly List<KnotHeader> _knots;
        private readonly List<uint> _strands = new List<uint>();

        private readonly ReaderWriterLockSlim _knotLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public bool Disposed => _streamInterface.Disposed;
        public int Count => _strands.Count;

        public Strand this[int index]
        {
            get
            {
                if (Disposed) throw new ObjectDisposedException(nameof(Braid));
                
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));

                return GetStrand((uint)index);
            }
        }

        static Braid()
        {
            EncodedObject.RegisterAll(Assembly.GetAssembly(typeof(Stitch)));
        }
        
        public Braid(Stream stream)
        {
            stream.Seek(0L, SeekOrigin.Begin);

            _streamInterface = new StreamInterface(stream);

            using var handle = _streamInterface.GetHandle();

            var newBraid = false;

            try
            {
                if (handle.Decode().Value.Value is BraidHeader bh)
                {
                    _header = bh;
                }
                else
                {
                    throw new BraidNotValidException("Failed to decode header.");
                }
            }
            catch (EndOfStreamException)
            {
                if (stream.Length != 0) throw new BraidNotValidException("Failed to decode header.");
                
                newBraid = true;
            }
            catch (EncodingException e)
            {
                throw new BraidNotValidException("Failed to decode header.", e);
            }

            if (newBraid)
            {
                _header = new BraidHeader();
                _header.WriteSelf(handle);
            }

            _knots = new List<KnotHeader>(_header?.KnotCount ?? 0);

            try
            {
                do
                {
                    if (handle.Decode().Value.Value is KnotHeader kh)
                    {
                        _knots.Add(kh);

                        handle.Seek(kh.Stitch.Length, SeekOrigin.Current);
                    }
                    else
                    {
                        throw new BraidNotValidException();
                    }
                } while (true);
            }
            catch (EndOfStreamException) {}
            catch (EncodingException e)
            {
                throw new BraidNotValidException("Failed to decode knot header.", e);
            }

            foreach (var knot in _knots)
            {
                if (knot.StrandId != 0U && !_strands.Contains(knot.StrandId))
                {
                    _strands.Add(knot.StrandId);
                }
            }
        }

        public Strand GetStrand(uint id)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Braid));
            
            if (id == 0U) throw new ArgumentOutOfRangeException(nameof(id));
            
            _knotLock.EnterReadLock();

            try
            {
                return new Strand(this, id);
            }
            finally
            {
                _knotLock.ExitReadLock();
            }
        }

        public Strand CreateStrand(Stream stream)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Braid));
            
            _knotLock.EnterWriteLock();

            try
            {
                var buffer = new byte[MaxKnotSize];

                var r = stream.Read(buffer);

                if (r == 0) throw new EndOfStreamException("No bytes to read from stream.");

                using var handle = _streamInterface.GetHandle();
                
                var knot = CreateKnot(r);

                knot.Disused = false;
                knot.Ordinal = 0;
                knot.StrandId = ++_header.LastStrandId;
                knot.UsedBytes = r;
                
                _header.WriteSelf(handle);
                knot.WriteSelf(handle);

                knot.Stitch.Write(buffer, 0, r, handle);

                var strand = new Strand(this, knot.StrandId);

                strand.Position = r;
                
                stream.CopyTo(strand);

                return strand;
            }
            finally
            {
                _knotLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Creates a strand large enough to store an encoded object.
        /// </summary>
        public Strand CreateStrand(IEncodable obj)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Braid));
            
            _knotLock.EnterWriteLock();

            try
            {
                var ms = obj.Encode().WriteToMemoryStream();
            
                var knot = GetFreeKnot((int)ms.Length);
                
                using var handle = _streamInterface.GetHandle();

                var id = ++_header.LastStrandId;
                
                _header.WriteSelf(handle);

                knot.Disused = false;
                knot.Ordinal = 0;
                knot.StrandId = id;
                knot.UsedBytes = 0;
                
                knot.WriteSelf(handle);
                
                _strands.Add(id);

                var strand = new Strand(this, id);
                
                ms.CopyTo(strand);

                strand.Seek(0L, SeekOrigin.Begin);

                return strand;
            }
            finally
            {
                _knotLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Creates a strand with the minimum possible capacity.
        /// </summary>
        public Strand CreateStrand()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Braid));
            
            _knotLock.EnterWriteLock();

            try
            {
                var knot = GetFreeKnot(MinKnotSize);
                
                using var handle = _streamInterface.GetHandle();

                var id = ++_header.LastStrandId;
                
                _header.WriteSelf(handle);

                knot.Disused = false;
                knot.Ordinal = 0;
                knot.StrandId = id;
                knot.UsedBytes = 0;
                
                knot.WriteSelf(handle);
                
                _strands.Add(id);

                return new Strand(this, id);
            }
            finally
            {
                _knotLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Deallocates all strands, wiping the database.
        /// </summary>
        public void Clear()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Braid));
            
            _knotLock.EnterWriteLock();

            try
            {
                using var handle = _streamInterface.GetHandle();
                
                foreach (var knot in _knots)
                {
                    if (knot.Disused) continue;
                    
                    knot.Disused = true;
                    knot.StrandId = 0;
                    knot.Ordinal = 0;
                    knot.UsedBytes = 0;

                    knot.WriteSelf(handle);
                }
                
                _strands.Clear();
            }
            finally
            {
                _knotLock.ExitWriteLock();
            }
        }

        public void DestroyStrand(uint id)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(Braid));
            
            _knotLock.EnterWriteLock();

            try
            {
                if (!_strands.Contains(id)) throw new InvalidOperationException("Stand ID not found.");
                
                using var handle = _streamInterface.GetHandle();

                foreach (var knot in _knots)
                {
                    if (knot.Disused || knot.StrandId != id) continue;
                    
                    knot.Disused = true;
                    knot.StrandId = 0;
                    knot.Ordinal = 0;
                    knot.UsedBytes = 0;

                    knot.WriteSelf(handle);
                }

                _strands.Remove(id);
            }
            finally
            {
                _knotLock.ExitWriteLock();
            }
        }

        private KnotHeader GetFreeKnot(int desiredSize)
        {
            if (desiredSize < MinKnotSize) desiredSize = MinKnotSize;
            else if (desiredSize > MaxKnotSize) desiredSize = MaxKnotSize;
            
            _knotLock.EnterUpgradeableReadLock();

            try
            {
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
            finally
            {
                _knotLock.ExitUpgradeableReadLock();
            }
        }

        private KnotHeader CreateKnot(int knotSize)
        {
            if (knotSize < MinKnotSize) knotSize = MinKnotSize;
            else if (knotSize > MaxKnotSize) knotSize = MaxKnotSize;

            using var handle = _streamInterface.GetHandle();
            
            _knotLock.EnterWriteLock();

            try
            {
                var len = handle.GetStreamLength();

                var knot = new KnotHeader(new Stitch(len + KnotHeader.EncodedSize, knotSize))
                {
                    Disused = true,
                    StrandId = 0U,
                    Ordinal = 0,
                    UsedBytes = 0
                };
                
                _knots.Add(knot);
                
                _header.KnotCount++;
                
                _header.WriteSelf(handle);

                knot.WriteSelf(handle);

                handle.SetStreamLength(knot.Stitch.End);

                return knot;
            }
            finally
            {
                _knotLock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            _streamInterface.Dispose();
        }

        public IEnumerator<Strand> GetEnumerator()
        {
            return new BraidEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}