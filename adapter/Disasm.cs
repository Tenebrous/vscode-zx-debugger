using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;

namespace ZXDebug
{
    public class Disassembler
    {
        List<Opcodes> _layers = new List<Opcodes>();

        public void ClearLayers()
        {
            _layers.Clear();
        }

        public void AddLayer( string pFilename )
        {
            var layer = new Opcodes() { Filename = pFilename };
            layer.Read( pFilename );
            _layers.Add( layer );
        }

        Queue<byte> _saved = new Queue<byte>();
        public Op Get( byte[] pBytes, int pStart )
        {
            _saved.Clear();

            var tableName = "start";

            Op op = null;

            var stream = new MemoryStream( pBytes, pStart, pBytes.Length - pStart );
            int current;

            while( (current = stream.ReadByte()) > -1 )
            {
                var currentByte = (byte) current;
                string nextTableName = null;
                string result = null;

                // try and find the entry in each layer, later layers override earlier ones
                foreach( var layer in _layers )
                {
                    // does this layer contain the named table?
                    if( !layer.Tables.TryGetValue( tableName, out var table ) )
                    {
                        // no, fall through to next layer
                        continue; 
                    }

                    // does the table have the byte as a sub table?
                    if( table.SubTables.TryGetValue( currentByte, out var subTable ) )
                    {
                        // yes, save table name to move to once we've finished the layers
                        nextTableName = subTable.ID;
                        result = null;
                        continue;
                    }

                    // does the table have the byte as a final opcode?
                    if( table.Opcodes.TryGetValue( currentByte, out var opText ) && !string.IsNullOrWhiteSpace( opText ) )
                    {
                        // yes, save it
                        nextTableName = null;
                        result = opText;
                        continue;
                    }
                }

                if( nextTableName != null )
                {
                    // we've been pointed to another table, read the next
                    // byte and go to the next table

                    tableName = nextTableName;

                    if( tableName.EndsWith( "*" ) )
                    {
                        // "TABLE*" with asterisk at the end means we need save next 
                        // byte to be used after we decipher the instruction

                        var saveByte = stream.ReadByte();

                        if( saveByte == -1 )
                            throw new Exception( "Ran out of bytes" );

                        tableName = tableName.Remove( tableName.Length - 1 ).Trim();
                        _saved.Enqueue( (byte)saveByte );
                    }
                }
                else if( result != null )
                {
                    // we've got a final opcode
                    op = new Op() { Text = result };
                    break;
                }
                else
                {
                    // ?
                    throw new Exception( "Invalid opcode" );
                }
            }

            if( op == null )
                return op;


            // now process the opcode to get any immediate values

            while( true )
            {
                var pos = 0;
                
                if( ( pos = op.Text.IndexOf( "**", StringComparison.Ordinal ) ) > -1 )
                {
                    // word arguments

                    var lo = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();
                    var hi = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();

                    if( lo == -1 || hi == -1 )
                        throw new Exception( "Ran out of bytes");

                    op.Text = $"{op.Text.Substring( 0, pos )}${hi:X2}{lo:X2}{op.Text.Substring( pos + 2 )}";
                }
                else if( ( pos = op.Text.IndexOf( "+*", StringComparison.Ordinal ) ) > -1 )
                {
                    // offset arguments

                    var b = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();

                    if( b == -1 )
                        throw new Exception( "Ran out of bytes" );

                    op.Text = $"{op.Text.Substring( 0, pos )}+${b:X2}{op.Text.Substring( pos + 2 )}";
                }
                else if( ( pos = op.Text.IndexOf( "*", StringComparison.Ordinal ) ) > -1 )
                {
                    // byte argument

                    var b = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();

                    if( b == -1 )
                        throw new Exception( "Ran out of bytes" );

                    op.Text = $"{op.Text.Substring( 0, pos )}${b:X2}{op.Text.Substring( pos + 1 )}";
                }
                else
                {
                    break;
                }
            }

            op.Length = (int)stream.Position;

            return op;
        }

        public class Opcodes
        {
            public string Filename;
            public Cache<string, Table> Tables = new Cache<string, Table>( NewTable, StringComparer.OrdinalIgnoreCase );

            public void Read( string pFilename )
            {
                //   | 0 | 1 | 2 ...
                // 0 |   |   |
                // 1 |   |   |
                // 2 |   |   |
                // ..

                Table table = null;
                var lowNibble = new byte[0];

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
                                for( var i = 1; i < row.Length; i++ )
                                    lowNibble[i] = Convert.ToByte( row[i].Trim(), 16 );
                            }
                            else
                            {
                                var highNibble = Convert.ToByte( row[0].Trim(), 16 );

                                for( var i = 1; i < row.Length; i++ )
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
