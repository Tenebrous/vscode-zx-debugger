using System;
using System.Collections.Generic;
using System.IO;

namespace ZXDebug
{
    /// <summary>
    /// Class to deal with disassembling z80 from bytes
    /// </summary>
    public class Disassembler : Loggable
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
        /// <param name="filename"></param>
        public void AddLayer( string filename )
        {
            var layer = new OpcodeTables { Filename = filename };
            layer.Read( filename );
            _layers.Add( layer );
        }

        Queue<byte> _tempByteQueue = new Queue<byte>();
        Operand[] _tempOperands = new Operand[2];

        /// <summary>
        /// Disassemble one instruction from the provided bytes starting from the indicated position
        /// </summary>
        /// <param name="bytes">Bytes to be disassembled</param>
        /// <param name="start">Starting position</param>
        /// <returns>A new Op representing the instruction, or null if not valid</returns>
        public Instruction Disassemble( byte[] bytes, int start )
        {

            var stream = new MemoryStream( bytes, start, bytes.Length - start );

            // decipher instruction

            var instruction = GetInstruction( stream );

            if( instruction == null )
                return null;

            // parse the opcode text to get any operands

            if( !GetOperands( instruction, stream ) )
                return null;

            instruction.Length = (int)stream.Position;
            instruction.Bytes = bytes.Extract( start, instruction.Length );

            return instruction;
        }

        Stack<byte> _tempOpcodeList = new Stack<byte>();
        Instruction GetInstruction( MemoryStream stream )
        {
            _tempByteQueue.Clear();
            var tableName = "start";
            Instruction instruction = null;
            int current;

            _tempOpcodeList.Clear();

            while( ( current = stream.ReadByte() ) > -1 )
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

                        var saveByte = stream.ReadByte();

                        if( saveByte == -1 )
                        {
                            LogDebug( "Ran out of bytes decoding {b} in table '" + tableName + "'" );
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
                        stream.Position = stream.Position - 1;

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

        bool GetOperands( Instruction instruction, MemoryStream stream )
        {
            var operands = 0;

            var start = instruction.Text.IndexOf( '{' );
            while( start > -1 )
            {
                var end = instruction.Text.IndexOf( '}', start );

                if( end == -1 )
                    break;

                var specifier = instruction.Text.Substring( start + 1, end - start - 1 );
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
                    lo = _tempByteQueue.Count > 0 ? _tempByteQueue.Dequeue() : stream.ReadByte();

                if( length > 1 )
                    hi = _tempByteQueue.Count > 0 ? _tempByteQueue.Dequeue() : stream.ReadByte();

                if( lo == -1 || hi == -1 )
                {
                    LogDebug( "Ran out of bytes decoding {" + specifier + "} in instruction '" + instruction.Text + "'" );
                    return false;
                }

                _tempOperands[operands++] = new Operand( type, hi << 8 | lo );

                start = instruction.Text.IndexOf( '{', end );
            }

            if( operands > 0 )
                instruction.Operands = _tempOperands.Extract( 0, operands );

            return true;
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

            public Operand( TypeEnum type, ushort value )
            {
                Type = type;
                Value = value;
            }

            public Operand( TypeEnum type, int value )
            {
                Type = type;
                Value = (ushort)value;
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