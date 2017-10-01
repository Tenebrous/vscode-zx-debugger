using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Spectrum;

namespace ZXDebug.SourceMapper
{
    /// <summary>
    /// Stores parsed information from a single memory map (e.g. .dbg or .map)
    /// </summary>
    public class Map
    {
        public string SourceRoot;
        public string Filename;
        public Maps Maps;
        
        public Banks Banks = new Banks();

        public Cache<BankID, AddressMatches> BankAddress = new Cache<BankID, AddressMatches>();
        public Cache<File, LineMatches> FileLine = new Cache<File, LineMatches>();

        /// <summary>
        /// Create a new map from the referenced file
        /// </summary>
        /// <param name="pParent">Mapper</param>
        /// <param name="pSourceRoot">Root folder for relative source paths</param>
        /// <param name="pFilename">File to read</param>
        public Map( Maps pParent, string pSourceRoot, string pFilename )
        {
            Maps = pParent;
            SourceRoot = pSourceRoot;
            Filename = pFilename;

            var ext = Path.GetExtension( pFilename )?.ToLower() ?? "";

            if( ext == ".dbg" )
                ReadDbg( pFilename );
            else if( ext == ".map" )
                ReadMap( pFilename );

            //File.WriteAllText( pFilename + ".json", JsonConvert.SerializeObject( this, new JsonSerializerSettings() {Formatting = Formatting.Indented,ReferenceLoopHandling = ReferenceLoopHandling.Ignore} ) );
        }

        void ReadDbg( string pFilename )
        {
            // banktype_bankid addr(h) ("filename":line) (label1 (label2...)) (; comment)
            //
            // ROM_0 0000h start                 ; cold start restart
            // BANK_02 8000h "C:\Users\m\AppData\Local\Temp\zcc53216.asm":1367 __Start

            var fileLine = new Regex(
                @"(?i)^(?'banktype'ROM|RAM|BANK|DIV|ALL)(?:_)?((?'bankid'\d*))? (?'addr'[0-9a-f]+h?)( (?'file'"".*?""):(?'line'\d*))?(?'labels'(?'label' [a-z0-9_]+)*)(?'comment' *;.*?)?$",
                RegexOptions.Compiled
            );

            using( var reader = new StreamReader( pFilename ) )
            {
                string text;

                while( ( text = reader.ReadLine() ) != null )
                {
                    var matches = fileLine.Matches( text );
                    foreach( Match match in matches )
                    {
                        var bankTypeStr = match.Groups["banktype"].Value;
                        var bankIDStr   = match.Groups["bankid"].Value;
                        var addressStr  = match.Groups["addr"].Value;
                        var fileStr     = match.Groups["file"].Value;
                        var lineStr     = match.Groups["line"].Value;
                        var labels      = match.Groups["labels"];
                        var commentStr  = match.Groups["comment"].Value;

                        if( labels.Length == 0 )
                            continue;

                        int bankID;
                        int.TryParse( bankIDStr, out bankID );

                        var address = Format.Parse( addressStr );

                        var bank = new BankID( bankTypeStr, bankID );

                        Banks.TryAdd( bank, out var symBank );
                        symBank.Symbols.TryAdd( address, out var sym );

                        var labelCaptures = labels.Captures;
                        sym.Labels = sym.Labels ?? new List<string>();
                        for( var i = 0; i < labelCaptures.Count; i++ )
                            sym.Labels.Add( labelCaptures[i].Value.Trim() );

                        sym.Comment = commentStr.Trim();
                        if( sym.Comment.StartsWith( ";" ) )
                            sym.Comment = sym.Comment.Substring( 1 ).Trim();

                        if( !string.IsNullOrWhiteSpace( fileStr ) )
                        {
                            var file = Maps.Files[fileStr];
                            var line = int.Parse( lineStr );

                            if( sym.File == null || sym.File.Filename != fileStr )
                            {
                                sym.File = file;
                                sym.Line = line;
                            }
                            else if( line > sym.Line )
                                sym.Line = line;
                        }

                        sym.Map = this;
                    }
                }
            }
        }

