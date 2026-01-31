using System;
using MandalaLogics.Encoding;

namespace MandalaLogics.Stacking
{
    internal sealed class BulkDataInfo : IEncodable, IEquatable<BulkDataInfo>
    {
        public uint BulkId { get; }
        public FileFingerprint Fingerprint { get; }
        public int ReferenceCount { get; private set; }

        internal BulkDataInfo(uint bulkId, FileFingerprint fingerprint)
        {
            BulkId = bulkId;
            Fingerprint = fingerprint;
            ReferenceCount = 1;
        }
        
        public BulkDataInfo(DecodingHandle handle)
        {
            BulkId = handle.Next<uint>();
            Fingerprint = handle.Next<FileFingerprint>();
            ReferenceCount = handle.Next<int>();
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(BulkId);
            handle.Append(Fingerprint);
            handle.Append(ReferenceCount);
        }

        public bool Equals(BulkDataInfo? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return BulkId == other.BulkId;
        }

        public void AddReference()
        {
            ReferenceCount++;
        }

        public void RemoveReference()
        {
            if (ReferenceCount == 0)
                throw new InvalidOperationException("Reference count cannot be decremented below zero.");

            ReferenceCount--;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || obj is BulkDataInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)BulkId;
        }
    }
}