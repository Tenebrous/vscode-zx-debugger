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

        public SpatialDictionary( Func<TKey, TValue> factory )
        {
            _indices = Keys;
            _values = Values;
            _factory = factory ?? ( pKey => new TValue() );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="foundValue"></param>
        /// <param name="factory"></param>
        /// <returns>true if created, false if already existed</returns>
        public bool TryAdd( TKey index, out TValue foundValue, Func<TKey, TValue> factory = null )
        {
            if( TryGetValue( index, out var result ) )
            {
                foundValue = result;
                return false;
            }

            result = (factory ?? _factory)(index);
            Add( index, result );
            foundValue = result;
            return true;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="mode">
        /// 0 = only return the exact entry
        /// -1 = if key not found, return next lower
        /// 1 = if key not found, return next higher</param>
        /// <returns></returns>
        int Find( TKey index, int mode )
        {
            int lower = 0;
            int upper = _indices.Count - 1;

            while( lower <= upper )
            {
                int middle = lower + ( upper - lower ) / 2;

                var lowerCompare = index.CompareTo( _indices[middle] );
                var upperCompare = index.CompareTo( _indices[middle] );

                if( lowerCompare >= 0 && upperCompare <= 0 )
                    return middle;

                if( lowerCompare < 0 )
                    upper = middle - 1;
                else
                    lower = middle + 1;
            }

            if( mode == 1 && lower < _indices.Count )
                return lower;

            if( mode == -1 && upper >= 0 )
                return upper;

            return -1;
        }


        /// <summary>
        /// Find the item at the specified index, or immediately preceding it
        /// and return both the found index and value
        /// </summary>
        /// <param name="index">Key to find</param>
        /// <param name="foundIndex">Key that was found</param>
        /// <param name="foundValue">Value that was found</param>
        /// <returns>true if a suitable item was found</returns>
        public bool TryGetValueOrBelow( TKey index, out TKey foundIndex, out TValue foundValue )
        {
            var pos = Find( index, -1 );

            if( pos == -1 )
            {
                foundIndex = default( TKey );
                foundValue = default( TValue );
                return false;
            }

            foundIndex = _indices[pos];
            foundValue = _values[pos];

            return true;
        }

        /// <summary>
        /// Find the item at the specified key, or immediately following it
        /// and return both the found index and value
        /// </summary>
        /// <param name="index">Key to find</param>
        /// <param name="foundIndex">Key that was found</param>
        /// <param name="foundValue">Value that was found</param>
        /// <returns>true if a suitable item was found</returns>
        public bool TryGetValueOrAbove( TKey index, out TKey foundIndex, out TValue foundValue )
        {
            var pos = Find( index, 1 );

            if( pos == -1 )
            {
                foundIndex = default( TKey );
                foundValue = default( TValue );
                return false;
            }

            foundIndex = _indices[pos];
            foundValue = _values[pos];

            return true;
        }
    }
}
