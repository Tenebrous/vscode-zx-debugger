using System.Collections;
using System.Collections.Generic;

namespace Spectrum
{
    public class Breakpoints : IEnumerable<Breakpoint>
    {
        Machine _machine;

        Dictionary<int, Breakpoint> _breakpoints = new Dictionary<int, Breakpoint>();

        public Breakpoints( Machine pMachine )
        {
            _machine = pMachine;
        }

        int GetFreeID()
        {
            int id = 0;

            while( _breakpoints.ContainsKey( id ) )
                id++;

            return id;
        }

        public Breakpoint Add( Machine.DisasmLine pLine )
        {
            if( pLine.Breakpoint != null )
                return pLine.Breakpoint;

            var bp = new Breakpoint()
            {
                Index = GetFreeID(),
                Bank = _machine.Memory.Bank( pLine.Bank.ID ),
                Line = pLine
            };

            if( _machine.Connection.SetBreakpoint( this, bp ) )
            {
                _breakpoints.Add( bp.Index, bp );
                pLine.Breakpoint = bp;
            }

            return bp;
        }

        public void Remove( Machine.DisasmLine pLine )
        {
            if( pLine.Breakpoint == null )
                return;

            _machine.Connection.RemoveBreakpoint( this, pLine.Breakpoint );

            _breakpoints.Remove( pLine.Breakpoint.Index );
        }

        public void Remove( Breakpoint pBreakpoint )
        {
            _machine.Connection.RemoveBreakpoint( this, pBreakpoint );

            _breakpoints.Remove( pBreakpoint.Index );
            
            pBreakpoint.Line.Breakpoint = null;
            pBreakpoint.Line = null;
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