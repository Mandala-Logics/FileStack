using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MandalaLogics.Database;
using MandalaLogics.Encoding;
using MandalaLogics.Splice;

namespace MandalaLogics.Stacking
{
    public partial class FileStack
    {
        internal class LevelHandler : IReadOnlyCollection<LevelInfo>
        {
            private static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(500);
            
            private readonly FileStack _owner;
            private Splice<LevelInfo> Db => _owner._levelDb;
            private readonly EntryTracker<uint> _levelTracker = new EntryTracker<uint>();

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

                if (!_levelTracker.EnterLock(levelId, WaitTime)) throw new InvalidOperationException("A handle for this level is open, cannot modify this level.");

                var li = Db.GetHandle(li => levelId.Equals(li.LevelId));

                return new LevelHandle(_owner, levelId, li);
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

            public void LevelHandleClosed(uint levelId)
            {
                _levelTracker.ExitLock(levelId);
            }

            public IEnumerator<LevelInfo> GetEnumerator() => Db.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            
        }
    }
}