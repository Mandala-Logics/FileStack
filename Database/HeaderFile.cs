using System;
using System.IO;
using MandalaLogics.Encoding;

namespace MandalaLogics.Database
{
    public class HeaderFile<T> : IDisposable where T : class, IEncodable
    {
        public bool ValueSet
        {
            get
            {
                lock (SyncRoot)
                {
                    return _val is { };
                }
            }
        }

        public T Value
        {
            get
            {
                lock (SyncRoot)
                {
                    return _val ?? throw new InvalidOperationException("Value has not been set.");
                }
            }
            set => SetValue(value);
        }

        public bool Disposed { get; private set; } = false;

        public readonly object SyncRoot = new object();

        private readonly Stream _stream;
        private T? _val;
        
        public HeaderFile(Stream stream)
        {
            _stream = stream;
            
            stream.Seek(0L, SeekOrigin.Begin);

            try
            {
                EncodedValue.Read(stream, out var ev);

                if (ev.Value is T obj)
                {
                    _val = obj;
                }
                else
                {
                    throw new HeaderFileNotValidException("Wrong type of encoded value read.");
                }
            }
            catch (EndOfStreamException)
            {
                _val = null;
            }
            catch (EncodingException e)
            {
                throw new HeaderFileNotValidException("Header file not valid.", e);
            }
        }

        public void SetValue(T value)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(HeaderFile<T>));

            lock (SyncRoot)
            {
                _val = value ?? throw new ArgumentNullException(nameof(value));

                _stream.Seek(0L, SeekOrigin.Begin);

                var w = _val.Encode().Write(_stream);
            
                _stream.SetLength(w);
            }
        }

        public void Flush()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(HeaderFile<T>));

            lock (SyncRoot)
            {
                _stream.Seek(0L, SeekOrigin.Begin);

                var w = _val?.Encode().Write(_stream) ?? throw new InvalidOperationException("Value has not been set.");
            
                _stream.SetLength(w);
            
                _stream.Flush();
            }
        }

        public void Dispose()
        {
            _stream.Dispose();

            Disposed = true;
        }
    }
}