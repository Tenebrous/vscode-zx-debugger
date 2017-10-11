using System;
using System.Collections.Generic;

namespace Spectrum
{
    public class SimpleHashSet<T> : HashSet<T>
    {
        public SimpleHashSet() : base()
        {
        }

        public SimpleHashSet(IEqualityComparer<T> comparer) : base( comparer )
        {
        }

        public bool this[ T key ]
        {
            get { return this.Contains( key ); }
            set
            {
                if( value )
                    this.Add( key );
                else
                    this.Remove( key );
            }
        }
    }

    public class MachineCaps
    {
        Machine _machine;
        public MachineCaps( Machine machine )
        {
            _machine = machine;
        }

        SimpleHashSet<string> _caps = new SimpleHashSet<string>(StringComparer.OrdinalIgnoreCase);

        public SimpleHashSet<string> Has => _caps;
        public SimpleHashSet<string> Is  => _caps;

        public void Clear()
        {
            _caps.Clear();
        }

        public void Read()
        {
            _machine.Connection.ReadMachineCaps( this );
        }

        public override string ToString()
        {
            return string.Join( ", ", _caps );
        }
    }
}
