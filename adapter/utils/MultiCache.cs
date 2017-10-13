using System;
using System.Collections;
using System.Collections.Generic;

namespace ZXDebug.utils
{
    class Cache<TKey1, TKey2, TValue> : IEnumerable<KeyValuePair<TKey1, Cache<TKey2, TValue>>>
    {
        Cache<TKey1,Cache<TKey2,TValue>> _data;
        Func<TKey2, TValue> _factory;

        public Cache( Func<TKey2, TValue> factory, IEqualityComparer<TKey1> comparer1 = null, IEqualityComparer<TKey2> comparer2 = null )
        {
            _factory = factory;
            _data = new Cache<TKey1, Cache<TKey2, TValue>>(
                factory : key1 => new Cache<TKey2, TValue>( factory, comparer2 ),
                comparer : comparer1
            );
        }

        public TValue this[TKey1 index1, TKey2 index2]
        {
            get => _data[index1][index2];
            set => _data[index1][index2] = value;
        }

        public IEnumerator<KeyValuePair<TKey1, Cache<TKey2, TValue>>> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
