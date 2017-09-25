using System;
using System.Text.RegularExpressions;

namespace Spectrum
{
    public struct BankID
    {
        public enum TypeEnum
        {
            All  = 0,
            ROM  = 1,
            Bank = 2,
            Div  = 3
        }

        public readonly TypeEnum Type;
        public readonly int Number;

        public BankID( TypeEnum pType, int pNumber = 0 )
        {
            Type = pType;
            Number = pNumber;
        }

        static Regex _parseBank = new Regex( @"(?'type'BANK|DIV)_(?'number'\d*)(_(?'part'L|H))?" );
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

        //public static implicit operator int(BankID pValue)
        //{
        //    if( pValue.Type == BankType.ROM )
        //        return -2 - pValue.Number;

        //    if( pValue.Type == BankType.All )
        //        return -1;

        //    return pValue.Number;
        //}

        //public static implicit operator BankID( int pValue )
        //{
        //    if( pValue < -1 )
        //        return new BankID() { Type = BankType.ROM, Number = -2 - pValue };

        //    if( pValue == -1 )
        //        return new BankID() { Type = BankType.All };

        //    return new BankID() { Type = BankType.Bank, Number = pValue };
        //}

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

        public static BankID ROM( int pID )
        {
            return new BankID( BankID.TypeEnum.ROM, pID );
        }

        public static BankID Bank( int pID )
        {
            return new BankID( BankID.TypeEnum.Bank, pID );
        }

        public static BankID Unpaged()
        {
            return new BankID( BankID.TypeEnum.All );
        }
    }
}