using System;
using MandalaLogics.Encoding;

namespace MandalaLogics.Stacking
{
    internal sealed class StackedFileInfo : IEncodable, IEquatable<StackedFileInfo>
    {
        public uint BulkId { get; }
        public uint LevelId { get; }
        public string FileId { get; }

        public StackedFileInfo(uint bulkId, uint levelId, string fileId)
        {
            BulkId = bulkId;
            LevelId = levelId;
            FileId = fileId;
        }

        public StackedFileInfo(DecodingHandle handle)
        {
            BulkId = handle.Next<uint>();
            LevelId = handle.Next<uint>();
            FileId = handle.Next<string>();
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(BulkId);
            handle.Append(LevelId);
            handle.Append(FileId);
        }

        public bool Equals(StackedFileInfo? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return BulkId == other.BulkId && LevelId == other.LevelId;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || obj is StackedFileInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BulkId, LevelId);
        }
    }
}