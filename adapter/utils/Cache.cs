using System;
using System.Collections.Generic;

namespace ZXDebug
{
    /// <summary>
    /// Like a Dictionary, but will create any missing item
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class Cache<TKey, TValue> : Dictionary<TKey, TValue> where TValue : new()
    {
        Func<TKey, TValue> _factory;

        public Cache()
        {
            _factory = pKey => new TValue();
        }

        public Cache( Func<TKey, TValue> pFactory )
        {
            _factory = pFactory;
        }

        public Cache( IEqualityComparer<TKey> pCompare ) : base( pCompare )
        {
            _factory = pKey => new TValue();
        }

        public Cache( Func<TKey, TValue> pFactory, IEqualityComparer<TKey> pCompare = null ) : base( pCompare )
        {
            _factory = pFactory;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pIndex"></param>
        /// <param name="pValue"></param>
        /// <returns>true if created, false if already existed</returns>
        public bool TryAdd( TKey pIndex, out TValue pValue )
        {
            if( TryGetValue( pIndex, out var result ) )
            {
                pValue = result;
                return true;
            }

            result = _factory(pIndex);
            Add( pIndex, result );
            pValue = result;
            return false;
        }

        public new TValue this[ TKey pKey ]
        {
            get
            {
                if( !TryGetValue( pKey, out var result ) )
                {
                    result = _factory( pKey );
                    base[pKey] = result;
                }

                return result;
            }
            set { base[pKey] = value; }
        }
    } 
}
