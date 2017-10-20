using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Text;
using ZXDebug;

namespace Spectrum
{
    /// <summary>
    /// Represents the current state of the device's memory 
    /// </summary>
    public class Memory : Loggable
    {
        // memory is always divided into 8k slots

        public bool PagingEnabled;

        Cache<ushort, Slot> _slots;
        Cache<BankID, Bank> _banks;

        public List<Slot> Slots { get; } = new List<Slot>();

        Machine _machine;

        public Memory( Machine machine )
        {
            _machine = machine;

            _slots = new Cache<ushort, Slot>(
                factory : address => new Slot()
                {
                    Min = address,
                    Max = (ushort)( address + 0x1FFF )
                }
            );

            _banks = new Cache<BankID, Bank>(
                factory : pID => new Bank()
                {
                    ID = pID
                }
            );
        }

        public Slot GetSlot( ushort address )
        {
            ushort alignedAddress;

            if( PagingEnabled )
            {
                var index = address / 0x2000;
                alignedAddress = (ushort)( index * 0x2000 );
            }
            else
            {
                alignedAddress = 0;
            }

            if( _slots.TryAdd( alignedAddress, out var slot ) )
            {
                Slots.Add( slot );
                Slots.Sort( comparison : ( pLeft, pRight ) => pLeft.Min.CompareTo( pRight.Min ) );
            }

            return slot;
        }

        public BankID GetMappedBank( ushort address )
        {
            return GetSlot( address )?.Bank?.ID ?? BankID.Unpaged();
        }

        public void SetAddressBank( ushort alignedAddress, ushort size, Bank bank )
        {
            GetSlot( alignedAddress ).Bank = bank;
            bank.PagedAddress = alignedAddress;
            bank.IsPagedIn = true;
        }

        public Bank Bank( BankID pID )
        {
            return _banks[pID];
        }

        public void ClearConfiguration()
        {
            foreach( var b in _banks )
            {
                b.Value.IsPagedIn = false;
            }
        }

        public int Read( ushort address, byte[] bytes, int start = 0, int length = 0 )
        {
            if( length == 0 )
                length = bytes.Length - start;

            return _machine.Connection.ReadMemory( address, bytes, start, length );
        }

        public bool ReadConfiguration()
        {
            return _machine.Connection.ReadMemoryConfiguration( this );
        }

        StringBuilder _tempToString = new StringBuilder();

        public override string ToString()
        {
            _tempToString.Clear();
            foreach( var slot in Slots )
            {
                if( _tempToString.Length > 0 )
                    _tempToString.Append( ' ' );

                if( slot.Bank != null )
                    _tempToString.Append( $"{slot.Min:X4}:{slot.Bank.ID,-10}" );
                else
                    _tempToString.Append( $"{slot.Min:X4}:?" );
            }

            return _tempToString.ToString();
        }


        const int _logChars = 128;
        const int _logDivWidth = 10;
        const int _logMemPerChar = 65536 / _logChars;

        public void Log()
        {
            var sorted = new List<Bank>( _banks.Values );
            sorted.Sort( ( left, right ) => left.PagedAddress.CompareTo( right.PagedAddress ) );

            string empty = new string( ' ', _logChars + _logDivWidth );
            string data = empty;

            var addresses = new HashSet<ushort>();
            foreach( var bank in _banks.Values )
                addresses.Add( bank.PagedAddress );

            var addressesToDo = new HashSet<ushort>( addresses );
            while( addressesToDo.Count > 0 )
            {
                foreach( var address in addresses )
                {
                    if( !addressesToDo.Contains( address ) )
                        continue;

                    var x = address / _logMemPerChar;
                    var text = $"+{address:X4} ";

                    if( !ReplacePart( ref data, x, text ) )
                        continue;

                    addressesToDo.Remove( address );
                }

                LogMessage( data );
                data = empty;
            }

            var banksToDo = new HashSet<Bank>();
            foreach( var pagedIn in new [] { true, false } )
            {
                foreach( var bank in _banks.Values )
                    if( bank.IsPagedIn == pagedIn )
                        banksToDo.Add( bank );

                while( banksToDo.Count > 0 )
                {
                    foreach( var bank in _banks.Values )
                    {
                        if( !banksToDo.Contains( bank ) )
                            continue;

                        var x = bank.PagedAddress / _logMemPerChar;
                        var text = $"+{bank.ID,-_logDivWidth} ";
                        
                        if( !ReplacePart( ref data, x, text ) )
                            continue;

                        banksToDo.Remove( bank );
                    }

                    LogMessage( data );
                    data = empty;
                }
            }
        }

        bool ReplacePart( ref string str, int start, string replacement )
        {
            if( !string.IsNullOrWhiteSpace( str.Substring( start, replacement.Length ) ) )
                return false;

            str = $"{str.Substring( 0, start )}{replacement}{str.Substring( start + replacement.Length )}";
            return true;
        }

    }
}
