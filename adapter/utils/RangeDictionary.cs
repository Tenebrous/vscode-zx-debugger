using System;
using System.Collections.Generic;

namespace ZXDebug
{
    public class RangeDictionary<TIndex, TValue> : SortedList<Range<TIndex>, TValue> 
        where TIndex : IComparable<TIndex> 
        where TValue : new()
    {
        //SortedList<Range, TValue> _entries;
        IList<Range<TIndex>> _ranges;

        public RangeDictionary()
        {
            //_entries = new SortedList<Range, TValue>();
            _ranges = Keys;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pIndex"></param>
        /// <param name="pValue"></param>
        /// <returns>true if created, false if already existed</returns>
        public bool TryAdd( TIndex pIndex, out TValue pValue )
        {
            if( TryGetValue( pIndex, out var result ) )
            {
                pValue = result;
                return false;
            }

            result = new TValue();
            Add( pIndex, result );
            pValue = result;
            return true;
        }

        public bool TryAdd( TIndex pIndex, out Range<TIndex> pRange, out TValue pValue )
        {
            if( TryGetValue( pIndex, out pRange, out pValue ) )
                return false;

            pValue = new TValue();
            pRange = Add( pIndex, pValue );
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pIndex"></param>
        /// <param name="pMode">
        /// 0 = only return the range that contains the index
        /// -1 = if containing range not found, return next lower
        /// 1 = if containing range not found, return next higher</param>
        /// <returns></returns>
        int Find( TIndex pIndex, int pMode )
        {
            int lower = 0;
            int upper = _ranges.Count - 1;

            while( lower <= upper )
            {
                int middle = lower + ( upper - lower ) / 2;

                var lowerCompare = pIndex.CompareTo( _ranges[middle].LowerKey );
                var upperCompare = pIndex.CompareTo( _ranges[middle].UpperKey );

                if( lowerCompare >= 0 && upperCompare <= 0 )
                    return middle;

                if( lowerCompare < 0 )
                    upper = middle - 1;
                else
                    lower = middle + 1;
            }

            if( pMode == 1 && lower < _ranges.Count )
                return lower;

            if( pMode == -1 && upper >= 0 )
                return upper;

            return -1;
        }

        /// <summary>
        /// Find the range which contains the specified key and return the range & value
        /// </summary>
        /// <param name="pIndex">Key to look up</param>
        /// <param name="pRange">Range to be returned</param>
        /// <param name="pValue">Value to be returned</param>
        /// <returns>true if the item was found, false if not</returns>
        public bool TryGetValue( TIndex pIndex, out Range<TIndex> pRange, out TValue pValue )
        {
            var index = Find( pIndex, 0 );

            if( index == -1 )
            {
                pRange = null;
                pValue = default( TValue );
                return false;
            }

            pRange = _ranges[index];
            pValue = this[pRange];

            return true;
        }

        /// <summary>
        /// Find the range which contains the specified key and return the value
        /// </summary>
        /// <param name="pIndex">Key to look up</param>
        /// <param name="pValue">Value to be returned</param>
        /// <returns>true if the item was found, false if not</returns>
        public bool TryGetValue( TIndex pIndex, out TValue pValue )
        {
            var index = Find( pIndex, 0 );

            if( index == -1 )
            {
                pValue = default( TValue );
                return false;
            }

            pValue = this[_ranges[index]];

            return true;
        }

        /// <summary>
        /// Find the range which contains the specified key, or the range immediately preceding the position the key would be in, 
        /// and and return the range and value
        /// </summary>
        /// <param name="pKey">Key to look up</param>
        /// <param name="pRange">Range to be returned</param>
        /// <param name="pValue">Value to be returned</param>
        /// <returns>true if a suitable range was found, false if not</returns>
        public bool TryGetValueOrBelow( TIndex pKey, out Range<TIndex> pRange, out TValue pValue )
        {
            var index = Find( pKey, -1 );

            if( index == -1 )
            {
                pRange = null;
                pValue = default( TValue );
                return false;
            }

            pRange = _ranges[index];
            pValue = this[pRange];

            return true;
        }

        /// <summary>
        /// Find the range which contains the specified key, or the range immediately following the position the key would be in, 
        /// and and return the range and value
        /// </summary>
        /// <param name="pKey">Key to look up</param>
        /// <param name="pRange">Range to be returned</param>
        /// <param name="pValue">Value to be returned</param>
        /// <returns>true if a suitable range was found, false if not</returns>
        public bool TryGetValueOrAbove( TIndex pKey, out Range<TIndex> pRange, out TValue pValue )
        {
            var index = Find( pKey, 1 );

            if( index == -1 )
            {
                pRange = null;
                pValue = default( TValue );
                return false;
            }

            pRange = _ranges[index];
            pValue = this[pRange];

            return true;
        }

        /// <summary>
        /// Add a new item to the dictionary at the specified position
        /// </summary>
        /// <param name="pKey">Start and end of new range</param>
        /// <param name="pValue">Value associated with range</param>
        /// <returns>Range of new value</returns>
        public Range<TIndex> Add( TIndex pKey, TValue pValue )
        {
            var range = new Range<TIndex>( pKey, pKey );
            this[range] = pValue;
            return range;
        }

        /// <summary>
        /// Add a new item to the dictionary covering the specified range
        /// </summary>
        /// <param name="pLower">Start of new range</param>
        /// <param name="pUpper">End of new range</param>
        /// <param name="pValue">Value associated with range</param>
        /// <returns>Range of new value</returns>
        public Range<TIndex> Add( TIndex pLower, TIndex pUpper, TValue pValue )
        {
            var range = new Range<TIndex>( pLower, pUpper );
            this[range] = pValue;
            return range;
        }

        /// <summary>
        /// Update the specified range to include a new position
        /// </summary>
        /// <param name="pRange">Existing range to update</param>
        /// <param name="pInclude">New position to include</param>
        /// <returns></returns>
        public Range<TIndex> Extend( Range<TIndex> pRange, TIndex pInclude )
        {
            TryGetValue( pRange, out var entry );

            var compareLower = pRange.LowerKey.CompareTo( pInclude );
            var compareUpper = pRange.UpperKey.CompareTo( pInclude );

            if( compareLower <= 0 || compareUpper >= 0 )
                return pRange;

            Remove( pRange );

            var newLower = compareLower > 0 ? pInclude : pRange.LowerKey;
            var newUpper = compareUpper < 0 ? pInclude : pRange.UpperKey;

            var range = new Range<TIndex>( newLower, newUpper );
            Add( range, entry );
            return pRange;
        }
    }

    /// <summary>
    /// A range with a lower and upper index
    /// </summary>
    public class Range<TIndex> : IComparable<Range<TIndex>> where TIndex : IComparable<TIndex>
    {
        public readonly TIndex LowerKey;
        public readonly TIndex UpperKey;

        /// <summary>
        /// Create a new range starting at pLower and ending at pUpper
        /// </summary>
        /// <param name="pLower">Lower index of the range</param>
        /// <param name="pUpper">Upper index of the range</param>
        public Range( TIndex pLower, TIndex pUpper )
        {
            LowerKey = pLower;
            UpperKey = pUpper;
        }

        public int CompareTo( Range<TIndex> pOther )
        {
            return LowerKey.CompareTo( pOther.LowerKey );
        }

        public override bool Equals( object pOther )
        {
            if( pOther == null )
                return false;

            var p = pOther as Range<TIndex>;
            if( (object)p == null )
                return false;

            // Return true if the fields match:
            return this == p;
        }

        public bool Equals( Range<TIndex> p )
        {
            // If parameter is null return false:
            if( (object)p == null )
                return false;

            // Return true if the fields match:
            return this == p;
        }

        public static bool operator ==( Range<TIndex> pLeft, Range<TIndex> pRight )
        {
            // If both are null, or both are same instance, return true.
            if( object.ReferenceEquals( pLeft, pRight ) )
                return true;

            // If one is null, but not both, return false.
            if( ( (object)pLeft == null ) || ( (object)pRight == null ) )
                return false;

            // Return true if the fields match:
            return pLeft.LowerKey.Equals( pRight.LowerKey ) && pLeft.UpperKey.Equals( pRight.UpperKey );
        }

        public static bool operator !=( Range<TIndex> pLeft, Range<TIndex> pRight )
        {
            return !( pLeft == pRight );
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
