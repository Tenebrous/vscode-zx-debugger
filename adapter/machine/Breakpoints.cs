using System.Collections;
using System.Collections.Generic;

namespace Spectrum
{
    public class Breakpoints : IEnumerable<Breakpoint>
    {
        Machine _machine;

        Dictionary<int, Breakpoint> _breakpoints = new Dictionary<int, Breakpoint>();

        public Breakpoints( Machine machine )
        {
            _machine = machine;
        }

        int GetFreeID()
        {
            int id = 0;

            while( _breakpoints.ContainsKey( id ) )
                id++;

            return id;
        }

        public Breakpoint Add( Machine.DisasmLine disasmLine )
        {
            if( disasmLine.Breakpoint != null )
                return disasmLine.Breakpoint;

            var bp = new Breakpoint()
            {
                Index = GetFreeID(),
                Bank = _machine.Memory.Bank( disasmLine.Bank.ID ),
                Line = disasmLine
            };

            if( _machine.Connection.SetBreakpoint( this, bp ) )
            {
                _breakpoints.Add( bp.Index, bp );
                disasmLine.Breakpoint = bp;
            }

            return bp;
        }

        public void Remove( Machine.DisasmLine disasmLine )
        {
            if( disasmLine.Breakpoint == null )
                return;

            _machine.Connection.RemoveBreakpoint( this, disasmLine.Breakpoint );

            _breakpoints.Remove( disasmLine.Breakpoint.Index );
        }

        public void Remove( Breakpoint breakpoint )
        {
            _machine.Connection.RemoveBreakpoint( this, breakpoint );

            _breakpoints.Remove( breakpoint.Index );
            
            breakpoint.Line.Breakpoint = null;
            breakpoint.Line = null;
        }

        public void Clear()
        {
            _machine.Connection.RemoveBreakpoints( this );

            foreach( var b in _breakpoints )
            {
                if( b.Value.Line != null )
                    b.Value.Line.Breakpoint = null;
            }

            _breakpoints.Clear();
        }

        public void Commit()
        {
            _machine.Connection.SetBreakpoints( this );
        }

        public IEnumerator<Breakpoint> GetEnumerator()
        {
            return _breakpoints.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}