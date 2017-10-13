using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ZXDebug
{
    /// <summary>
    /// Container for a set of opcode tables
    /// </summary>
    public class OpcodeTables
    {
        [JsonIgnore]
        public string Filename;

        public Dictionary<string, OpcodeTable> Tables = new Dictionary<string, OpcodeTable>( StringComparer.OrdinalIgnoreCase );

        /// <summary>
        /// Read a new set of opcode tables from the provided filename
        /// </summary>
        /// <param name="filename"></param>
        public void Read( string filename )
        {
            if( Path.GetExtension( filename ).ToLower() == ".json" )
            {
                JsonConvert.PopulateObject( File.ReadAllText( filename ), this );
                return;
            }

            OpcodeTable table = null;
            var lowNibble = new byte[0];

            using( var file = new StreamReader( filename ) )
            {
                string line;
                while( ( line = file.ReadLine() ) != null )
                {
                    if( line.Contains( ";" ) )
                        line = line.Split( ';' )[0];

                    if( string.IsNullOrWhiteSpace( line ) )
                        continue;

                    if( line.StartsWith( ">" ) )
                    {
                        var id = line.Substring( 1 ).Trim();
                        if( !Tables.TryGetValue( id, out table ) )
                        {
                            table = new OpcodeTable() { ID = id };
                            Tables[id] = table;
                        }
                    }
                    else
                    {
                        var row = line.Split( '|' );

                        if( string.IsNullOrWhiteSpace( row[0] ) )
                        {
                            // first row, list of low nibbles
                            lowNibble = new byte[row.Length];
                            for( var i = 1; i < row.Length; i++ )
                                lowNibble[i] = System.Convert.ToByte( row[i].Trim(), 16 );
                        }
                        else
                        {
                            var highNibble = System.Convert.ToByte( row[0].Trim(), 16 );

                            for( var i = 1; i < row.Length; i++ )
                            {
                                var op = (byte) ( ( highNibble << 4 ) | lowNibble[i] );

                                var text = row[i].Trim();

                                if( text.StartsWith( ">" ) )
                                    table.SubTables[op] = text.Substring( 1 );
                                else if( !string.IsNullOrWhiteSpace( text ) )
                                    table.Opcodes[op] = text;
                            }
                        }
                    }
                }
            }

            //// save as .json for later use
            //File.WriteAllText(
            //    Path.ChangeExtension( filename, "json" ),
            //    JsonConvert.SerializeObject(
            //        this,
            //        Formatting.Indented
            //    )
            //);
        }
    }
}