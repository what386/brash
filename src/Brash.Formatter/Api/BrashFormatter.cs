namespace Brash.Formatter.Api;

public static class BrashFormatter
{
    public static string Format(string source)
    {
        return FormatterEngine.Format(source, FormatterOptions.Default);
    }
}
