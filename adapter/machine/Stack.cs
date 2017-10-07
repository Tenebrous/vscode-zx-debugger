using System.Collections.Generic;

namespace Spectrum
{
    public class Stack : List<ushort>
    {
        Machine _machine;
        public Stack( Machine machine )
        {
            _machine = machine;
        }
    }
}