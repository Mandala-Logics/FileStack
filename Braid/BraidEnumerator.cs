using System.Collections;
using System.Collections.Generic;

namespace MandalaLogics.Packing
{
    public sealed partial class Braid
    {
        public class BraidEnumerator : IEnumerator<Strand>
        {
            public Strand Current { get; private set; } = null!;

            object? IEnumerator.Current => Current;

            private readonly Braid _owner;
            private int _pos;

            public BraidEnumerator(Braid owner)
            {
                _owner = owner;
                _pos = 0;
            }
            
            public bool MoveNext()
            {
                _owner._knotLock.EnterReadLock();

                try
                {
                    _pos++;

                    if (_pos > _owner._strands.Count) return false;

                    Current = _owner.GetStrand((uint)_pos);

                    return true;
                }
                finally
                {
                    _owner._knotLock.ExitReadLock();
                }
            }

            public void Reset()
            {
                _pos = 0;
            }
            
            public void Dispose() {}
        }
    }
}