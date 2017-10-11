using System.Text;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ZXDebug
{
    public static class Convert
    {
        public static string ToHex16( ValueTree value )
        {
            return System.Convert.ToUInt16( value.Content ).ToHex();
        }

        public static string ToHex8( ValueTree value )
        {
            return System.Convert.ToByte( value.Content ).ToHex();
        }

        public static string ToHex( byte[] bytes )
        {
            return BitConverter.ToString( bytes ).Replace( "-", "" );
        }

        public static string ToHex( byte[] bytes, int length )
        {
            return BitConverter.ToString( bytes, 0, length ).Replace( "-", "" );
        }

        static bool RemovePrefix( ref string value, string prefix )
        {
            if( !value.StartsWith( prefix ) )
                return false;

            value = value.Substring( prefix.Length );
            return true;
        }

        static bool RemoveSuffix( ref string value, string suffix )
        {
            if( !value.EndsWith( suffix ) )
                return false;

            value = value.Substring( 0, value.Length - suffix.Length );
            return true;
        }

        public static ushort Parse( string value, bool isHex = false )
        {
            ushort result = 0;

            try
            {
                var updated = true;
                while( updated )
                {
                    updated = false;

                    if( !string.IsNullOrWhiteSpace( Settings.HexPrefix ) )
                        updated |= RemovePrefix( ref value, Settings.HexPrefix );

                    updated |= RemovePrefix( ref value, "&h" );
                    updated |= RemovePrefix( ref value, "&H" );
                    updated |= RemovePrefix( ref value, "0x" );
                    updated |= RemovePrefix( ref value, "$" );
                    updated |= RemovePrefix( ref value, "&" );
                    updated |= RemovePrefix( ref value, "h" );
                    updated |= RemovePrefix( ref value, "H" );

                    if( !string.IsNullOrWhiteSpace( Settings.HexSuffix ) )
                        updated |= RemoveSuffix( ref value, Settings.HexSuffix );

                    updated |= RemoveSuffix( ref value, "h" );
                    updated |= RemoveSuffix( ref value, "H" );

                    isHex |= updated;
                }

                result =  isHex ? System.Convert.ToUInt16( value, 16 ) : ushort.Parse( value );
            }
            catch( Exception e )
            {
                Logging.Write( Logging.Severity.Error, $"Can\'t parse \'{value}\': {e}" );
            }

            return result;
        }

        public static byte FromHex( char hex )
        {
            var val = (int)char.ToUpper(hex);
            return (byte)(val - ( val <= 57 ? 48 : 55 ));
        }

        static StringBuilder _tempHexToBin = new StringBuilder();
        public static string HexToBin( string hex, int groupSize )
        {
            _tempHexToBin.Clear();

            var count = 0;
            for( var i = 0; i < hex.Length; i += 2 )
            {
                var part = System.Convert.ToByte( hex.Substring( i, 2 ), 16 );
                var binary = System.Convert.ToString( part, 2 ).PadLeft( 8, '0' );


                foreach( var ch in binary )
                {
                    _tempHexToBin.Append( ch );

                    if( ++count % groupSize == 0 )
                        _tempHexToBin.Append( ' ' );
                }
            }

            return _tempHexToBin.ToString().Trim();
        }

        public static byte[] HexToBytes( string hex )
        {
            var count = hex.Length / 2;
            var result = new byte[count];

            for( var i = 0; i < count; i++ )
                result[i] = System.Convert.ToByte( hex.Substring( i*2, 2 ), 16 );

            return result;
        }

        public static string Encode( string str )
        {
            return str.Replace( "\r", "\\r" ).Replace( "\n", "\\n" );
        }

        public static T[] Extract<T>( this T[] data, int start, int length )
        {
            var result = new T[length];
            Array.Copy( data, start, result, 0, length );
            return result;
        }

        public static string ReplaceFirst( this string haystack, string needle, string replacement )
        {
            var pos = haystack.IndexOf( needle, StringComparison.OrdinalIgnoreCase );

            if( pos == -1 )
                return haystack;

            return haystack.Substring( 0, pos ) + replacement + haystack.Substring( pos + needle.Length );
        }

        public static string ToHex( this byte value )
        {
            return $"{Settings.HexPrefix}{value:X2}{Settings.HexSuffix}";
        }
        public static string ToBin( this byte value )
        {
            return $"{Settings.BinPrefix}{System.Convert.ToString( value, 2 ).PadLeft( 8, '0' )}{Settings.BinSuffix}";
        }

        public static string ToHex( this ushort value )
        {
            return $"{Settings.HexPrefix}{value:X4}{Settings.HexSuffix}";
        }


        public class FormatSettings
        {
            public string HexPrefix = "$";
            public string HexSuffix = "";

            public string BinPrefix = "";
            public string BinSuffix = "";

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