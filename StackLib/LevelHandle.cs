using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MandalaLogics.Encoding;
using MandalaLogics.Packing;
using MandalaLogics.Splice;

namespace MandalaLogics.Stacking
{
    public partial class FileStack
    {
        public class LevelHandle : IReadOnlyList<FileStackEntry>, IDisposable
        {
            private readonly FileStack _owner;
            private readonly Splice<StackedFileInfo> _db;
            
            public uint LevelId { get; }
            public FileStackEntry this[int index] => new FileStackEntry(_db[index], this);
            public int Count => _db.Count;
            public bool Disposed => _db.Disposed || _owner.Disposed;
            public bool HasMetadata => info.Value.HasMetadata;
            public EncodedValue? Metadata => info.Value.Metadata;

            private Splice<LevelInfo>.SpliceHandle info;
            private ReaderWriterLockSlim _enumLock = new ReaderWriterLockSlim();
            
            internal LevelHandle(FileStack owner, uint levelId, Splice<LevelInfo>.SpliceHandle li)
            {
                _owner = owner;

                LevelId = levelId;

                _db = new Splice<StackedFileInfo>(owner._data.GetStrand(levelId));

                info = li;
            }

            public EncodedValue? GetMetadata()
            {
                return info.Value.Metadata;
            }

            public void SetMetadata(EncodedValue? metadata)
            {
                info.Value.Metadata = metadata;
            }

            public uint Add(string fileId, Stream stream)
            {
                if (Disposed) throw new ObjectDisposedException(nameof(LevelHandle));
                
                var bulkId = _owner._bulkHandler.GetOrCreate(stream);
                
                _db.Add(new StackedFileInfo(bulkId, LevelId, fileId));

                info.Value.FileCount++;
                //info.Flush();

                return bulkId;
            }

            public uint? TryAdd(string fileId, FileFingerprint fingerprint)
            {
                if (Disposed) throw new ObjectDisposedException(nameof(LevelHandle));
                
                if (_owner._bulkHandler.Get(fingerprint) is { } handle)
                {
                    _db.Add(new StackedFileInfo(handle.Value.BulkId, LevelId, fileId));
                    
                    handle.Value.AddReference();
                    handle.Dispose();
                    
                    info.Value.FileCount++;
                    //info.Flush();

                    return handle.Value.BulkId;
                }
                else
                {
                    return null;
                }
            }

            internal void RecoverFile(uint bulkId, Stream output)
            {
                if (Disposed) throw new ObjectDisposedException(nameof(LevelHandle));
                
                using var strand = _owner._data.GetStrand(bulkId);
                
                strand.CopyTo(output);
                
                output.Flush();
            }

            public void Dispose()
            {
                _db.Dispose();
                info.Dispose();
                
                _owner._levelHandler.LevelHandleClosed(LevelId);
            }

            public IEnumerator<FileStackEntry> GetEnumerator()
            {
                return new LevelHandleEnumerator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            internal sealed class LevelHandleEnumerator : IEnumerator<FileStackEntry>
            {
                public FileStackEntry Current { get; private set; } = null!;
                object? IEnumerator.Current => Current;

                private readonly LevelHandle _owner;
                private IEnumerator<StackedFileInfo> _baseEnum = null!;
                
                public LevelHandleEnumerator(LevelHandle owner)
                {
                    _owner = owner;
                    Reset();
                }
                
                public bool MoveNext()
                {
                    if (_baseEnum.MoveNext())
                    {
                        Current = new FileStackEntry(_baseEnum.Current, _owner);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                public void Reset()
                {
                    _baseEnum?.Dispose();
                    _baseEnum = _owner._db.GetEnumerator();
                }
                
                public void Dispose()
                {
                    _baseEnum?.Dispose();
                }
            }
        }
    }
}