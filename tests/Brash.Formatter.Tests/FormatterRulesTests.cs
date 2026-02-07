using Brash.Formatter;
using Xunit;

namespace Brash.Formatter.Tests;

public class FormatterRulesTests
{
    [Fact]
    public void Formatter_NormalizesIndentationForBlocks()
    {
        const string input =
            """
            fn main():
            let x=1
            if x>0
            print("ok")
            else
            print("no")
            end
            end
            """;

        const string expected =
            """
            fn main():
                let x = 1
                if x > 0
                    print("ok")
                else
                    print("no")
                end
            end
            """;

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected.Replace("\r\n", "\n") + "\n", formatted);
    }

    [Fact]
    public void Formatter_AddsSpacingAroundCommasAndOperators()
    {
        const string input = "let a=cmd(\"echo\",\"x\")|cmd(\"wc\",\"-c\")";
        const string expected = "let a = cmd(\"echo\", \"x\") | cmd(\"wc\", \"-c\")\n";

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void Formatter_CollapsesRepeatedBlankLines()
    {
        const string input =
            """
            let x=1



            let y=2
            """;

        const string expected =
            """
            let x = 1

            let y = 2
            """;

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected.Replace("\r\n", "\n") + "\n", formatted);
    }

    [Fact]
    public void Formatter_NormalizesTypeAndConstNaming()
    {
        const string input =
            """
            struct person_data
                value: int
            end

            impl person_data
                fn value_or_default(): int
                    return default_value
                end
            end

            const default_value = 42
            let p: person_data = person_data { value: default_value }
            """;

        const string expected =
            """
            struct PersonData
                value: int
            end

            impl PersonData
                fn value_or_default(): int
                    return DEFAULT_VALUE
                end
            end

            const DEFAULT_VALUE = 42
            let p: PersonData = PersonData { value: DEFAULT_VALUE }
            """;

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected.Replace("\r\n", "\n") + "\n", formatted);
    }

    [Fact]
    public void Formatter_DoesNotRenameInsideStringsOrComments()
    {
        const string input =
            """
            const app_name = "app_name"
            // app_name stays as text in comments
            print("app_name")
            let x = app_name
            """;

        const string expected =
            """
            const APP_NAME = "app_name"
            // app_name stays as text in comments
            print("app_name")
            let x = APP_NAME
            """;

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected.Replace("\r\n", "\n") + "\n", formatted);
    }

    [Fact]
    public void Formatter_PreservesInlineCommentDelimiters()
    {
        const string input = "let x=1 // keep inline comment";
        const string expected = "let x = 1 // keep inline comment\n";

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void Formatter_AddsStructLiteralBraceSpacing()
    {
        const string input = "let p:Person=Person{name:\"Ada\",age:42}";
        const string expected = "let p: Person = Person { name: \"Ada\", age: 42 }\n";

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void Formatter_WrapsLongArgumentLists()
    {
        const string input =
            """
            let result = very_long_function_name("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccccccccccccc")
            """;

        const string expected =
            """
            let result = very_long_function_name(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "cccccccccccccccccccccccccccccccc"
            )
            """;

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected.Replace("\r\n", "\n") + "\n", formatted);
    }

    [Fact]
    public void Formatter_InsertsBlankLineBetweenTopLevelMajorDeclarations()
    {
        const string input =
            """
            fn a()
            end
            struct Person
                name: string
            end
            impl Person
                fn greet(): string
                    return "hi"
                end
            end
            """;

        const string expected =
            """
            fn a()
            end

            struct Person
                name: string
            end

            impl Person
                fn greet(): string
                    return "hi"
                end
            end
            """;

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected.Replace("\r\n", "\n") + "\n", formatted);
    }

    [Fact]
    public void Formatter_GroupsConsecutiveImports_AndSeparatesFromDeclarations()
    {
        const string input =
            """
            import "a"
            import "b"
            fn main()
            end
            """;

        const string expected =
            """
            import "a"
            import "b"

            fn main()
            end
            """;

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected.Replace("\r\n", "\n") + "\n", formatted);
    }

    [Fact]
    public void Formatter_IsIdempotent_ForSafeStyleRules()
    {
        const string input =
            """
            import "a"
            fn main()
            let x=1
            if x>0
            print("ok")
            end
            end
            """;

        var once = BrashFormatter.Format(input);
        var twice = BrashFormatter.Format(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void Formatter_SortsTopLevelImportsDeterministically()
    {
        const string input =
            """
            import User from "models/user.bsh"
            import "core/io.bsh"
            import { helper_fn, CONFIG } from "lib/tools.bsh"
            fn main()
            end
            """;

        const string expected =
            """
            import "core/io.bsh"
            import User from "models/user.bsh"
            import { helper_fn, CONFIG } from "lib/tools.bsh"

            fn main()
            end
            """;

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected.Replace("\r\n", "\n") + "\n", formatted);
    }

    [Fact]
    public void Formatter_NormalizesFunctionDeclarationSpacing()
    {
        const string input =
            """
            async fn  greet  ( name : string , greeting : string = "Hello" ) : string
                return greeting + ", " + name
            end
            """;

        const string expected =
            """
            async fn greet(name: string, greeting: string = "Hello"): string
                return greeting + ", " + name
            end
            """;

        var formatted = BrashFormatter.Format(input);
        Assert.Equal(expected.Replace("\r\n", "\n") + "\n", formatted);
    }
}
