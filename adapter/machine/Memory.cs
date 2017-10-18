using System.Collections.Generic;
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
            return GetSlot( address )?.Bank.ID ?? BankID.Unpaged();
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

        public void Log()
        {
            var temp = new StringBuilder();

            var sorted = new List<Bank>( _banks.Values );
            sorted.Sort(( left, right ) => left.PagedAddress.CompareTo( right.PagedAddress ));

            var slots = new Dictionary<int, Queue<string>>();
            for( var i = 0; i < 8; i++ )
            {
                var addr = (ushort)( i * 0x2000 );
                slots[i] = new Queue<string>();

                var s = GetSlot( addr );
                slots[i].Enqueue( $"{s.Min:X4}" );

                if( s.Bank.IsPagedIn )
                    slots[i].Enqueue( $"{s.Bank.ID}" );
                else
                    slots[i].Enqueue( "?" );

                foreach( var b in _banks )
                    if( !b.Value.IsPagedIn )
                        if( b.Value.PagedAddress == addr )
                            slots[i].Enqueue( $"{b.Value.ID}" );
            }

            var row = 0;
            while( slots.Count > 0 )
            {
                temp.Clear();

                if( row == 0 )
                    temp.Append( "       | " );
                else if( row == 1 )
                    temp.Append( "active | " );
                else
                    temp.Append( "       | " );

                row++;

                for( var i = 0; i < 8; i++ )
                {
                    var text = "";
                    if( slots.TryGetValue( i, out var q ) )
                    {
                        text = q.Dequeue();

                        if( q.Count == 0 )
                            slots.Remove( i );
                    }

                    temp.Append( text.PadRight( 12 ) );
                    temp.Append( " | " );
                }

                LogMessage( temp.ToString() );
            }
        }
    }
}
