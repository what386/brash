namespace Brash.Compiler.CodeGen;

internal sealed class HelperUsage
{
    public bool NeedsGetField { get; set; }
    public bool NeedsSetField { get; set; }
    public bool NeedsCallMethod { get; set; }
    public bool NeedsMapNew { get; set; }
    public bool NeedsMapSet { get; set; }
    public bool NeedsMapGet { get; set; }
    public bool NeedsMapLiteral { get; set; }
    public bool NeedsIndexGet { get; set; }
    public bool NeedsIndexSet { get; set; }
    public bool NeedsExecCmd { get; set; }
    public bool NeedsAsyncSpawnCmd { get; set; }
    public bool NeedsAwait { get; set; }
    public bool NeedsReadLn { get; set; }
}
