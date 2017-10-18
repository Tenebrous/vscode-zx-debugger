using System;
using System.Text;
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

        public enum PartEnum : byte
        {
            All  = 0,
            Low  = 1,
            High = 2
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
        /// Part of memory bank, Low or High
        /// </summary>
        public readonly PartEnum Part;

        /// <summary>
        /// Create a new memory bank of the provided type & id
        /// </summary>
        /// <param name="type"></param>
        /// <param name="number"></param>
        /// <param name="part"></param>
        public BankID( TypeEnum type, int number = 0, PartEnum part = PartEnum.All )
        {
            Type = type;
            Number = number;
            Part = part;
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
            Part = PartEnum.All;

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

                if( part == "L" )
                    Part = PartEnum.Low;
                else if( part == "H" )
                    Part = PartEnum.High;
                else 
                    Part = PartEnum.All;
            }
        }

        /// <summary>
        /// Create a BankID from the textual bank type & number
        /// </summary>
        /// <param name="type"></param>
        /// <param name="number"></param>
        /// <param name="part"></param>
        public BankID( string type, int number, PartEnum part = PartEnum.All )
        {
            Type = TypeEnum.All;
            Part = part;

            if( string.Compare( type, "ROM", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.ROM;
            else if( string.Compare( type, "BANK", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.Bank;
            else if( string.Compare( type, "RAM", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.Bank;
            else if( string.Compare( type, "DIV", StringComparison.OrdinalIgnoreCase ) == 0 )
                Type = TypeEnum.Div;

            Number = number;
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
                hash = hash * 23 + Part.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==( BankID left, BankID right )
        {
            return left.Type == right.Type && left.Number == right.Number && left.Part == right.Part;
        }

        public static bool operator !=( BankID left, BankID right )
        {
            return !( left == right );
        }

        public BankID Low
        {
            get
            {
                if( Part == PartEnum.All )
                    return new BankID( Type, Number, PartEnum.Low );

                return this;
            }
        }

        public BankID High
        {
            get
            {
                if( Part == PartEnum.All )
                    return new BankID( Type, Number, PartEnum.High );

                return this;
            }
        }

        public BankID All
        {
            get
            {
                if( Part == PartEnum.Low || Part == PartEnum.High )
                    return new BankID( Type, Number, PartEnum.All );

                return this;
            }
        }

        static StringBuilder _temp = new StringBuilder();
        public override string ToString()
        {
            if( Type == TypeEnum.All )
                return "ALL";

            _temp.Clear();

            if( Type == BankID.TypeEnum.ROM )
                _temp.Append( "ROM_" );
            else if( Type == TypeEnum.Bank )
                _temp.Append( "BANK_" );
            else if( Type == TypeEnum.Div )
                _temp.Append( "DIV_" );

            if( Part == PartEnum.Low )
            {
                _temp.Append( Number * 2 );
                _temp.Append( "[" );
            }
            else if( Part == PartEnum.High )
            {
                _temp.Append( Number * 2 + 1 );
                _temp.Append( "[" );
            }

            _temp.Append( Number );

            if( Part == PartEnum.Low )
                _temp.Append( "L]" );
            else if( Part == PartEnum.High )
                _temp.Append( "H]" );

            return _temp.ToString();
        }
    }
}