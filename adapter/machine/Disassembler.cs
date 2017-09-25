using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ZXDebug
{
    /// <summary>
    /// Class to deal with disassembling z80 from bytes
    /// </summary>
    public class Disassembler
    {
        public DisassemblerSettings Settings = new DisassemblerSettings();

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

        Queue<byte> _tempByteQueue = new Queue<byte>();
        Operand[] _tempOperands = new Operand[2];

        /// <summary>
        /// Disassemble one instruction from the provided bytes starting from the indicated position
        /// </summary>
        /// <param name="pBytes">Bytes to be disassembled</param>
        /// <param name="pStart">Starting position</param>
        /// <returns>A new Op representing the instruction, or null if not valid</returns>
        public Instruction Disassemble( byte[] pBytes, int pStart )
        {

            var stream = new MemoryStream( pBytes, pStart, pBytes.Length - pStart );

            // decipher instruction

            var instruction = GetInstruction( stream );

            if( instruction == null )
                return null;

            // parse the opcode text to get any operands

            GetOperands( instruction, stream );

            instruction.Length = (int)stream.Position;
            instruction.Bytes = pBytes.Extract( pStart, instruction.Length );

            return instruction;
        }

        Stack<byte> _tempOpcodeList = new Stack<byte>();
        Instruction GetInstruction( MemoryStream pStream )
        {
            _tempByteQueue.Clear();
            var tableName = "start";
            Instruction instruction = null;
            int current;

            _tempOpcodeList.Clear();

            while( ( current = pStream.ReadByte() ) > -1 )
            {
                var currentByte = (byte) current;
                _tempOpcodeList.Push(currentByte);

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

                    // does the table entry indicate this goes to a sub table?
                    if( table.SubTables.TryGetValue( currentByte, out var subTable ) )
                    {
                        // yes, save table name to move to once we've finished the layers
                        nextTableName = subTable;
                        result = null;
                        continue;
                    }

                    // does the table entry indicate this is the final opcode?
                    if( table.Opcodes.TryGetValue( currentByte, out var opText ) && !string.IsNullOrWhiteSpace( opText ) )
                    {
                        // yes, save it
                        nextTableName = null;
                        result = opText;
                    }
                }

                if( nextTableName != null )
                {
                    // we've been pointed to another table
                    tableName = nextTableName;

                    if( tableName.EndsWith( "{b}" ) )
                    {
                        // e.g. ">IYBITS {b}"
                        //
                        // in this case, we need to grab a data byte first, and /then/
                        // a byte which tells us which opcode we're using
                        //
                        // the data byte we save will be used later

                        var saveByte = pStream.ReadByte();

                        if( saveByte == -1 )
                        {
                            Log.Write( Log.Severity.Error, "Ran out of bytes decoding {b} in table '" + tableName + "'" );
                            return null;
                        }

                        tableName = tableName.Remove( tableName.Length - 3 ).Trim();
                        _tempByteQueue.Enqueue( (byte) saveByte );
                    }
                }
                else if( result != null )
                {
                    if( result == "*" )
                    {
                        // prefix change results in restarting decoding and treating 
                        // anything prior as db

                        byte db = 0;
                        if( _tempOpcodeList.Count > 0 ) db = _tempOpcodeList.Pop();
                        if( _tempOpcodeList.Count > 0 ) db = _tempOpcodeList.Pop();

                        instruction = new Instruction { Text = "db " + db.ToHex() };
                        pStream.Position = pStream.Position - 1;

                        break;
                    }
                    else
                    {
                        // we've got a final opcode
                        instruction = new Instruction { Text = result };
                        break;
                    }
                }
                else
                {
                    // ?
                    throw new Exception( "Invalid opcode" );
                }
            }

            return instruction;
        }

        void GetOperands( Instruction pInstruction, MemoryStream pStream )
        {
            var operands = 0;

            var start = pInstruction.Text.IndexOf( '{' );
            while( start > -1 )
            {
                var end = pInstruction.Text.IndexOf( '}', start );

                if( end == -1 )
                    break;

                var specifier = pInstruction.Text.Substring( start + 1, end - start - 1 );
                var type = Operand.TypeEnum.Unknown;
                var length = 0;
                var lo = 0;
                var hi = 0;

                switch( specifier )
                {
                    case "b":    type = Operand.TypeEnum.Imm8;     length = 1; break;
                    case "+b":   type = Operand.TypeEnum.CodeRel;  length = 1; break;
                    case "+i":   type = Operand.TypeEnum.Index;    length = 1; break;
                    case "w":    type = Operand.TypeEnum.Imm16;    length = 2; break;
                    case "code": type = Operand.TypeEnum.CodeAddr; length = 2; break;
                    case "data": type = Operand.TypeEnum.DataAddr; length = 2; break;
                }

                if( length > 0 )
                    lo = _tempByteQueue.Count > 0 ? _tempByteQueue.Dequeue() : pStream.ReadByte();

                if( length > 1 )
                    hi = _tempByteQueue.Count > 0 ? _tempByteQueue.Dequeue() : pStream.ReadByte();

                if( lo == -1 || hi == -1 )
                {
                    Log.Write( Log.Severity.Error, "Ran out of bytes decoding {" + specifier + "} in instruction '" + pInstruction.Text + "'" );
                    return;
                }

                _tempOperands[operands++] = new Operand( type, hi << 8 | lo );

                start = pInstruction.Text.IndexOf( '{', end );
            }

            if( operands > 0 )
                pInstruction.Operands = _tempOperands.Extract( 0, operands );
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
                    JsonConvert.PopulateObject( File.ReadAllText( pFilename ), this );
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

        public struct Operand
        {
            public enum TypeEnum : byte
            {
                Unknown,
                Imm8,
                Imm16,
                CodeRel,
                Index,
                DataAddr,
                CodeAddr
            }

            public TypeEnum Type;
            public ushort Value;

            public Operand( TypeEnum pType, ushort pValue )
            {
                Type = pType;
                Value = pValue;
            }

            public Operand( TypeEnum pType, int pValue )
            {
                Type = pType;
                Value = (ushort)pValue;
            }
        }

        public class Instruction
        {
            public int    Length;
            public byte[] Bytes;
            public string Text;
            public Operand[] Operands;
        }
    }

    public class DisassemblerSettings
    {
        public bool BlankLineBeforeLabel;
    }
}