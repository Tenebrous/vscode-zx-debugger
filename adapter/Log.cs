using System;

public static class Log
{
    public enum Severity
    {
        Error = 0,
        Message = 1,
        Debug = 2,
        Verbose = 3
    }

    public static Action<Severity, string> OnLog;
    public static Severity MaxSeverity;

    static bool _inLog;

    public static void Write( Severity pSeverity, string pMessage )
    {
        if( _inLog ) return;
        if( pSeverity > MaxSeverity ) return;

        _inLog = true;
        OnLog?.Invoke( pSeverity, pMessage );
        _inLog = false;
    }
}
