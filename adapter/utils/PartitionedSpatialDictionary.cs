using System;
using System.Collections;
using System.Collections.Generic;

namespace ZXDebug.utils
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TPartition">Partition type - the dictionary is divided into partitions and no partition can see data from the other</typeparam>
    /// <typeparam name="TKey">Key type - each entry has a key, and the key must be comparable as they are put in order</typeparam>
    /// <typeparam name="TValue">Value type - each key is associated with a value</typeparam>
    public class SpatialDictionary<TPartition, TKey, TValue> : IEnumerable<KeyValuePair<TPartition, SpatialDictionary<TKey, TValue>>>
        where TKey : IComparable<TKey> 
        where TValue : new()
    {
        Dictionary<TPartition,SpatialDictionary<TKey, TValue>> _data 
            = new Cache<TPartition, SpatialDictionary<TKey, TValue>>();

        Func<TPartition, TKey, TValue> _factory;

        public SpatialDictionary()
        {
            _factory = null;
        }

        public SpatialDictionary( Func<TPartition, TKey, TValue> pFactory )
        {
            _factory = pFactory;
        }

        /// <summary>
        /// Attempt to add a new item to the dictionary
        /// </summary>
        /// <param name="pPartition"></param>
        /// <param name="pKey"></param>
        /// <param name="pValue"></param>
        /// <param name="pFactory"></param>
        /// <returns>true if created, false if already existed</returns>
        public bool TryAdd( TPartition pPartition, TKey pKey, out TValue pValue, Func<TPartition, TKey, TValue> pFactory = null )
        {
            if( !_data.TryGetValue( pPartition, out var spatial ) )
            {
                spatial = new SpatialDictionary<TKey, TValue>( SubFactory( pPartition, pFactory ) );
                _data[pPartition] = spatial;
            }

            return spatial.TryAdd( pKey, out pValue, SubFactory(pPartition, pFactory) );
        }

        Func<TKey, TValue> SubFactory( TPartition pPartition, Func<TPartition, TKey, TValue> pFactory )
        {
            if( pFactory != null )
                return pKey => pFactory( pPartition, pKey );

            if( _factory != null )
                return pKey => _factory( pPartition, pKey );

            return null;
        }

        public bool TryGetValue( TPartition pPartition, TKey pKey, out TValue pValue )
        {
            if( !_data.TryGetValue( pPartition, out var spatial ) )
            {
                pValue = default( TValue );
                return false;
            }

            return spatial.TryGetValue( pKey, out pValue );
        }

        public bool TryGetValueOrBelow( TPartition pPartition, TKey pKey, out TKey pFoundIndex, out TValue pValue )
        {
            if( !_data.TryGetValue( pPartition, out var spatial ) )
            {
                pValue = default( TValue );
                pFoundIndex = default( TKey );
                return false;
            }

            return spatial.TryGetValueOrBelow( pKey, out pFoundIndex, out pValue );
        }

        public bool TryGetValueOrAbove( TPartition pPartition, TKey pKey, out TKey pFoundIndex, out TValue pValue )
        {
            if( !_data.TryGetValue( pPartition, out var spatial ) )
            {
                pValue = default( TValue );
                pFoundIndex = default( TKey );
                return false;
            }

            return spatial.TryGetValueOrAbove( pKey, out pFoundIndex, out pValue );
        }

        public IEnumerator<KeyValuePair<TPartition, SpatialDictionary<TKey, TValue>>> GetEnumerator()
        {
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
