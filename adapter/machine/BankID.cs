using System;
using System.Text.RegularExpressions;

namespace Spectrum
{
    /// <summary>
    /// The ID of a memory bank
    /// </summary>
    public struct BankID
    {
        public enum TypeEnum
        {
            All  = 0,
            ROM  = 1,
            Bank = 2,
            Div  = 3
        }

        /// <summary>
        /// Type of memory bank
        /// </summary>
        public readonly TypeEnum Type;

        /// <summary>
        /// ID of memory bank
        /// </summary>
        public readonly int Number;

        /// <summary>
        /// Create a new memory bank of the provided type & id
        /// </summary>
        /// <param name="pType"></param>
        /// <param name="pNumber"></param>
        public BankID( TypeEnum pType, int pNumber = 0 )
        {
            Type = pType;
            Number = pNumber;
        }

        static Regex _parseBank = new Regex( @"(?'type'BANK|DIV)_(?'number'\d*)(_(?'part'L|H))?" );
        /// <summary>
        /// Create a BankID by parsing the provided string into a bank type & number
        /// </summary>
        /// <param name="pBank"></param>
        public BankID( string pBank )
        {
            Type = TypeEnum.All;
            Number = 0;

            var match = _parseBank.Match( pBank );
            if( match.Success )
            {
                Number = int.Parse( match.Groups["number"].Value );

                var type = match.Groups["type"].Value;
                var part = match.Groups["part"].Value;

                if( type == "ROM" )
                    Type = TypeEnum.ROM;
                else if( type == "BANK" )
                    Type = TypeEnum.Bank;
                else if( type == "DIV" )
                    Type = TypeEnum.Div;
            }
        }

        /// <summary>
        /// Create a BankID from the textual bank type & number
        /// </summary>
        /// <param name="pType"></param>
        /// <param name="pNumber"></param>
        public BankID( string pType, int pNumber )
        {
            Type = TypeEnum.All;

            if( string.Compare( pType, "ROM", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.ROM;
            else if( string.Compare( pType, "BANK", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.Bank;
            else if( string.Compare( pType, "DIV", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.Div;

            Number = pNumber;
        }

        public override string ToString()
        {
            if( Type == TypeEnum.ROM )
                return "ROM_" + Number;

            if( Type == TypeEnum.Bank )
                return "BANK_" + Number;

            if( Type == TypeEnum.Div )
                return "DIV_" + Number;

            return "ALL";
        }

        /// <summary>
        /// Create a BankID for the specified ROM number
        /// </summary>
        /// <param name="pID">ROM number</param>
        /// <returns></returns>
        public static BankID ROM( int pID )
        {
            return new BankID( BankID.TypeEnum.ROM, pID );
        }

        /// <summary>
        /// Create a BankID for the specified BANK number
        /// </summary>
        /// <param name="pID">BANK number</param>
        /// <returns></returns>
        public static BankID Bank( int pID )
        {
            return new BankID( BankID.TypeEnum.Bank, pID );
        }

        /// <summary>
        /// Create a BankID for unpaged memory
        /// </summary>
        /// <returns></returns>
        public static BankID Unpaged()
        {
            return new BankID( BankID.TypeEnum.All );
        }


        public override bool Equals( Object obj )
        {
            return obj is BankID && this == (BankID)obj;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Type.GetHashCode();
                hash = hash * 23 + Number.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==( BankID x, BankID y )
        {
            return x.Type == y.Type && x.Number == y.Number;
        }

        public static bool operator !=( BankID x, BankID y )
        {
            return !( x == y );
        }
    }
}