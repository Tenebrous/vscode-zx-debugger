using System.Text;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ZXDebug
{
    public static class Format
    {
        public static string ToHex16( Value pValue )
        {
            return Convert.ToUInt16( pValue.Content ).ToHex();
        }

        public static string ToHex8( Value pValue )
        {
            return Convert.ToByte( pValue.Content ).ToHex();
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
                var updated = true;
                while( updated )
                {
                    updated = false;

                    if( !string.IsNullOrWhiteSpace( Settings.HexPrefix ) )
                        updated |= RemovePrefix( ref pValue, Settings.HexPrefix );

                    updated |= RemovePrefix( ref pValue, "&h" );
                    updated |= RemovePrefix( ref pValue, "&H" );
                    updated |= RemovePrefix( ref pValue, "0x" );
                    updated |= RemovePrefix( ref pValue, "$" );
                    updated |= RemovePrefix( ref pValue, "&" );
                    updated |= RemovePrefix( ref pValue, "h" );
                    updated |= RemovePrefix( ref pValue, "H" );

                    if( !string.IsNullOrWhiteSpace( Settings.HexSuffix ) )
                        updated |= RemoveSuffix( ref pValue, Settings.HexSuffix );

                    updated |= RemoveSuffix( ref pValue, "h" );
                    updated |= RemoveSuffix( ref pValue, "H" );

                    isHex |= updated;
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

        public static string ReplaceFirst( this string pHaystack, string pNeedle, string pNewNeedle )
        {
            var pos = pHaystack.IndexOf( pNeedle, StringComparison.OrdinalIgnoreCase );

            if( pos == -1 )
                return pHaystack;

            return pHaystack.Substring( 0, pos ) + pNewNeedle + pHaystack.Substring( pos + pNeedle.Length );
        }

        public static string ToHex( this byte pValue )
        {
            return $"{Settings.HexPrefix}{pValue:X2}{Settings.HexSuffix}";
        }

        public static string ToHex( this ushort pValue )
        {
            return $"{Settings.HexPrefix}{pValue:X4}{Settings.HexSuffix}";
        }


        public class FormatSettings
        {
            public string HexPrefix = "$";
            public string HexSuffix = "";

            [JsonConverter( typeof( StringEnumConverter ) )]
            public enum LabelPositionEnum
            {
                Left,
                Right
            }

            public LabelPositionEnum LabelPosition = LabelPositionEnum.Left;
        }
        public static FormatSettings Settings = new FormatSettings();
    }
}