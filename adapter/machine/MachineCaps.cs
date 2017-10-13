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

    public class MachineCaps : SimpleHashSet<string>
    {
        Machine _machine;
        public MachineCaps( Machine machine ) : base(StringComparer.OrdinalIgnoreCase)
        {
            _machine = machine;
        }

        public SimpleHashSet<string> Has => this;
        public SimpleHashSet<string> Is  => this;

        public void Read()
        {
            _machine.Connection.ReadMachineCaps( this );
        }

        public override string ToString()
        {
            return string.Join( ", ", this );
        }
    }
}
