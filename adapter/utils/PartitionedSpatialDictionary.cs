using System;
using System.Collections.Generic;

namespace ZXDebug.utils
{
    public class PartitionedSpatialDictionary<TPartition, TKey, TValue> 
        where TKey : IComparable<TKey> 
        where TValue : new()
    {
        Dictionary<TPartition,SpatialDictionary<TKey, TValue>> _data 
            = new Cache<TPartition, SpatialDictionary<TKey, TValue>>();

        Func<TKey, TValue> _factory;

        public PartitionedSpatialDictionary()
        {
            _factory = null;
        }

        public PartitionedSpatialDictionary( Func<TKey, TValue> pFactory )
        {
            _factory = pFactory;
        }

        public bool TryAdd( TPartition pPartition, TKey pKey, out TValue pValue, Func<TKey, TValue> pFactory = null )
        {
            if( !_data.TryGetValue( pPartition, out var spatial ) )
            {
                spatial = new SpatialDictionary<TKey, TValue>( _factory );
                _data[pPartition] = spatial;
            }

            return spatial.TryAdd( pKey, out pValue, pFactory );
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
    }
}
