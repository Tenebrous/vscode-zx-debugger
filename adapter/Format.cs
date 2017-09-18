using System.Text;
using System;

namespace ZXDebug
{
    public static class Format
    {
        public static string ToHex16( Value pValue )
        {
            uint value = Convert.ToUInt16( pValue.Content );
            return $"${value:X4} / {value}";
        }

        public static string ToHex8( Value pValue )
        {
            var value = Convert.ToByte( pValue.Content );
            return $"${value:X2} / {value}";
        }

        public static ushort Parse( string pValue )
        {
	        ushort result = 0;

	        try
	        {
	            if( pValue.StartsWith( "$" ) )
	                result = Convert.ToUInt16( pValue.Substring( 1 ), 16 );
	            else if( pValue.StartsWith( "0x" ) )
	                result = Convert.ToUInt16( pValue.Substring( 2 ), 16 );
                else if( pValue.EndsWith( "h" ) )
	                result = Convert.ToUInt16( pValue.Substring( 0, pValue.Length - 1 ), 16 );
                else
	                result = ushort.Parse( pValue );
            }
            catch( Exception e )
	        {
	            Log.Write( Log.Severity.Error, $"Can\'t parse \'{pValue}\': {e}" );
	        }

            return result;
        }

        public static ushort FromHex( string pHex )
        {
            return Convert.ToUInt16( pHex, 16 );
        }

        public static byte FromHex( char pHex )
        {
            var val = (int)char.ToUpper(pHex);
            return (byte)(val - ( val <= 57 ? 48 : 55 ));
        }

	    static StringBuilder _tempHexToBin = new StringBuilder();
        public static string HexToBin( string pHex, int pSplit )
        {
	        _tempHexToBin.Clear();

	        var count = 0;
	        for( var i = 0; i < pHex.Length; i += 2 )
	        {
	            var part = Convert.ToByte( pHex.Substring( i, 2 ), 16 );
	            var binary = Convert.ToString( part, 2 ).PadLeft( 8, '0' );


	            foreach( var ch in binary )
	            {
	                _tempHexToBin.Append( ch );

	                if( ++count % pSplit == 0 )
	                    _tempHexToBin.Append( ' ' );
	            }
            }

            return _tempHexToBin.ToString().Trim();
        }

        public static bool ApplyRule( string pRule, string pValue, ref string pResult )
        {
            var result = false;
            int count;

            if( pRule == "b" )
            {
                pResult = HexToBin( pValue, 8 );
                result = true;
            }
            else if( pRule.StartsWith( "b" ) && int.TryParse( pRule.Substring( 1 ), out count ) )
            {
                pResult = HexToBin( pValue, count );
                result = true;
            }
            else if( pRule == "n" )
            {
                pResult = HexToBin( pValue, 4 );
                result = true;
            }
            else if( pRule == "w" )
            {
                pResult = HexToBin( pValue, 2 );
                result = true;
            }
            else if( pRule == "dw" )
            {
                pResult = HexToBin( pValue, 4 );
                result = true;
            }

            return result;
        }

        public static byte[] HexToBytes( string pHex )
        {
            var count = pHex.Length / 2;
            var result = new byte[count];

            for( var i = 0; i < count; i++ )
                result[i] = Convert.ToByte( pHex.Substring( i*2, 2 ), 16 );

            return result;
        }

       
        public static string ToHex( byte[] pBytes )
        {
            return BitConverter.ToString( pBytes ).Replace( "-", "" );
        }

        public static string Encode( string pString )
        {
            return pString.Replace( "\r", "\\r" ).Replace( "\n", "\\n" );
        }
    }
}