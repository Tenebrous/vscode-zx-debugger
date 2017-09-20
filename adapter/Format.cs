using System.Text;
using System;

namespace ZXDebug
{
    public static class Format
    {
        public static string HexPrefix = "$";
        public static string HexSuffix = "";

        public static string ToHex16( Value pValue )
        {
            return ToHex16( Convert.ToUInt16( pValue.Content ) );
        }

        public static string ToHex8( Value pValue )
        {
            return ToHex8( Convert.ToUInt16( pValue.Content ) );
        }

        public static string ToHex16( ushort pValue )
        {
            return $"{HexPrefix}{pValue:X4}{HexSuffix}";
        }

        public static string ToHex8( ushort pValue )
        {
            return $"{HexPrefix}{pValue:X2}{HexSuffix}";
        }

        public static string ToHex( ushort pValue, int pBytes )
        {
            var format = $"{{0}}{{1:X{pBytes * 2}}}{{2}}";
            return string.Format( format, HexPrefix, pValue, HexSuffix );
        }

        public static string ToHex( byte[] pBytes )
        {
            return BitConverter.ToString( pBytes ).Replace( "-", "" );
        }

        public static string ToHex( byte[] pBytes, int pLength )
        {
            return BitConverter.ToString( pBytes, 0, pLength ).Replace( "-", "" );
        }

        static bool RemovePrefix( ref string pValue, string pPrefix )
        {
            if( !pValue.StartsWith( pPrefix ) )
                return false;

            pValue = pValue.Substring( pPrefix.Length );
            return true;
        }

        static bool RemoveSuffix( ref string pValue, string pSuffix )
        {
            if( !pValue.EndsWith( pSuffix ) )
                return false;

            pValue = pValue.Substring( 0, pValue.Length - pSuffix.Length );
            return true;
        }

        public static ushort Parse( string pValue, bool pKnownHex = false )
        {
	        ushort result = 0;
            var isHex = pKnownHex;

            try
            {
                if( !isHex )
                {
                    var updated = true;
                    while( updated )
                    {
                        updated = false;

                        if( !string.IsNullOrWhiteSpace( HexPrefix ) )
                            updated |= RemovePrefix( ref pValue, HexPrefix );

                        updated |= RemovePrefix( ref pValue, "&h" );
                        updated |= RemovePrefix( ref pValue, "&H" );
                        updated |= RemovePrefix( ref pValue, "0x" );
                        updated |= RemovePrefix( ref pValue, "$" );
                        updated |= RemovePrefix( ref pValue, "&" );
                        updated |= RemovePrefix( ref pValue, "h" );
                        updated |= RemovePrefix( ref pValue, "H" );

                        if( !string.IsNullOrWhiteSpace( HexSuffix ) )
                            updated |= RemoveSuffix( ref pValue, HexSuffix );

                        updated |= RemoveSuffix( ref pValue, "h" );
                        updated |= RemoveSuffix( ref pValue, "H" );

                        isHex |= updated;
                    }
                }

                result =  isHex ? Convert.ToUInt16( pValue, 16 ) : ushort.Parse( pValue );
            }
            catch( Exception e )
	        {
	            Log.Write( Log.Severity.Error, $"Can\'t parse \'{pValue}\': {e}" );
	        }

            return result;
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

        public static byte[] HexToBytes( string pHex )
        {
            var count = pHex.Length / 2;
            var result = new byte[count];

            for( var i = 0; i < count; i++ )
                result[i] = Convert.ToByte( pHex.Substring( i*2, 2 ), 16 );

            return result;
        }

        public static string Encode( string pString )
        {
            return pString.Replace( "\r", "\\r" ).Replace( "\n", "\\n" );
        }

        public static T[] Extract<T>( this T[] pData, int pIndex, int pLength )
        {
            var result = new T[pLength];
            Array.Copy( pData, pIndex, result, 0, pLength );
            return result;
        }
    }
}