namespace MandalaLogics.Encoding
{
    public interface IEncodable
    {
        public void DoEncode(EncodingHandle handle);
    }

    public static class EncodingExtensions
    {
        public static EncodedObject Encode(this IEncodable obj)
        {
            return EncodedObject.Create(obj);
        }
    } 
}