using VSCode;

namespace ZXDebug
{
    public class HandleValueTree
    {
        Session _session;

        ValueTree _registersValues;
        ValueTree _pagingValues;
        ValueTree _settingsValues;
        
        public HandleValueTree( Session session )
        {
            _session = session;
        }

        public void Configure()
        {
            _registersValues = _session.Values.Create( "Registers" );
            SetupValues_Registers( _registersValues );

            _pagingValues = _session.Values.Create( "Paging", refresher: SetupValues_Paging );
            SetupValues_Paging( _pagingValues );

            _settingsValues = _session.Values.Create( "Settings" );
            SetupValues_Settings( _settingsValues );
        }

        void SetupValues_Registers( ValueTree val )
        {
            ValueTree reg16;

            val.Create(         "A",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "HL",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "H",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "L",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "BC",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "B",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "C",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "DE",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "D",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "E",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );


            val.Create(         "A'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "HL'", getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "H'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "L'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "BC'", getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "B'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "C'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "DE'", getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "D'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "E'",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );


            reg16 = val.Create( "IX",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "IXH", getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "IXL", getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            reg16 = val.Create( "IY",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );
                reg16.Create(   "IYH", getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
                reg16.Create(   "IYL", getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            val.Create(         "PC",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );

            val.Create(         "SP",  getter: GetReg, setter: SetReg, formatter: Convert.ToHex16 );

            val.Create(         "I",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );

            val.Create(         "R",   getter: GetReg, setter: SetReg, formatter: Convert.ToHex8  );
        }

        void SetupValues_Paging( ValueTree val )
        {
            val.ClearChildren();
            foreach( var p in _session.Machine.Memory.Slots )
            {
                var slot = val.Create( p.Min.ToHex(), delegate( ValueTree pValue ) { pValue.Content = p.Bank.ID.ToString(); } );
            }
        }

        void SetupValues_Settings( ValueTree val )
        {
        }

        void SetReg( ValueTree val, string content )
        {
            _session.Machine.Registers.Set( val.Name, content );
        }

        string GetReg( ValueTree val )
        {
            return _session.Machine.Registers[val.Name].ToString();
        }
    }
}
