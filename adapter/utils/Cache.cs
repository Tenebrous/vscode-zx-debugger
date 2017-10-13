using System;
using System.Collections.Generic;

namespace ZXDebug
{
    /// <summary>
    /// Like a Dictionary, but will create any missing item
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class Cache<TKey, TValue> : Dictionary<TKey, TValue> 
    {
        Func<TKey, TValue> _factory;

        public Cache( Func<TKey, TValue> factory )
        {
            _factory = factory;
        }

        public Cache( Func<TKey, TValue> factory, IEqualityComparer<TKey> comparer = null ) : base( comparer )
        {
            _factory = factory;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <returns>true if created, false if already existed</returns>
        public bool TryAdd( TKey index, out TValue value )
        {
            if( TryGetValue( index, out var result ) )
            {
                value = result;
                return false;
            }

            result = _factory(index);
            Add( index, result );
            value = result;
            return true;
        }

        public new TValue this[ TKey index ]
        {
            get
            {
                if( TryGetValue( index, out var result ) )
                    return result;

                result = _factory( index );
                base[index] = result;

                return result;
            }
            set { base[index] = value; }
        }
    } 
}
