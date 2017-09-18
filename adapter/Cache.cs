using System;
using System.Collections;
using System.Collections.Generic;

namespace ZXDebug
{
    /// <summary>
    /// Like a Dictionary, but will create any missing item
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class Cache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        Func<TKey, TValue> _factory;

        Dictionary<TKey, TValue> _dictionary;

        public Cache( Func<TKey, TValue> pFactory, IEqualityComparer<TKey> pCompare = null )
        {
            _factory = pFactory;
            _dictionary = new Dictionary<TKey, TValue>(pCompare);
        }

        public TValue this[ TKey pKey ]
        {
            get
            {
                if( _dictionary.TryGetValue( pKey, out var result ) )
                    return result;

                result = _factory( pKey );

                _dictionary[pKey] = result;

                return result;
            }

            set => _dictionary[pKey] = value;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGetValue( TKey pKey, out TValue pValue )
        {
            return _dictionary.TryGetValue( pKey, out pValue );
        }
    } 
}
