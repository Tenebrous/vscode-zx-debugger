using System;
using System.Collections.Generic;

namespace ZXDebug
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey">Key type - each entry has a key, and the key must be comparable as they are put in order</typeparam>
    /// <typeparam name="TValue">Value type - each key is associated with a value</typeparam>
    public class SpatialDictionary<TKey, TValue> : SortedList<TKey, TValue>
        where TKey : IComparable<TKey>
        where TValue : new()
    {
        IList<TKey> _indices;
        IList<TValue> _values;
        Func<TKey, TValue> _factory;

        public SpatialDictionary()
        {
            _indices = Keys;
            _values = Values;
            _factory = pKey => new TValue();
        }

        public SpatialDictionary( Func<TKey, TValue> pFactory )
        {
            _indices = Keys;
            _values = Values;
            _factory = pFactory ?? ( pKey => new TValue() );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pKey"></param>
        /// <param name="pValue"></param>
        /// <param name="pFactory"></param>
        /// <returns>true if created, false if already existed</returns>
        public bool TryAdd( TKey pKey, out TValue pValue, Func<TKey, TValue> pFactory = null )
        {
            if( TryGetValue( pKey, out var result ) )
            {
                pValue = result;
                return false;
            }

            result = (pFactory ?? _factory)(pKey);
            Add( pKey, result );
            pValue = result;
            return true;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pKey"></param>
        /// <param name="pMode">
        /// 0 = only return the exact entry
        /// -1 = if key not found, return next lower
        /// 1 = if key not found, return next higher</param>
        /// <returns></returns>
        int Find( TKey pKey, int pMode )
        {
            int lower = 0;
            int upper = _indices.Count - 1;

            while( lower <= upper )
            {
                int middle = lower + ( upper - lower ) / 2;

                var lowerCompare = pKey.CompareTo( _indices[middle] );
                var upperCompare = pKey.CompareTo( _indices[middle] );

                if( lowerCompare >= 0 && upperCompare <= 0 )
                    return middle;

                if( lowerCompare < 0 )
                    upper = middle - 1;
                else
                    lower = middle + 1;
            }

            if( pMode == 1 && lower < _indices.Count )
                return lower;

            if( pMode == -1 && upper >= 0 )
                return upper;

            return -1;
        }


        /// <summary>
        /// Find the item at the specified index, or immediately preceding it
        /// and return both the found index and value
        /// </summary>
        /// <param name="pKey">Key to find</param>
        /// <param name="pFoundIndex">Key that was found</param>
        /// <param name="pValue">Value that was found</param>
        /// <returns>true if a suitable item was found</returns>
        public bool TryGetValueOrBelow( TKey pKey, out TKey pFoundIndex, out TValue pValue )
        {
            var index = Find( pKey, -1 );

            if( index == -1 )
            {
                pFoundIndex = default( TKey );
                pValue = default( TValue );
                return false;
            }

            pFoundIndex = _indices[index];
            pValue = _values[index];

            return true;
        }

        /// <summary>
        /// Find the item at the specified key, or immediately following it
        /// and return both the found index and value
        /// </summary>
        /// <param name="pKey">Key to find</param>
        /// <param name="pFoundIndex">Key that was found</param>
        /// <param name="pValue">Value that was found</param>
        /// <returns>true if a suitable item was found</returns>
        public bool TryGetValueOrAbove( TKey pKey, out TKey pFoundIndex, out TValue pValue )
        {
            var index = Find( pKey, 1 );

            if( index == -1 )
            {
                pFoundIndex = default( TKey );
                pValue = default( TValue );
                return false;
            }

            pFoundIndex = _indices[index];
            pValue = _values[index];

            return true;
        }
    }
}
