using System.Collections.Generic;
using System.Text;

namespace Spectrum
{
    public class Memory
    {
        public bool   PagingEnabled;
        public ushort SlotSize = 0x4000;

        Dictionary<int, Slot> _slots = new Dictionary<int, Slot>();
        Dictionary<BankID, Bank> _banks = new Dictionary<BankID, Bank>();

        public List<Slot> Slots { get; } = new List<Slot>();

        Machine _machine;
        public Memory( Machine pMachine )
        {
            _machine = pMachine;
        }

        public Slot GetSlot( ushort pAddress )
        {
            int slotIndex;
            ushort slotAddress;
            int slotSize;

            if( PagingEnabled )
            {
                slotSize = SlotSize;
                slotIndex = (int) ( pAddress / SlotSize );
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

        public void ClearMemoryMap()
        {
            //_slots.Clear();
            //_banks.Clear();
        }

        public void SetAddressBank( ushort pMin, ushort pMax, Bank pBank )
        {
            GetSlot( pMin ).Bank = pBank;
            _banks[pBank.ID] = pBank;
        }

        public Bank Bank( BankID pID )
        {
            if( !_banks.TryGetValue( pID, out var result ) )
                result = new Bank() { ID = pID };

            return result;
        }

        public int Get( ushort pAddress, int pLength, byte[] pBuffer )
        {
            return _machine.Connection.ReadMemory( pAddress, pBuffer, pLength );
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

                _tempToString.Append( kvp.Key + ":" + kvp.Value.Bank.ID );
            }

            return _tempToString.ToString();
        }
    }
}