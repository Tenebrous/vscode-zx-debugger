using System;
using System.Text.RegularExpressions;

namespace Spectrum
{
    /// <summary>
    /// The ID of a memory bank
    /// </summary>
    public struct BankID
    {
        public enum TypeEnum : byte
        {
            All  = 0,
            ROM  = 1,
            Bank = 2,
            Div  = 3
        }

        /// <summary>
        /// Type of memory bank - All, ROM, Bank, DIV
        /// </summary>
        public readonly TypeEnum Type;

        /// <summary>
        /// ID of memory bank
        /// </summary>
        public readonly int Number;

        /// <summary>
        /// Create a new memory bank of the provided type & id
        /// </summary>
        /// <param name="type"></param>
        /// <param name="number"></param>
        public BankID( TypeEnum type, int number = 0 )
        {
            Type = type;
            Number = number;
        }

        static Regex _parseBank = new Regex( @"(?'type'BANK|DIV)_(?'number'\d*)(_(?'part'L|H))?" );
        /// <summary>
        /// Create a BankID by parsing the provided string into a bank type & number
        /// </summary>
        /// <param name="bank"></param>
        public BankID( string bank )
        {
            Type = TypeEnum.All;
            Number = 0;

            var match = _parseBank.Match( bank );
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
        /// <param name="type"></param>
        /// <param name="number"></param>
        public BankID( string type, int number )
        {
            Type = TypeEnum.All;

            if( string.Compare( type, "ROM", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.ROM;
            else if( string.Compare( type, "BANK", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.Bank;
            else if( string.Compare( type, "DIV", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.Div;

            Number = number;
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
        /// <param name="number">ROM number</param>
        /// <returns></returns>
        public static BankID ROM( int number )
        {
            return new BankID( BankID.TypeEnum.ROM, number );
        }

        /// <summary>
        /// Create a BankID for the specified BANK number
        /// </summary>
        /// <param name="number">BANK number</param>
        /// <returns></returns>
        public static BankID Bank( int number )
        {
            return new BankID( BankID.TypeEnum.Bank, number );
        }

        /// <summary>
        /// Create a BankID for unpaged memory
        /// </summary>
        /// <returns></returns>
        public static BankID Unpaged()
        {
            return new BankID( BankID.TypeEnum.All );
        }


        public override bool Equals( object other )
        {
            return other is BankID && this == (BankID)other;
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

        public static bool operator ==( BankID left, BankID right )
        {
            return left.Type == right.Type && left.Number == right.Number;
        }

        public static bool operator !=( BankID left, BankID right )
        {
            return !( left == right );
        }
    }
}