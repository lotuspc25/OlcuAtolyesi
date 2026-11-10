namespace OlcuAtolyesi.Diagnostics;

/// <summary>
/// Tanılama olaylarının önem seviyesi.
/// </summary>
public enum DiagnosticLevel
{
    Debug = 0,
    Info = 1,
    Event = 2,
    Warn = 3,
    Error = 4,
    Critical = 5
}

/// <summary>
/// Tanılama olayının üretildiği sistem kaynağı.
/// </summary>
public enum DiagnosticSource
{
    PC = 0,
    Android = 1
}
