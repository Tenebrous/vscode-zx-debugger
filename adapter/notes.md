# Read labels from .asm file and write out .dbg file

```csharp
ConvertAsmToDbg( @"D:\Dev\ZX\vscode-zx-debugger\maps\128k_rom0.asm", @"D:\Dev\ZX\vscode-zx-debugger\maps\128k_rom0.dbg", "ROM_0" );
ConvertAsmToDbg( @"D:\Dev\ZX\vscode-zx-debugger\maps\128k_rom1.asm", @"D:\Dev\ZX\vscode-zx-debugger\maps\128k_rom1.dbg", "ROM_1" );

static void ConvertAsmToDbg( string pIn, string pOut, string pType )
{
    using( var fileIn = new StreamReader( pIn ) )
    using( var fileOut = new StreamWriter( pOut ) )
    {
        bool canReadHeader = false;
        bool inHeader = false;
        string header = null;
        string label = null;

        string line;
        while( ( line = fileIn.ReadLine() ) != null )
        {
            if( (line.StartsWith( "; ----" ) || line.StartsWith( "; ====" )) && !inHeader )
            {
                inHeader = true;
                header = null;
            }
            else if( (line.StartsWith( "; ----" ) || line.StartsWith( "; ====" ) || string.IsNullOrWhiteSpace( line )) && inHeader )
            {
                inHeader = false;
            }
            else if( inHeader && line.StartsWith( "; " ) && header == null )
            {
                header = line.Substring( 1 ).Trim();
                label = null;
            }
            else if( header != null && line.StartsWith( ";" ) && !line.Contains( " " ) && !string.IsNullOrWhiteSpace( line.Substring(1) ) )
                label = line.Substring( 1 ).Trim();
            else if( line.StartsWith( "L" ) && line.Substring( 5, 1 ) == ":" && header != null )
            {
                fileOut.WriteLine( pType + " " + line.Substring( 1, 4 ).ToLower() + "h " + ( label ?? line.Substring( 0, 5 ) ) + " " + header );
                header = null;
                label = null;
            }
        }
    }
}
```


# Monitor .png for changes and send updated sprite to ZEsarUX

```csharp
var f = new FileSystemWatcher(@"D:\Dev\ZX\test1", "*.png");
f.EnableRaisingEvents = true;
f.Changed += Files_Changed;


static ConcurrentBag<string> _files = new ConcurrentBag<string>();

static void Files_Changed( object pSender, FileSystemEventArgs pFileSystemEventArgs )
{
    _files.Add( pFileSystemEventArgs.FullPath );
}


// call DoFiles regularly (i.e. in event loop)

static void DoFiles()
{
    var list = _debugger.CustomCommand( "tbblue-get-palette 0 256" );
    if( list == null || list.Count == 0 )
        return;

    var pal = list[0];

    var palStr = pal.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );
    var cols = new Color[palStr.Length];

    for( int i = 0; i < cols.Length; i++ )
    {
        var rgb = (byte)Format.Parse( palStr[i], pKnownHex:true );
        cols[i] = Color.FromArgb( 
            (rgb & 0xE0 ),
            (rgb & 0x1C) << 3, 
            (rgb & 0x03) << 6
        );
    }
    
    var s = new StringBuilder();

    while( _files.TryTake( out var file ) )
    {
        s.Clear();

        using( var img = new Bitmap( file ) )
        {
            for( int y = 0; y < 16; y++ )
            {
                for( int x = 0; x < 16; x++ )
                {
                    s.Append( ' ' );

                    var pix = img.GetPixel( x, y );

                    if( pix.A == 0 )
                    {
                        s.Append( "E3h" );
                        continue;
                    }

                    var closest = 0;
                    var closestDist = double.MaxValue;
                    
                    for( int i = 0; i < cols.Length; i++ )
                    {
                        //var dhue = cols[i].GetHue() - pix.GetHue();
                        //var dsat = cols[i].GetSaturation() - pix.GetSaturation();
                        //var dbri = cols[i].GetBrightness() - pix.GetBrightness();
                        //var dist = Math.Sqrt( dhue * dhue + dsat * dsat + dbri * dbri );

                        var dr = cols[i].R - pix.R;
                        var dg = cols[i].G - pix.G;
                        var db = cols[i].B - pix.B;
                        var dist = Math.Sqrt( dr * dr + dg * dg + db * db );

                        if( dist < closestDist )
                        {
                            closest = i;
                            closestDist = dist;
                        }
                    }

                    s.Append( string.Format( "{0:X2}h", closest ) );
                }
            }
        }

        for( int i = 0; i < 64; i++ )
        {
            var result = _debugger.CustomCommand( "tbblue-set-pattern " + i + s.ToString() );
        }
    }
}
```