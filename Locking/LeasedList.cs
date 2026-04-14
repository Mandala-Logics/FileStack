using System;
using System.Collections.Generic;

namespace MandalaLogics.Locking
{
    public class LeasedList<T> : ILeaseable<IList<T>>
    {
        private readonly IList<T> _baseList;
        private readonly SyncLock _lock = new SyncLock();

        public LeasedList(IList<T> list)
        {
            _baseList = list;
        }

        public LeasedList()
        {
            _baseList = new List<T>();
        }

        public LeasedList(int capacity)
        {
            _baseList = new List<T>(capacity);
        }
        
        public LeasedList(IEnumerable<T> values)
        {
            _baseList = new List<T>(values);
        }

        public Lease<IList<T>> GetLease()
        {
            return new Lease<IList<T>>(_baseList, _lock.Take());
        }

        public void Use(Action<IList<T>> action)
        {
            using var l = _lock.Take();
            
            action.Invoke(_baseList);
        }
    }
}