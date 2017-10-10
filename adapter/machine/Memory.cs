using System;
using System.Collections.Generic;
using System.Text;
using ZXDebug;

namespace Spectrum
{
    /// <summary>
    /// Represents the current state of the device's memory 
    /// </summary>
    public class Memory
    {
        public bool   PagingEnabled;
        public ushort SlotSize = 0x4000;

        Dictionary<int, Slot> _slots = new Dictionary<int, Slot>();
        Cache<BankID, Bank> _banks = new Cache<BankID, Bank>( pID => new Bank() { ID = pID } );

        public List<Slot> Slots { get; } = new List<Slot>();

        Machine _machine;
        public Memory( Machine machine )
        {
            _machine = machine;
        }

        public Slot GetSlot( ushort address )
        {
            int slotIndex;
            ushort slotAddress;
            int slotSize;

            if( PagingEnabled )
            {
                slotSize = SlotSize;
                slotIndex = address / SlotSize;
                slotAddress = (ushort) ( slotIndex * SlotSize );
            }
            else
            {
                slotSize = 0x10000;
                slotIndex = -1;
                slotAddress = 0;
            }

            if( _slots.TryGetValue( slotIndex, out var slot ) )
                return slot;

            slot = new Slot() { ID = slotIndex, Min = slotAddress, Max = (ushort) ( slotAddress + slotSize - 1 ) };
            _slots[slotIndex] = slot;

            Slots.Add( slot );
            Slots.Sort( ( pLeft, pRight ) => pLeft.Min.CompareTo( pRight.Min ) );

            return slot;
        }

        public BankID GetMappedBank( ushort address )
        {
            return GetSlot( address )?.Bank.ID ?? BankID.Unpaged();
        }

        public void ClearMemoryMap()
        {
            //_slots.Clear();
            //_banks.Clear();
        }

        public void SetAddressBank( ushort min, ushort max, Bank bank )
        {
            GetSlot( min ).Bank = bank;
            bank.LastAddress = min;
        }

        public Bank Bank( BankID pID )
        {
            return _banks[pID];
        }

        public int Get( ushort address, byte[] bytes, int start = 0, int length = 0)
        {
            if( length == 0 )
                length = bytes.Length - start;

            return _machine.Connection.ReadMemory( address, bytes, start, length );
        }

        public void GetMapping()
        {
            _machine.Connection.RefreshMemoryPages( this );
        }

        StringBuilder _tempToString = new StringBuilder();
        public override string ToString()
        {
            _tempToString.Clear();
            foreach( var kvp in _slots )
            {
                if( _tempToString.Length > 0 )
                    _tempToString.Append( ' ' );

                if( kvp.Value.Bank != null )
                    _tempToString.Append( kvp.Key + ":" + kvp.Value.Bank?.ID );
                else
                    _tempToString.Append( kvp.Key + ":?" );
            }

            return _tempToString.ToString();
        }
    }
}