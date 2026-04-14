using System;
using System.Threading;

namespace MandalaLogics.Locking
{
    public sealed class Lease<T> : IDisposable where T : class
    {
        public T Value => _val ?? throw new ObjectDisposedException(nameof(Lease<T>));

        private readonly IDisposable _token;
        private int _disposed;
        private T? _val;

        public Lease(T obj, IDisposable token)
        {
            _val = obj ?? throw new ArgumentNullException();

            _token = token;
            
            _disposed = 0;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            
            _token.Dispose();
            _val = null;
        }
    }
}