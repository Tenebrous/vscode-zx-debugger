using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace ZXDebug
{
    /// <summary>
    /// Class to deal with disassembling z80 from bytes
    /// </summary>
    public class Disassembler
    {
        /// <summary>
        /// Each layer is a definition of z80 opcodes and is checked from first to last.
        /// This allows us to use a base z80 definition file then layer on others, e.g. for the Next
        /// </summary>
        List<OpcodeTables> _layers = new List<OpcodeTables>();

        /// <summary>
        /// Remove currently-loaded layer files
        /// </summary>
        public void ClearLayers()
        {
            _layers.Clear();
        }

        /// <summary>
        /// Load a new layer file and add to the end of the list
        /// </summary>
        /// <param name="pFilename"></param>
        public void AddLayer( string pFilename )
        {
            var layer = new OpcodeTables { Filename = pFilename };
            layer.Read( pFilename );
            _layers.Add( layer );
        }

        Queue<byte> _saved = new Queue<byte>();

        /// <summary>
        /// Disassemble one instruction from the provided bytes starting from the indicated position
        /// </summary>
        /// <param name="pBytes">Bytes to be disassembled</param>
        /// <param name="pStart">Starting position</param>
        /// <returns>A new Op representing the instruction, or null if not valid</returns>
        public Instruction Get( byte[] pBytes, int pStart )
        {
            _saved.Clear();

            var tableName = "start";

            Instruction instruction = null;
            var stream = new MemoryStream( pBytes, pStart, pBytes.Length - pStart );
            int current;

            while( ( current = stream.ReadByte() ) > -1 )
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
                        nextTableName = subTable;
                        result = null;
                        continue;
                    }

                    // does the table have the byte as a final opcode?
                    if( table.Opcodes.TryGetValue( currentByte, out var opText ) && !string.IsNullOrWhiteSpace( opText ) )
                    {
                        // yes, save it
                        nextTableName = null;
                        result = opText;
                    }
                }

                if( nextTableName != null )
                {
                    // we've been pointed to another table, read the next
                    // byte and go to the next table

                    tableName = nextTableName;

                    if( tableName.EndsWith( "{b}" ) )
                    {
                        // "TABLE*" with asterisk at the end means we need save next 
                        // byte to be used after we decipher the instruction

                        var saveByte = stream.ReadByte();

                        if( saveByte == -1 )
                            throw new Exception( "Ran out of bytes" );

                        tableName = tableName.Remove( tableName.Length - 3 ).Trim();
                        _saved.Enqueue( (byte) saveByte );
                    }
                }
                else if( result != null )
                {
                    // we've got a final opcode
                    instruction = new Instruction { Text = result };
                    break;
                }
                else
                {
                    // ?
                    throw new Exception( "Invalid opcode" );
                }
            }

            if( instruction == null )
                return instruction;


            // now process the opcode to get any immediate values

            var pos = 0;

            while( pos < instruction.Text.Length )
            {
                var foundPos = 0;

                if( ( foundPos = instruction.Text.IndexOf( "{b}", pos, StringComparison.Ordinal ) ) > -1 )
                {
                    // byte argument

                    var b = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();

                    if( b == -1 )
                        throw new Exception( "Ran out of bytes" );

                    instruction.OperandType = Instruction.TypeEnum.Imm8;
                    instruction.Operand = (ushort)b;

                    pos = foundPos + 3;

                    //instruction.Text = $"{instruction.Text.Substring( 0, pos )}${b:X2}{instruction.Text.Substring( pos + 3 )}";
                }
                else if( ( foundPos = instruction.Text.IndexOf( "{w}", pos, StringComparison.Ordinal ) ) > -1 )
                {
                    // word argument

                    var lo = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();
                    var hi = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();

                    if( lo == -1 || hi == -1 )
                        throw new Exception( "Ran out of bytes" );

                    instruction.OperandType = Instruction.TypeEnum.Imm16;
                    instruction.Operand = (ushort)( hi << 8 | lo );

                    pos = foundPos + 3;

                    //instruction.Text = $"{instruction.Text.Substring( 0, pos )}${instruction.Operand:X4}{instruction.Text.Substring( pos + 3 )}";
                }
                else if( ( foundPos = instruction.Text.IndexOf( "{code}", pos, StringComparison.Ordinal ) ) > -1 )
                {
                    // code address

                    var lo = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();
                    var hi = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();

                    if( lo == -1 || hi == -1 )
                        throw new Exception( "Ran out of bytes" );

                    instruction.OperandType = Instruction.TypeEnum.CodeAddr;
                    instruction.Operand = (ushort)( hi << 8 | lo );

                    pos = foundPos + 6;

                    //instruction.Text = $"{instruction.Text.Substring( 0, pos )}${instruction.Operand:X4}{instruction.Text.Substring( pos + 6 )}";
                }
                else if( ( foundPos = instruction.Text.IndexOf( "{data}", pos, StringComparison.Ordinal ) ) > -1 )
                {
                    // data address

                    var lo = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();
                    var hi = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();

                    if( lo == -1 || hi == -1 )
                        throw new Exception( "Ran out of bytes" );

                    instruction.OperandType = Instruction.TypeEnum.DataAddr;
                    instruction.Operand = (ushort)( hi << 8 | lo );

                    pos = foundPos + 6;

                    //instruction.Text = $"{instruction.Text.Substring( 0, pos )}${instruction.Operand:X4}{instruction.Text.Substring( pos + 6 )}";
                }
                else if( ( foundPos = instruction.Text.IndexOf( "{+b}", pos, StringComparison.Ordinal ) ) > -1 )
                {
                    // offset arguments

                    var b = _saved.Count > 0 ? _saved.Dequeue() : stream.ReadByte();

                    if( b == -1 )
                        throw new Exception( "Ran out of bytes" );

                    instruction.OperandType = Instruction.TypeEnum.Rel8;
                    instruction.Operand = (ushort)b;

                    pos = foundPos + 4;

                    //instruction.Text = $"{instruction.Text.Substring( 0, pos )}+${b:X2}{instruction.Text.Substring( pos + 4 )}";
                }
                else
                {
                    break;
                }
            }

            instruction.Length = (int) stream.Position;
            instruction.Bytes = pBytes.Extract( pStart, instruction.Length );

            return instruction;
        }

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
            /// <param name="pFilename"></param>
            public void Read( string pFilename )
            {
                if( Path.GetExtension( pFilename ).ToLower() == ".json" )
                { 
                    JsonConvert.PopulateObject( File.ReadAllText(pFilename), this );
                    return;
                }

                OpcodeTable table = null;
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
                                    lowNibble[i] = Convert.ToByte( row[i].Trim(), 16 );
                            }
                            else
                            {
                                var highNibble = Convert.ToByte( row[0].Trim(), 16 );

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

                // save as .json for later use
                File.WriteAllText( 
                    Path.ChangeExtension( pFilename, "json" ), 
                    JsonConvert.SerializeObject( 
                        this, 
                        Formatting.Indented
                    ) 
                );
            }
        }

        public class OpcodeTable
        {
            [JsonIgnore]
            public string ID;

            public Dictionary<byte, string> SubTables = new Dictionary<byte, string>();
            public Dictionary<byte, string> Opcodes = new Dictionary<byte, string>();
        }

        public class Instruction
        {
            public enum TypeEnum : byte
            {
                Imm8,
                Imm16,
                Rel8,
                DataAddr,
                CodeAddr
            }

            public int    Length;
            public byte[] Bytes;
            public string Text;

            public TypeEnum OperandType;
            public ushort Operand;
        }
    }
}