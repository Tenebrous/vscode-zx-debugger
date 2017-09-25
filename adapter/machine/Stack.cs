using System.Collections.Generic;

namespace Spectrum
{
    public class Stack : List<ushort>
    {
        Machine _machine;
        public Stack( Machine pMachine )
        {
            _machine = pMachine;
        }

        public void Get()
        {
            _machine.Connection.RefreshStack( this );
        }
    }
}