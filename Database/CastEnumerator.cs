using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace MandalaLogics.Database
{
    public sealed class CastEnumerator<TIn, TOut> : IEnumerator<TOut>
    {
        public TOut Current
        {
            get
            {
                try { return _convertor.Invoke(_iEnum.Current); }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException;
                }
            }
        }

        object IEnumerator.Current => Current!;
        private readonly IEnumerator<TIn> _iEnum;
        private readonly Func<TIn, TOut> _convertor;
        
        public CastEnumerator(IEnumerator<TIn> iEnum, Func<TIn, TOut> convertor)
        {
            this._iEnum = iEnum ?? throw new ArgumentNullException(nameof(iEnum));
            this._convertor = convertor ?? throw new ArgumentNullException(nameof(convertor));
        }
        
        public void Dispose() => _iEnum.Dispose();
        public bool MoveNext() => _iEnum.MoveNext();
        public void Reset() => _iEnum.Reset();
    }
}