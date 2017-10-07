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
        /// <param name="index"></param>
        /// <param name="foundValue"></param>
        /// <returns>true if created, false if already existed</returns>
        public bool TryAdd( TIndex index, out TValue foundValue )
        {
            if( TryGetValue( index, out var result ) )
            {
                foundValue = result;
                return false;
            }

            result = new TValue();
            Add( index, result );
            foundValue = result;
            return true;
        }

        public bool TryAdd( TIndex index, out Range<TIndex> foundRange, out TValue foundValue )
        {
            if( TryGetValue( index, out foundRange, out foundValue ) )
                return false;

            foundValue = new TValue();
            foundRange = Add( index, foundValue );
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="mode">
        /// 0 = only return the range that contains the index
        /// -1 = if containing range not found, return next lower
        /// 1 = if containing range not found, return next higher</param>
        /// <returns></returns>
        int Find( TIndex index, int mode )
        {
            int lower = 0;
            int upper = _ranges.Count - 1;

            while( lower <= upper )
            {
                int middle = lower + ( upper - lower ) / 2;

                var lowerCompare = index.CompareTo( _ranges[middle].LowerKey );
                var upperCompare = index.CompareTo( _ranges[middle].UpperKey );

                if( lowerCompare >= 0 && upperCompare <= 0 )
                    return middle;

                if( lowerCompare < 0 )
                    upper = middle - 1;
                else
                    lower = middle + 1;
            }

            if( mode == 1 && lower < _ranges.Count )
                return lower;

            if( mode == -1 && upper >= 0 )
                return upper;

            return -1;
        }

        /// <summary>
        /// Find the range which contains the specified key and return the range & value
        /// </summary>
        /// <param name="index">Key to look up</param>
        /// <param name="foundRange">Range to be returned</param>
        /// <param name="foundValue">Value to be returned</param>
        /// <returns>true if the item was found, false if not</returns>
        public bool TryGetValue( TIndex index, out Range<TIndex> foundRange, out TValue foundValue )
        {
            var pos = Find( index, 0 );

            if( pos == -1 )
            {
                foundRange = null;
                foundValue = default( TValue );
                return false;
            }

            foundRange = _ranges[pos];
            foundValue = this[foundRange];

            return true;
        }

        /// <summary>
        /// Find the range which contains the specified key and return the value
        /// </summary>
        /// <param name="index">Key to look up</param>
        /// <param name="foundValue">Value to be returned</param>
        /// <returns>true if the item was found, false if not</returns>
        public bool TryGetValue( TIndex index, out TValue foundValue )
        {
            var pos = Find( index, 0 );

            if( pos == -1 )
            {
                foundValue = default( TValue );
                return false;
            }

            foundValue = this[_ranges[pos]];

            return true;
        }

        /// <summary>
        /// Find the range which contains the specified key, or the range immediately preceding the position the key would be in, 
        /// and and return the range and value
        /// </summary>
        /// <param name="index">Key to look up</param>
        /// <param name="foundRange">Range to be returned</param>
        /// <param name="foundValue">Value to be returned</param>
        /// <returns>true if a suitable range was found, false if not</returns>
        public bool TryGetValueOrBelow( TIndex index, out Range<TIndex> foundRange, out TValue foundValue )
        {
            var pos = Find( index, -1 );

            if( pos == -1 )
            {
                foundRange = null;
                foundValue = default( TValue );
                return false;
            }

            foundRange = _ranges[pos];
            foundValue = this[foundRange];

            return true;
        }

        /// <summary>
        /// Find the range which contains the specified key, or the range immediately following the position the key would be in, 
        /// and and return the range and value
        /// </summary>
        /// <param name="index">Key to look up</param>
        /// <param name="foundRange">Range to be returned</param>
        /// <param name="foundValue">Value to be returned</param>
        /// <returns>true if a suitable range was found, false if not</returns>
        public bool TryGetValueOrAbove( TIndex index, out Range<TIndex> foundRange, out TValue foundValue )
        {
            var pos = Find( index, 1 );

            if( pos == -1 )
            {
                foundRange = null;
                foundValue = default( TValue );
                return false;
            }

            foundRange = _ranges[pos];
            foundValue = this[foundRange];

            return true;
        }

        /// <summary>
        /// Add a new item to the dictionary at the specified position
        /// </summary>
        /// <param name="index">Start and end of new range</param>
        /// <param name="value">Value associated with range</param>
        /// <returns>Range of new value</returns>
        public Range<TIndex> Add( TIndex index, TValue value )
        {
            var range = new Range<TIndex>( index, index );
            this[range] = value;
            return range;
        }

        /// <summary>
        /// Add a new item to the dictionary covering the specified range
        /// </summary>
        /// <param name="lower">Start of new range</param>
        /// <param name="upper">End of new range</param>
        /// <param name="value">Value associated with range</param>
        /// <returns>Range of new value</returns>
        public Range<TIndex> Add( TIndex lower, TIndex upper, TValue value )
        {
            var range = new Range<TIndex>( lower, upper );
            this[range] = value;
            return range;
        }

        /// <summary>
        /// Update the specified range to include a new position
        /// </summary>
        /// <param name="range">Existing range to update</param>
        /// <param name="index">New position to include</param>
        /// <returns></returns>
        public Range<TIndex> Extend( Range<TIndex> range, TIndex index )
        {
            TryGetValue( range, out var entry );

            var compareLower = range.LowerKey.CompareTo( index );
            var compareUpper = range.UpperKey.CompareTo( index );

            if( compareLower <= 0 || compareUpper >= 0 )
                return range;

            Remove( range );

            var newLower = compareLower > 0 ? index : range.LowerKey;
            var newUpper = compareUpper < 0 ? index : range.UpperKey;

            var newRange = new Range<TIndex>( newLower, newUpper );
            Add( newRange, entry );
            return range;
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
