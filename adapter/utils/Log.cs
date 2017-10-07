﻿using System;
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
    public static void Write( Severity severity, string message )
    {
        if( _inLog ) return;

        if( severity <= MaxSeverityLog )
            File.AppendAllText( Filename, severity + ": " + message + "\r\n" );

        if( severity > MaxSeverityConsole ) return;

        _inLog = true;
        OnLog?.Invoke( severity, message );
        _inLog = false;
    }

    static Log()
    {
        Filename = Path.Combine(
            Path.GetDirectoryName( System.Reflection.Assembly.GetEntryAssembly().Location ) ?? "",
            "debug.log"
        );

        if( File.Exists( Filename + ".prev" ) ) 
            File.Delete( Filename + ".prev" );

        if( File.Exists( Filename ) )
            File.Move( Filename, Filename + ".prev" );
    }
}
