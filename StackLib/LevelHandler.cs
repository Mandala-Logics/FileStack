using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MandalaLogics.Database;
using MandalaLogics.Encoding;
using MandalaLogics.Locking;

namespace MandalaLogics.Stacking
{
    public partial class FileStack
    {
        internal class LevelHandler : IReadOnlyCollection<LevelInfo>
        {
            private static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(500);
            
            private readonly FileStack _owner;
            private Splice<LevelInfo> Db => _owner._levelDb;

            private readonly Leaser<uint, LevelHandle> _openLevels = new Leaser<uint, LevelHandle>();

            public int Count => Db.Count;

            public LevelHandler(FileStack owner)
            {
                _owner = owner;
            }

            public uint CreateLevel(EncodedValue? metadata)
            {
                using var strand = _owner._data.CreateStrand();
                
                Db.Add(new LevelInfo(strand.Id, DateTime.Now) { Metadata = metadata });

                return strand.Id;
            }

            public bool IsValidId(uint levelId)
            {
                return Db.Any(li => levelId.Equals(li.LevelId));
            }

            public LevelHandle GetLevel(uint levelId)
            {
                if (!IsValidId(levelId)) throw new ArgumentException("Level ID provided is not valid.");

                if (_openLevels.TryGet(levelId, out var handle))
                {
                    return handle;
                }
                else
                {
                    var li = Db.GetHandle(li => levelId.Equals(li.LevelId));

                    var level = new LevelHandle(_owner, levelId, li);

                    _openLevels.TryAdd(levelId, handle);

                    return level;
                }
            }

            public Lease<LevelHandle> GetLevelHandleLease(uint levelId)
            {
                if (!IsValidId(levelId)) throw new ArgumentException("Level ID provided is not valid.");
                
                if (_openLevels.TryTakeLease(levelId, out var handle))
                {
                    return handle;
                }
                else
                {
                    var li = Db.GetHandle(li => levelId.Equals(li.LevelId));

                    var level = new LevelHandle(_owner, levelId, li);

                    return _openLevels.AddAndTakeLease(levelId, level);
                }
            }

            public void DeleteLevel(uint levelId)
            {
                if (!IsValidId(levelId)) throw new ArgumentException("Level ID provided is not valid.");
                
                using var handle = GetLevel(levelId);

                foreach (var sfi in handle)
                {
                    _owner._bulkHandler.RemoveReference(sfi.BulkId);
                }

                using (var li = Db.GetHandle(li => levelId.Equals(li.LevelId)))
                {
                    li.DeleteEntry();
                }
                
                _owner._data.DestroyStrand(handle.LevelId);
            }

            public IEnumerator<LevelInfo> GetEnumerator() => Db.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            
        }
    }
}