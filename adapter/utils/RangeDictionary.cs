using System;
using System.Collections.Generic;

namespace ZXDebug
{
    class RangeDictionary<TKey, TValue> where TKey : IComparable<TKey>
    {
        SortedList<Range, TValue> _entries;
        IList<Range> _keys;

        public RangeDictionary()
        {
            _entries = new SortedList<Range, TValue>();
            _keys = _entries.Keys;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pKey"></param>
        /// <param name="pMode">
        /// 0 = only return the range that contains the key
        /// -1 = if containing range not found, return next lower
        /// 1 = if containing range not found, return next higher</param>
        /// <returns></returns>
        int BinarySearch( TKey pKey, int pMode )
        {
            int lower = 0;
            int upper = _keys.Count - 1;

            while( lower <= upper )
            {
                int middle = lower + (upper - lower) / 2;

                var lowerCompare = pKey.CompareTo( _keys[middle].LowerKey );
                var upperCompare = pKey.CompareTo( _keys[middle].UpperKey );

                if( lowerCompare >= 0 && upperCompare <= 0 )
                    return middle;

                if( lowerCompare < 0 )
                    upper = middle - 1;
                else
                    lower = middle + 1;
            }

            if( pMode == 1 && lower < _keys.Count )
                return lower;

            if( pMode == -1 && upper >= 0 )
                return upper;

            return -1;
        }

        /// <summary>
        /// Find the range which contains the specified key and return the range & value
        /// </summary>
        /// <param name="pKey">Key to look up</param>
        /// <param name="pRange">Range to be returned</param>
        /// <param name="pValue">Value to be returned</param>
        /// <returns>true if the item was found, false if not</returns>
        public bool TryGetValue( TKey pKey, out Range pRange, out TValue pValue )
        {
            int index = BinarySearch( pKey, 0 );

            if( index == -1 )
            {
                pRange = null;
                pValue = default(TValue);
                return false;
            }

            pRange = _keys[index];
            pValue = _entries[pRange];

            return true;
        }

        /// <summary>
        /// Find the range which contains the specified key and return the value
        /// </summary>
        /// <param name="pKey">Key to look up</param>
        /// <param name="pRange">Range to be returned</param>
        /// <param name="pValue">Value to be returned</param>
        /// <returns>true if the item was found, false if not</returns>
        public bool TryGetValue( TKey pKey, out TValue pValue )
        {
            int index = BinarySearch( pKey, 0 );

            if( index == -1 )
            {
                pValue = default(TValue);
                return false;
            }

            pValue = _entries[_keys[index]];

            return true;
        }

        public bool TryGetValueOrBelow( TKey pKey, out Range pRange, out TValue pValue )
        {
            int index = BinarySearch( pKey, -1 );

            if( index == -1 )
            {
                pRange = null;
                pValue = default( TValue );
                return false;
            }

            pRange = _keys[index];
            pValue = _entries[pRange];

            return true;
        }

        public bool TryGetValueOrAbove( TKey pKey, out Range pRange, out TValue pValue )
        {
            int index = BinarySearch( pKey, 1 );

            if( index == -1 )
            {
                pRange = null;
                pValue = default( TValue );
                return false;
            }

            pRange = _keys[index];
            pValue = _entries[pRange];

            return true;
        }


        public Range Add( TKey pKey, TValue pValue )
        {
            var range = new Range( pKey, pKey );
            _entries[range] = pValue;
            return range;
        }

        public Range Add( TKey pLower, TKey pUpper, TValue pValue )
        {
            var range = new Range( pLower, pUpper );
            _entries[range] = pValue;
            return range;
        }

        public Range Extend( Range pRange, TKey pInclude )
        {
            _entries.TryGetValue( pRange, out var entry );
            _entries.Remove( pRange );

            var newLower = pRange.LowerKey.CompareTo( pInclude ) > 0 ? pInclude : pRange.LowerKey;
            var newUpper = pRange.UpperKey.CompareTo( pInclude ) < 0 ? pInclude : pRange.UpperKey;

            var range = new Range( newLower, newUpper );
            _entries.Add( range, entry );
            return pRange;
        }

        public class Range : IComparable<Range>
        {
            public readonly TKey LowerKey;
            public readonly TKey UpperKey;

            public Range( TKey pLower, TKey pUpper )
            {
                LowerKey = pLower;
                UpperKey = pUpper;
            }

            public int CompareTo( Range other )
            {
                return LowerKey.CompareTo( other.LowerKey );
            }

            public override bool Equals( System.Object obj )
            {
                if( obj == null )
                    return false;

                Range p = obj as Range;
                if( (object)p == null )
                    return false;

                // Return true if the fields match:
                return this == p;
            }

            public bool Equals( Range p )
            {
                // If parameter is null return false:
                if( (object)p == null )
                    return false;

                // Return true if the fields match:
                return this == p;
            }

            public static bool operator ==( Range a, Range b )
            {
                // If both are null, or both are same instance, return true.
                if( object.ReferenceEquals( a, b ) )
                    return true;

                // If one is null, but not both, return false.
                if( ( (object)a == null ) || ( (object)b == null ) )
                    return false;

                // Return true if the fields match:
                return a.LowerKey.Equals( b.LowerKey ) && a.UpperKey.Equals( b.UpperKey );
            }

            public static bool operator !=( Range a, Range b )
            {
                return !( a == b );
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + LowerKey.GetHashCode();
                    hash = hash * 23 + UpperKey.GetHashCode();
                    return hash;
                }
            }

            public override string ToString()
            {
                return LowerKey.ToString() + "-" + UpperKey.ToString();
            }
        }
    }
}
