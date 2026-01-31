using MandalaLogics.Encoding;

namespace MandalaLogics.Stacking
{
    public class StackHeader : IEncodable
    {
        public int Version { get; }
        public EncodedValue? Metadata { get; internal set; }
        public bool HasMetadata => Metadata is { };

        public StackHeader(int version)
        {
            Version = version;
        }

        public StackHeader(DecodingHandle handle)
        {
            Version = handle.Next<int>();

            var hasMetadata = handle.Next<bool>();

            if (hasMetadata) Metadata = handle.Next();
        }

        void IEncodable.DoEncode(EncodingHandle handle)
        {
            handle.Append(Version);
            handle.Append(HasMetadata);
            
            if (HasMetadata) handle.Append(Metadata ?? throw new PlaceholderException());
        }
    }
}