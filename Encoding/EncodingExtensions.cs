namespace MandalaLogics.Encoding
{
    public static class EncodingExtensions
    {
        public static EncodedObject Encode(this IEncodable obj)
        {
            return EncodedObject.Create(obj);
        }
        
        public static int GetEncodedSize(this IEncodable obj)
        {
            var eo = obj.Encode();

            if (!eo.IsFixedSize)
                throw new EncodingException("Cannot get size for an object with an unfixed size.");

            return (int)eo.WriteToMemoryStream().Length;
        }
    } 
}