using System.Collections.Generic;
using ZEsarUXDebugger;

namespace Z80Machine
{
    public class Machine
    {
        // the class used to actually retrieve the data can be abstracted out at some point
        // but for now we'll tie directly to the ZEsarUX connection class
        public ZEsarUXConnection Connection;

        public Machine( ZEsarUXConnection pConnection )
        {
            Connection = pConnection;
        }

        public Registers RefreshRegisters()
        {
            return _registers = Connection.GetRegisters();
        }

        Registers _registers;
        public Registers Registers
        {
            get { return _registers; }
        }
    }

    public class Registers
    {
        public ushort PC;
        public ushort SP;

        public byte   A;
        public ushort BC;
        public ushort DE;
        public ushort HL;

        public byte   AltA;
        public ushort AltBC;
        public ushort AltDE;
        public ushort AltHL;

        public ushort IX;
        public ushort IY;

        public byte   I;
        public byte   R;
    }


    public class Memory
    {
        public class Map
        {
            public ushort Min;
            public ushort Max;

            // 0, 1, 2 etc = bank #
            // -1 = not specified
            // -2 = rom 1
            // -3 = rom 2
            public int Bank;
        }

        public bool PagingEnabled;

        List<Map> _pages = new List<Map>();
        public int GetMapForAddress( ushort pAddress )
        {
            foreach( var page in _pages )
                if( pAddress >= page.Min && pAddress <= page.Max )
                    return page.Bank;

            return 0;
        }

        public void ClearMap()
        {
            _pages.Clear();
        }

        public void AddMap( ushort pMin, ushort pMax, int pBank )
        {
            _pages.Add( new Map() { Min = pMin, Max = pMax, Bank = pBank } );
        }
    }
}