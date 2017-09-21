using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Spectrum;
using Newtonsoft.Json;

namespace ZXDebug
{
    /// <summary>
    /// Stores parsed information from a single memory map (e.g. .dbg)
    /// </summary>
    public class SourceMap
    {
        public string Filename;

        public SourceBanks Banks = new SourceBanks();
        public Sources Files = new Sources();

        public SourceMap( string pFilename )
        {
            Filename = pFilename;

            var ext = Path.GetExtension( pFilename )?.ToLower() ?? "";

            if( ext == ".dbg" )
                ReadDbg( pFilename );
            else if( ext == ".map" )
                ReadMap( pFilename );

            //File.WriteAllText( pFilename + ".json", JsonConvert.SerializeObject( this, new JsonSerializerSettings() {Formatting = Formatting.Indented,ReferenceLoopHandling = ReferenceLoopHandling.Ignore} ) );
        }

        public void ReadDbg( string pFilename )
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
                        var symBank = Banks[bank];
                        var sym = symBank.Symbols[address];

                        var labelCaptures = labels.Captures;
                        sym.Labels = new List<string>();
                        for( var i = 0; i < labelCaptures.Count; i++ )
                            sym.Labels.Add( labelCaptures[i].Value.Trim() );

                        sym.Comment = commentStr.Trim();
                        if( sym.Comment.StartsWith( ";" ) )
                            sym.Comment = sym.Comment.Substring( 1 ).Trim();

                        if( !string.IsNullOrWhiteSpace( fileStr ) )
                        {
                            var file = Files[fileStr];
                            sym.File = file;
                            sym.Line = int.Parse( lineStr );
                        }

                        sym.Map = this;
                    }
                }
            }
        }

        public void ReadMap( string pFilename )
        {
            // symbol = $address ; type, scope, def, module, section, file:line
            //
            // DEFINED_startup                 = $0001 ; const, local, , zxn_crt, , C:\Users\m\AppData\Local\Temp\zcc12716.asm:10
            // startup                         = $001F ; const, local, , zxn_crt, , C:\Users\m\AppData\Local\Temp\zcc12716.asm:11
            // DEFINED_REGISTER_SP             = $0001 ; const, local, , zxn_crt, , C:\Users\m\AppData\Local\Temp\zcc12716.asm:18
            // REGISTER_SP                     = $7FFF ; const, local, , zxn_crt, , C:\Users\m\AppData\Local\Temp\zcc12716.asm:19

            var fileLine = new Regex(
                @"(?i)^(?'label'.*?)\s*=\s*(\$(?'addr'[0-9a-fA-F]*))\s?;\s(?'type'addr)\s*,\s*(?'scope'.*?)\s*,\s*(?'def'.*?)\s*,\s*(?'module'.*?)\s*,\s*(?'section'.*?)\s*,\s*(?'file'.*):(?'line'\d*).*?$",
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
                        var label      = match.Groups["label"].Value;
                        var addressStr = match.Groups["addr"].Value;
                        var type       = match.Groups["type"].Value;
                        var scope      = match.Groups["scope"].Value;
                        var def        = match.Groups["def"].Value;
                        var module     = match.Groups["module"].Value;
                        var section    = match.Groups["section"].Value;
                        var fileStr    = match.Groups["file"].Value;
                        var lineStr    = match.Groups["line"].Value;

                        if( label.StartsWith( "__ASMLINE__" ) || label.StartsWith( "__CLINE__" ) )
                        {
                            // decode symbol
                            //  _22src_5Cmain_2Easm_22_3A0_3Adefault
                            //  "src\main.asm":0:default

                            if( label.StartsWith( "__ASMLINE__" ) )
                                label = Decode( label.Substring( 11 ) ).Trim();
                            else if( label.StartsWith( "__CLINE__" ) )
                                label = Decode( label.Substring( 9 ) ).Trim();

                            var labelSplit = label.Split( ':' );
                            fileStr = Dequote( labelSplit[0] );
                            lineStr = labelSplit.Length > 1 ? labelSplit[1] : "0";
                            section = labelSplit.Length > 2 ? labelSplit[2] : "";
                            label = null;
                        }

                        var address = Format.Parse( addressStr, pKnownHex: true );
                        var bank = new BankID( section );
                        var symBank = Banks[bank];
                        var sym = symBank.Symbols[address];

                        if( label != null )
                        {
                            if( sym.Labels == null )
                                sym.Labels = new List<string>();

                            sym.Labels.Add( label );
                        }

                        if( !string.IsNullOrWhiteSpace( fileStr ) )
                        {
                            var file = Files[fileStr];
                            sym.File = file;
                            sym.Line = int.Parse( lineStr );
                        }

                        sym.Map = this;
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


        public class SourceBanks : Cache<BankID, SourceBank>
        {
            public SourceBanks() : base( NewBank ) { }

            static SourceBank NewBank( BankID pBank )
            {
                return new SourceBank() { Bank = pBank };
            }
        }

        public class SourceBank
        {
            public BankID Bank;

            public Cache<ushort, SourceAddress> Symbols;

            public SourceBank()
            {
                Symbols = new Cache<ushort, SourceAddress>( NewSymbol );
            }

            SourceAddress NewSymbol( ushort pAddress )
            {
                return new SourceAddress() { Address = pAddress };
            }
        }

        public class SourceAddress
        {
            public SourceMap    Map;
            public ushort       Address;
            public Source       File;
            public int          Line;
            public List<string> Labels;
            public string       Comment;
        }

        public class Sources : Cache<string, Source>
        {
            public Sources() : base( NewSource ) { }

            static Source NewSource( string pFile )
            {
                return new Source() { Filename = pFile };
            }
        }

        public class Source
        {
            public string Filename;
        }
    }

    /// <summary>
    /// A collection of Map files
    /// </summary>
    public class SourceMaps : List<SourceMap>
    {
        public SourceMap.SourceAddress Find( BankID pBank, ushort pAddress )
        {
            SourceMap.SourceAddress value = null;

            foreach( var map in this )
            {
                if( !map.Banks.TryGetValue( pBank, out var bank ) )
                    continue;

                if( !bank.Symbols.TryGetValue( pAddress, out value ) )
                    continue;
            }

            return value;
        }
    }
}
