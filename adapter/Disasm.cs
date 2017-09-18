using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ZXDebug
{
    public class Disassembler
    {
        List<Opcodes> _layers = new List<Opcodes>();

        public void AddFile( string pFilename )
        {
            var layer = new Opcodes();
            layer.Read( pFilename );
            _layers.Add( layer );
        }

        public Op Get( byte[] pBytes, int pStart )
        {
            string tableName = "start";

            int index = pStart;

            while( index < pBytes.Length && tableName != null )
            {
                string nextTableName = null;
                string result = null;

                foreach( var layer in _layers )
                {
                    if( !layer.Tables.TryGetValue( tableName, out var table ) )
                        continue;

                    if( table.SubTables.TryGetValue( pBytes[index], out var subTable ) )
                    {
                        nextTableName = subTable.ID;
                        result = null;
                    }
                    else if( table.Opcodes.TryGetValue( pBytes[index], out var opText ))
                    {
                        nextTableName = null;
                        result = opText;
                    }
                }

                if( nextTableName != null )
                {
                    index++;
                    tableName = nextTableName;
                }
                else if( result != null )
                {
                    return new Op() { Length = index - pStart + 1, Text = result };
                }
                else
                {
                    throw new Exception( "Invalid opcode" );
                }
            }

            return null;
        }

        public class Opcodes
        {
            public Cache<string, Table> Tables = new Cache<string, Table>( NewTable, StringComparer.OrdinalIgnoreCase );

            public void Read( string pFilename )
            {
                //   | 0 | 1 | 2 ...
                // 0 |   |   |
                // 1 |   |   |
                // 2 |   |   |
                // ..

                Table table = null;
                byte[] lowNibble = new byte[0];

                using( var file = new StreamReader( pFilename ) )
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
                            table = Tables[line.Substring( 1 ).Trim()];
                        }
                        else
                        {
                            var row = line.Split( '|' );

                            if( string.IsNullOrWhiteSpace( row[0] ) )
                            {
                                // first row, list of low nibbles
                                lowNibble = new byte[row.Length];
                                for( int i = 1; i < row.Length; i++ )
                                    lowNibble[i] = Convert.ToByte( row[i].Trim(), 16 );
                            }
                            else
                            {
                                var highNibble = Convert.ToByte( row[0].Trim(), 16 );

                                for( int i = 1; i < row.Length; i++ )
                                {
                                    var op = (byte)( ( highNibble << 4 ) | lowNibble[i] );

                                    var text = row[i].Trim();

                                    if( text.StartsWith( ">" ) )
                                        table.SubTables[op] = Tables[text.Substring( 1 )];
                                    else
                                        table.Opcodes[op] = text;
                                }
                            }
                        }
                    }
                }
            }

            static Table NewTable( string pID )
            {
                return new Table() { ID = pID };
            }
        }

        public class Table
        {
            public string ID;
            public Dictionary<byte, Table> SubTables = new Dictionary<byte, Table>();
            public Dictionary<byte, string> Opcodes = new Dictionary<byte, string>();
        }

        public class Op
        {
            public string Text;
            public int Length;
        }
    }
}
