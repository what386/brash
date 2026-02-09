namespace Brash.Compiler.Preprocessor.Core;

internal sealed class ConditionalFrame
{
    public ConditionalFrame(int startLine, bool parentActive, bool conditionTrue)
    {
        StartLine = startLine;
        ParentActive = parentActive;
        ConditionMatched = conditionTrue;
        CurrentBranchActive = parentActive && conditionTrue;
    }

    public int StartLine { get; }
    public bool ParentActive { get; }
    public bool ConditionMatched { get; set; }
    public bool CurrentBranchActive { get; set; }
    public bool ElseSeen { get; set; }
}

