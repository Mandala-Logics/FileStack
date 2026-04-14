namespace MandalaLogics.Locking
{
    public interface ILeaseable<T> where T : class
    {
        public Lease<T> GetLease();
    }
}