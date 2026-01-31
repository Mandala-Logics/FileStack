using System;
using MandalaLogics.Encoding;

namespace MandalaLogics.Stacking
{
    public class LevelInfo : IEncodable
    {
        public uint LevelId { get; }
        public DateTime Timestamp { get; }
        public int FileCount { get; internal set; } = 0;
        public EncodedValue? Metadata { get; internal set; }
        public bool HasMetadata => Metadata is { };

        public LevelInfo(uint id, DateTime timestamp)
        {
            LevelId = id;
            Timestamp = timestamp;
        }
        
        public LevelInfo(DecodingHandle handle)
        {
            LevelId = handle.Next<uint>();
            Timestamp = handle.Next<DateTime>();
            FileCount = handle.Next<int>();
            
            var hasMetadata = handle.Next<bool>();

            if (hasMetadata) Metadata = handle.Next();
        }
        
        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(LevelId);
            handle.Append(Timestamp);
            handle.Append(FileCount);
            handle.Append(HasMetadata);
            
            if (HasMetadata) handle.Append(Metadata ?? throw new PlaceholderException());
        }
    }
}