        void ReadMap( string pFilename )
        {
            // symbol = $address ; type, scope, def, module, section, file:line
            //
            // DEFINED_startup                 = $0001 ; const, local, , zxn_crt, , C:\Users\m\AppData\Local\Temp\zcc12716.asm:10
            // startup                         = $001F ; const, local, , zxn_crt, , C:\Users\m\AppData\Local\Temp\zcc12716.asm:11
            // DEFINED_REGISTER_SP             = $0001 ; const, local, , zxn_crt, , C:\Users\m\AppData\Local\Temp\zcc12716.asm:18
            // REGISTER_SP                     = $7FFF ; const, local, , zxn_crt, , C:\Users\m\AppData\Local\Temp\zcc12716.asm:19

            var lineRegex = new Regex(
                @"(?i)^(?'label'.*?)\s*=\s*(\$(?'addr'[0-9a-fA-F]*))\s?;\s(?'type'addr)\s*,\s*(?'scope'.*?)\s*,\s*(?'def'.*?)\s*,\s*(?'module'.*?)\s*,\s*(?'section'.*?)\s*,\s*(?'file'.*):(?'line'\d*).*?$",
                RegexOptions.Compiled
            );

            var symbolRegex = new Regex(
                @"^(?'name'.*):(?'line'\d*)(:(?'section'.*))?$",
                RegexOptions.Compiled
            );

            using( var reader = new StreamReader( pFilename ) )
            {
                string text;

                while( ( text = reader.ReadLine() ) != null )
                {
                    try
                    {
                        var matches = lineRegex.Matches( text );
                        foreach( Match match in matches )
                        {
                            var label       = match.Groups["label"].Value;
                            var addressStr  = match.Groups["addr"].Value;
                            var type        = match.Groups["type"].Value;
                            var scope       = match.Groups["scope"].Value;
                            var def         = match.Groups["def"].Value;
                            var module      = match.Groups["module"].Value;
                            var section     = match.Groups["section"].Value;
                            var fileStr     = match.Groups["file"].Value;
                            var lineStr     = match.Groups["line"].Value;

                            if( label.StartsWith( "__ASMLINE__" ) || label.StartsWith( "__CLINE__" ) )
                            {
                                // decode symbol
                                //  _22src_5Cmain_2Easm_22_3A0_3Adefault
                                //  "src\main.asm":0:default

                                if( label.StartsWith( "__ASMLINE__" ) )
                                    label = Decode( label.Substring( 11 ) ).Trim();
                                else if( label.StartsWith( "__CLINE__" ) )
                                    label = Decode( label.Substring( 9 ) ).Trim();

                                var labelMatch = symbolRegex.Match( label );
                                fileStr = labelMatch.Groups["name"].Value;
                                lineStr = labelMatch.Groups["line"].Value;
                                section = labelMatch.Groups["section"].Value;
                                label = null;

                                if( string.IsNullOrWhiteSpace( lineStr ) )
                                    lineStr = "0";
                            }
                            else if( label.StartsWith( "__ASM_LINE_" ) || label.StartsWith( "__C_LINE_" ) )
                            {
                                label = null;
                            }

                            var address = Format.Parse( addressStr, pKnownHex: true );
                            var bank = new BankID( section );

                            Banks.TryAdd(bank, out var symBank);
                            symBank.Symbols.TryAdd( address, out var sym );

                            if( label != null )
                            {
                                sym.Labels = sym.Labels ?? new List<string>();
                                sym.Labels.Add( label );
                            }

                            if( !string.IsNullOrWhiteSpace( fileStr ) )
                            {
                                var normalisedFileStr = Path.GetFullPath( Path.Combine( SourceRoot, fileStr ) );

                                Maps.Files.TryAdd( normalisedFileStr, out var file );
                                var line = int.Parse( lineStr );

                                if( sym.File == null || sym.File.Filename != normalisedFileStr )
                                {
                                    sym.File = file;
                                    sym.Line = line;
                                }
                                else if( line > sym.Line ) // && label != null )
                                {
                                    sym.Line = line;
                                }

                                
                                SourceLink store;

                                FileLine.TryAdd(file, out var fileLine );
                                if( fileLine.TryAdd( line, out var fileLineRange, out store ) )
                                {
                                    store.File = file;
                                    store.BankID = bank;
                                    store.LowerLine = int.MaxValue;
                                    store.LowerAddress = ushort.MaxValue;
                                }
                                fileLine.Extend( fileLineRange, line );
                                if( line < store.LowerLine ) store.LowerLine = line;
                                if( line > store.UpperLine ) store.UpperLine = line;
                                if( address < store.LowerAddress ) store.LowerAddress = address;
                                if( address > store.UpperAddress ) store.UpperAddress = address;
                                
                                BankAddress.TryAdd( bank, out var bankAddress );
                                if( bankAddress.TryAdd( address, out var bankAddressRange, out store ) )
                                {
                                    store.File = file;
                                    store.BankID = bank;
                                    store.LowerLine = int.MaxValue;
                                    store.LowerAddress = ushort.MaxValue;
                                }
                                bankAddress.Extend( bankAddressRange, address );
                                if( line < store.LowerLine ) store.LowerLine = line;
                                if( line > store.UpperLine ) store.UpperLine = line;
                                if( address < store.LowerAddress ) store.LowerAddress = address;
                                if( address > store.UpperAddress ) store.UpperAddress = address;
                            }

                            sym.Map = this;
                        }
                    }
                    catch( Exception e )
                    {
                        throw new Exception( "Error with line [" + text + "]", e );
                    }
                }
            }
        }

        static string Dequote( string pValue )
        {
            if( pValue.StartsWith( "\"" ) && pValue.EndsWith( "\"" ) )
                return pValue.Substring( 1, pValue.Length - 2 );

            return pValue;
        }

        static string Decode( string pValue )
        {
            // replace _(hex) values with ascii equivelant

            var result = "";

            for( int i = 0; i < pValue.Length; i++ )
            {
                if( pValue[i] == '_' )
                {
                    var hex = Convert.ToUInt32( pValue.Substring(i+1,2), 16 );
                    i += 2;
                    result += (char)hex;
                }
                else
                    result += pValue[i];
            }

            return result.Trim();
        }

        public override string ToString()
        {
            return Filename;
        }
    }
     
    public class SourceLink
    {
        public File File;
        public int LowerLine;
        public int UpperLine;

        public BankID BankID;
        public ushort LowerAddress;
        public ushort UpperAddress;
    }

    public class AddressMatches : RangeDictionary<ushort, SourceLink>
    {
    }

    public class LineMatches : RangeDictionary<int, SourceLink>
    {
    }
}
