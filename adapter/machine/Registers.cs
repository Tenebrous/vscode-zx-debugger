using System;
using System.Collections.Generic;
using ZXDebug;

namespace Spectrum
{
    public class Registers
    {
        public byte   A;
        public byte   F;
        public byte   B;
        public byte   C;
        public byte   D;
        public byte   E;
        public byte   H;
        public byte   L;

        public byte   AltA;
        public byte   AltF;
        public byte   AltB;
        public byte   AltC;
        public byte   AltD;
        public byte   AltE;
        public byte   AltH;
        public byte   AltL;

        public byte   IXH;
        public byte   IXL;
        public byte   IYH;
        public byte   IYL;

        public ushort PC;
        public ushort SP;

        public byte   I;
        public byte   R;

        Machine _machine;
        public Registers( Machine pMachine )
        {
            _machine = pMachine;
        }

        /// <summary>
        /// Request an update to the registers from the device
        /// </summary>
        public void Get()
        {
            _machine.Connection.RefreshRegisters( this );
        }


        /// <summary>
        /// Send a register update to the device
        /// </summary>
        /// <param name="pRegister"></param>
        /// <param name="pValue"></param>
        public void Set( string pRegister, string pValue )
        {
            _machine.Connection.SetRegister( this, pRegister, Format.Parse( pValue ) );
        }

        public bool IsValidRegister( string pRegister )
        {
            try
            {
                var x = this[pRegister];
                return true;
            }
            catch
            {
                return false;
            }
        }

        HashSet<string> _wordRegs = new HashSet<string>()
                                    {
                                        "PC", "SP", "AF", "BC", "DE", "HL",  "AF'", "BC'", "DE'", "HL'", "IX", "IY"
                                    };

        public int Size( string pRegister )
        {
            return _wordRegs.Contains( pRegister.ToUpper() ) ? 2 : 1;
        }

        /// <summary>
        /// Get/Set the buffered value of the selected register
        /// </summary>
        /// <param name="pRegister"></param>
        /// <returns></returns>
        public ushort this[string pRegister]
        {
            get
            {
                switch( pRegister.ToUpper() )
                {
                    //
                    case "A":   return (ushort) A;
                    case "F":   return (ushort) F;
                    case "AF":  return (ushort) ((A << 8) | F);

                    case "B":   return (ushort) B;
                    case "C":   return (ushort) C;
                    case "BC":  return (ushort) ((B << 8) | C);

                    case "D":   return (ushort) D;
                    case "E":   return (ushort) E;
                    case "DE":  return (ushort) ((D << 8) | E);

                    case "H":   return (ushort) H;
                    case "L":   return (ushort) L;
                    case "HL":  return (ushort) ((H << 8) | L);

                    //
                    case "A'":  return (ushort) AltA;
                    case "F'":  return (ushort) AltF;
                    case "AF'": return (ushort) ((AltA << 8) | AltF);

                    case "B'":  return (ushort) AltB;
                    case "C'":  return (ushort) AltC;
                    case "BC'": return (ushort) ((AltB << 8) | AltC);

                    case "D'":  return (ushort) AltD;
                    case "E'":  return (ushort) AltE;
                    case "DE'": return (ushort) ((AltD << 8) | AltE);

                    case "H'":  return (ushort) AltH;
                    case "L'":  return (ushort) AltL;
                    case "HL'": return (ushort) ((AltH << 8) | AltL);

                    //
                    case "IXH": return (ushort) IXH;
                    case "IXL": return (ushort) IXL;
                    case "IX":  return (ushort) ((IXH << 8) | IXL);

                    case "IYH": return (ushort) IYH;
                    case "IYL": return (ushort) IYL;
                    case "IY":  return (ushort) ((IYH << 8) | IYL);

                    //                     
                    case "PC":  return (ushort) PC;
                    case "SP":  return (ushort) SP;
                                            
                    case "I":   return (ushort) I;
                    case "R":   return (ushort) R;

                    default:
                        throw new Exception( "Unknown register '" + pRegister + "'" );
                }
            }

            set
            {
                switch( pRegister.ToUpper() )
                {
                    //
                    case "A":   A     = (byte)value;          return;

                    case "B":   B     = (byte)value;          return;
                    case "C":   C     = (byte)value;          return;
                    case "BC":  B     = (byte)(value >> 8);     
                        C     = (byte)(value & 0xFF); return;

                    case "D":   D     = (byte)value;          return;
                    case "E":   E     = (byte)value;          return;
                    case "DE":  D     = (byte)(value >> 8);     
                        E     = (byte)(value & 0xFF); return;

                    case "H":   H     = (byte)value;          return;
                    case "L":   L     = (byte)value;          return;
                    case "HL":  H     = (byte)(value >> 8);     
                        L     = (byte)(value & 0xFF); return;

                    //
                    case "A'":  AltA  = (byte)value;          return;

                    case "B'":  AltB  = (byte)value;          return;
                    case "C'":  AltC  = (byte)value;          return;
                    case "BC'": AltB  = (byte)(value >> 8);     
                        AltC  = (byte)(value & 0xFF); return;

                    case "D'":  AltD  = (byte)value;          return;
                    case "E'":  AltE  = (byte)value;          return;
                    case "DE'": AltD  = (byte)(value >> 8);     
                        AltE  = (byte)(value & 0xFF); return;

                    case "H'":  AltH  = (byte)value;          return;
                    case "L'":  AltL  = (byte)value;          return;
                    case "HL'": AltH  = (byte)(value >> 8);     
                        AltL  = (byte)(value & 0xFF); return;

                    //
                    case "IXH": IXH   = (byte)value;          return;
                    case "IXL": IXL   = (byte)value;          return;
                    case "IX":  IXH   = (byte)(value >> 8);     
                        IXL   = (byte)(value & 0xFF); return;

                    case "IYH": IYH   = (byte)value;          return;
                    case "IYL": IYL   = (byte)value;          return;
                    case "IY":  IYH   = (byte)(value >> 8);     
                        IYL   = (byte)(value & 0xFF); return;

                    //
                    case "PC":  PC    = value;                return;
                    case "SP":  SP    = value;                return;
                             
                    case "I":   I     = (byte)value;          return;
                    case "R":   R     = (byte)value;          return;

                    default:
                        throw new Exception( "Unknown register '" + pRegister + "'" );
                }
            }
        }

    }
}