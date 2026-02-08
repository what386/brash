using Brash.Compiler.Optimization.Bash;
using Xunit;

namespace Brash.Compiler.Tests;

public class BashOptimizerTests
{
    [Fact]
    public void Optimize_NormalizesLineEndingsAndTrimsWhitespace()
    {
        var input = "line1  \r\nline2\t \r\n";
        var optimizer = new BashOptimizer();

        var output = optimizer.Optimize(input);

        Assert.Equal("line1\nline2\n", output);
    }

    [Fact]
    public void Optimize_CollapsesConsecutiveBlankLines()
    {
        var input = "a\n\n\nb\n\n\n\nc\n";
        var optimizer = new BashOptimizer();

        var output = optimizer.Optimize(input);

        Assert.Equal("a\n\n\nb\n\n\n\nc\n", output);
    }

    [Fact]
    public void Optimize_AllowsDisablingPasses()
    {
        var input = "a  \n\n\nb\n";
        var optimizer = new BashOptimizer();
        var options = new BashOptimizationOptions
        {
            TrimTrailingWhitespace = false,
            NormalizeLineEndings = false
        };

        var output = optimizer.Optimize(input, options);

        Assert.Equal("a  \n\n\nb\n", output);
    }

    [Fact]
    public void Optimize_DoesNotStripCommentOnlyLines()
    {
        var input = "#!/usr/bin/env bash\n# comment\necho ok\n";
        var optimizer = new BashOptimizer();

        var output = optimizer.Optimize(input);

        Assert.Contains("#!/usr/bin/env bash", output);
        Assert.Contains("\n# comment\n", output, StringComparison.Ordinal);
        Assert.Contains("echo ok", output);
    }

    [Fact]
    public void Optimize_DoesNotCompactWhitespaceAroundBlocks()
    {
        var input = "fn() {\n\nx=1\n\n}\n";
        var optimizer = new BashOptimizer();

        var output = optimizer.Optimize(input);

        Assert.Equal(input, output);
    }

    [Fact]
    public void Optimize_DoesNotApplyPeepholeSubstitutions()
    {
        var input = "x=$(printf '%s' \"abc\")\ny=$(printf '%s' \"$(whoami)\")\n";
        var optimizer = new BashOptimizer();

        var output = optimizer.Optimize(input);

        Assert.Equal(input, output);
    }
}
