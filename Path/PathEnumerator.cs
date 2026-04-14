using System.Collections;
using System.Collections.Generic;

namespace MandalaLogics.Path
{
    public class PathEnumerator : IEnumerator<string>
    {
        public string Current { get; private set; } = null!;

        object? IEnumerator.Current => Current;

        private int _pos = -1;
        private readonly PathBase _path;

        public PathEnumerator(PathBase path)
        {
            _path = path;
        }
        
        public bool MoveNext()
        {
            if (++_pos == _path.Count - 1)
            {
                if (_path.HasExtension)
                {
                    Current = string.Join('.', _path[_pos], _path.Extension);
                    return true;
                }
            }
            
            if (_pos >= _path.Count)
            {
                Current = string.Empty;
                return false;
            }
            
            Current = _path[_pos];
                
            return true;
        }

        public void Reset()
        {
            _pos = -1;
        }

        public void Dispose() { }
    }
}