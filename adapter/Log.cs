using System;
using System.IO;

public static class Log
{
    public enum Severity
    {
        Error   = 0,
        Message = 1,
        Debug   = 2,
        Verbose = 3
    }

    public static Action<Severity, string> OnLog;
    public static Severity MaxSeverityConsole;
    public static Severity MaxSeverityLog;

    public static string Filename;

    static bool _inLog;
    public static void Write( Severity pSeverity, string pMessage )
    {
        if( _inLog ) return;

        if( pSeverity <= MaxSeverityLog )
            File.AppendAllText( Filename, pSeverity + ": " + pMessage + "\r\n" );

        if( pSeverity > MaxSeverityConsole ) return;

        _inLog = true;
        OnLog?.Invoke( pSeverity, pMessage );
        _inLog = false;
    }

    static Log()
    {
        Filename = Path.Combine(
            Path.GetDirectoryName( System.Reflection.Assembly.GetEntryAssembly().Location ) ?? "",
            "debug.log"
        );
    }
}
