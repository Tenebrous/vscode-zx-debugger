﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using Spectrum;
using ZXDebug.utils;

namespace ZXDebug.SourceMapper
{
    public class Label
    {
        public string Name;
        public string Comment;
    }

    public class LabelLocation
    {
        public Map    Map;
        public BankID BankID;
        public ushort Address;
        public string Comment;
    }

    /// <summary>
    /// Stores parsed information from a single memory map (e.g. .dbg or .map)
    /// </summary>
    public class Map
    {
        public string SourceRoot;
        public string Filename;
        public Maps Maps;

        public SpatialDictionary<BankID, ushort, FileLine> Source = new SpatialDictionary<BankID, ushort, FileLine>( 
            factory : ( bank, address ) => new FileLine()
        );

        public SpatialDictionary<BankID, ushort, List<Label>> Labels = new SpatialDictionary<BankID, ushort, List<Label>>(
            factory : ( bank, address ) => new List<Label>()
        );

        public Cache<string,List<LabelLocation>> ByLabel = new Cache<string,List<LabelLocation>>(
            factory : s => new List<LabelLocation>(),
            comparer : StringComparer.OrdinalIgnoreCase
        );

        Regex _regexDbg = new Regex(
                @"(?i)^(?'bank'(ROM|RAM|BANK|DIV|ALL)(_)?(\d*)?) (?'addr'[0-9a-f]+h?)( (?'file'"".*?""):(?'line'\d*))?(?'labels'(?'label' [a-z0-9_]+)*)( *;(?'comment'.*?))?$",
                RegexOptions.Compiled
            );

        Regex _regexMap = new Regex(
                @"(?i)^(?'label'.*?)\s*=\s*(\$(?'addr'[0-9a-fA-F]*))\s?;\s(?'type'addr)\s*,\s*(?'scope'.*?)\s*,\s*(?'def'.*?)\s*,\s*(?'module'.*?)\s*,\s*(?'bank'.*?)\s*,\s*(?'file'.*):(?'line'\d*).*?$",
                RegexOptions.Compiled
            );

        /// <summary>
        /// Create a new map from the referenced file
        /// </summary>
        /// <param name="parent">Mapper</param>
        /// <param name="sourceRoot">Root folder for relative source paths</param>
        /// <param name="filename">File to read</param>
        public Map( Maps parent, string sourceRoot, string filename )
        {
            Maps = parent;
            SourceRoot = sourceRoot;
            Filename = filename;

            var ext = Path.GetExtension( filename )?.ToLower() ?? "";

            if( ext == ".dbg" )
                Parse( filename, _regexDbg );
            else if( ext == ".map" )
                Parse( filename, _regexMap );
        }


        List<Label> _tempLabels = new List<Label>();
        void Parse( string filename, Regex regex )
        {
            using( var reader = new StreamReader( filename ) )
            {
                string text;

                while( ( text = reader.ReadLine() ) != null )
                {
                    var matches = regex.Matches( text );

                    foreach( Match match in matches )
                    {
                        var bankStr    = match.Groups["bank"].Value;
                        var addressStr = match.Groups["addr"].Value;
                        var fileStr    = match.Groups["file"].Value;
                        var lineStr    = match.Groups["line"].Value;
                        var labelStr   = match.Groups["label"].Value;
                        var labelsGrp  = match.Groups["labels"];
                        var commentStr = match.Groups["comment"].Value;
                        var typeStr    = match.Groups["type"].Value;
                        var scopeStr   = match.Groups["scope"].Value;
                        var defStr     = match.Groups["def"].Value;
                        var moduleStr  = match.Groups["module"].Value;

                        if( labelStr.StartsWith( "__ASM_LINE_" ) 
                            || labelStr.StartsWith( "__C_LINE_" ) 
                            || labelStr.StartsWith( "__CLINE_" ) )
                            labelStr = null;

                        if( string.IsNullOrWhiteSpace( commentStr ) )
                            commentStr = null;
                        else
                            commentStr = commentStr.Trim();

                        _tempLabels.Clear();

                        if( labelsGrp.Captures.Count == 0 )
                            if( !string.IsNullOrWhiteSpace(labelStr) )
                                _tempLabels.Add( new Label() { Name = labelStr, Comment = commentStr } );

                        for( var i = 0; i < labelsGrp.Captures.Count; i++ )
                            _tempLabels.Add( new Label() { Name = labelsGrp.Captures[i].Value.Trim(), Comment = commentStr } );

                        AddMapping( bankStr, addressStr, fileStr, lineStr, _tempLabels );
                    }
                }
            }
        }

        void AddMapping( string bankStr, string addressStr, string fileStr, string lineStr, List<Label> labelList )
        {
            var bank = new BankID( bankStr );
            var address = Convert.Parse( addressStr, isHex : true );

            if( labelList != null && labelList.Count > 0 )
            {
                Labels.TryAdd( bank, address, out var labels );
                labels.AddRange( labelList );

                foreach( var l in labelList )
                {
                    ByLabel[l.Name].Add( new LabelLocation()
                        {
                            Map = this,
                            BankID = bank,
                            Address = address,
                            Comment = l.Comment
                        }
                    );
                }
            }

            if( !string.IsNullOrWhiteSpace( fileStr ) )
            {
                var normalisedFileStr = Path.GetFullPath( Path.Combine( SourceRoot, fileStr ) );
                var line = int.Parse( lineStr );

                Maps.Files.TryAdd( normalisedFileStr, out var file );

                if( Source.TryAdd( bank, address, out var sym,
                        ( pBank, pAddress ) => new FileLine(file, line)
                    )
                )
                {
                    // TryAdd returns false if item was already there, true if it was added

                    if( sym.File != file )
                    {
                        sym.File = file;
                        sym.Line = line;
                    }
                    else if( line > sym.Line )
                    {
                        sym.Line = line;
                    }
                }
            }
        }

        public override string ToString()
        {
            return Filename;
        }
    }

    public class FileLine
    {
        public File File;
        public int Line;

        public FileLine()
        {
        }

        public FileLine( File file, int line )
        {
            File = file;
            Line = line;
        }

        public override string ToString()
        {
            return $"{File}:{Line}";
        }

        public override bool Equals( object other )
        {
            if( object.ReferenceEquals( other, null ) )
                return false;

            if( object.ReferenceEquals( this, other ) )
                return true;

            if( GetType() != other.GetType() )
                return false;

            var cast = (FileLine)other;

            return File == cast.File && Line == cast.Line;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // ReSharper disable NonReadonlyMemberInGetHashCode
                int hash = 17;
                hash = hash * 23 + File.GetHashCode();
                hash = hash * 23 + Line.GetHashCode();
                return hash;
                // ReSharper restore NonReadonlyMemberInGetHashCode
            }
        }

        public static bool operator ==( FileLine left, FileLine right )
        {
            if( object.ReferenceEquals( left, null ) )
            {
                if( object.ReferenceEquals( right, null ) )
                    return true;

                return false;
            }

            return left.Equals( right );
        }

        public static bool operator !=( FileLine left, FileLine right )
        {
            return !( left == right );
        }
    }
}
