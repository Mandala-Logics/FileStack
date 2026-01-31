using System;
using System.IO;
using MandalaLogics.Splice;

namespace MandalaLogics.Stacking
{
    public partial class FileStack
    {
        internal class BulkHandler
        {
            private const long MinCompareLength = 1 * 1024 * 1024;

            private readonly FileStack _owner;
            private Splice<BulkDataInfo> Db => _owner._bulkDb;
            
            public BulkHandler(FileStack owner)
            {
                _owner = owner;
            }

            public void RemoveReference(uint bulkId)
            {
                using var handle = Db.GetHandle(b => b.BulkId.Equals(bulkId));
                
                handle.Value.RemoveReference();

                if (handle.Value.ReferenceCount == 0)
                {
                    _owner._data.DestroyStrand(handle.Value.BulkId);
                    handle.DeleteEntry();
                }
            }

            public Splice<BulkDataInfo>.SpliceHandle? Get(FileFingerprint fingerprint)
            {
                try
                {
                    var handle = Db.GetHandle(x => x.Fingerprint.Equals(fingerprint));

                    return handle;
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }

            public uint GetOrCreate(Stream stream)
            {
                if (stream.Length == 0) throw new InvalidOperationException("Stream cannot have a length of zero.");

                FileFingerprint fingerprint;

                if (stream.Length >= MinCompareLength)
                {
                    fingerprint = new FileFingerprint(stream);
                    
                    if (Get(fingerprint) is { } handle)
                    {
                        handle.Value.AddReference();
                    
                        handle.Dispose();
                    
                        return handle.Value.BulkId;
                    }
                }
                else
                {
                    fingerprint = new FileFingerprint(stream.Length);
                }
                
                using var strand = _owner._data.CreateStrand(stream);

                Db.Add(new BulkDataInfo(strand.Id, fingerprint));

                return strand.Id;
            }
        }
    }
}