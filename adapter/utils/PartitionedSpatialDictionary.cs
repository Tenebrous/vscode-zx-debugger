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
    {
        Dictionary<TPartition,SpatialDictionary<TKey, TValue>> _data;

        Func<TPartition, TKey, TValue> _factory;

        public SpatialDictionary( Func<TPartition, TKey, TValue> factory )
        {
            _factory = factory;

            _data = new Cache<TPartition, SpatialDictionary<TKey, TValue>>( 
                partition => new SpatialDictionary<TKey, TValue>( SubFactory( partition, _factory ) )
            );
        }

        /// <summary>
        /// Attempt to add a new item to the dictionary
        /// </summary>
        /// <param name="partition"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="factory"></param>
        /// <returns>true if created, false if already existed</returns>
        public bool TryAdd( TPartition partition, TKey key, out TValue value, Func<TPartition, TKey, TValue> factory = null )
        {
            if( !_data.TryGetValue( partition, out var spatial ) )
            {
                spatial = new SpatialDictionary<TKey, TValue>( SubFactory( partition, factory ) );
                _data[partition] = spatial;
            }

            return spatial.TryAdd( key, out value, SubFactory(partition, factory) );
        }

        Func<TKey, TValue> SubFactory( TPartition partition, Func<TPartition, TKey, TValue> factory )
        {
            if( factory != null )
                return pKey => factory( partition, pKey );

            if( _factory != null )
                return pKey => _factory( partition, pKey );

            return null;
        }

        public bool TryGetValue( TPartition partition, TKey key, out TValue value )
        {
            if( !_data.TryGetValue( partition, out var spatial ) )
            {
                value = default( TValue );
                return false;
            }

            return spatial.TryGetValue( key, out value );
        }

        public bool TryGetValueOrBelow( TPartition partition, TKey index, out TKey foundIndex, out TValue foundValue )
        {
            if( !_data.TryGetValue( partition, out var spatial ) )
            {
                foundValue = default( TValue );
                foundIndex = default( TKey );
                return false;
            }

            return spatial.TryGetValueOrBelow( index, out foundIndex, out foundValue );
        }

        public bool TryGetValueOrAbove( TPartition partition, TKey index, out TKey foundIndex, out TValue foundValue )
        {
            if( !_data.TryGetValue( partition, out var spatial ) )
            {
                foundValue = default( TValue );
                foundIndex = default( TKey );
                return false;
            }

            return spatial.TryGetValueOrAbove( index, out foundIndex, out foundValue );
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
