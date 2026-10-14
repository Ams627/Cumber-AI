using System.Globalization;
using HelpTextOptions;
using Xunit;

namespace HelpTextOptions.Tests;

public sealed class ParserTests
{
    private const string Definition = """
        @group common
        -v, --verbose  Verbose output
        -c, --colour  Output colour | type=string

        = FileUtil - a file utility
          @usegroup common
          $(subcommands)

        == help - overview
        CUSTOM OVERVIEW
          $(subcommands)

        == Print - print a file
        -n, --lines  Number of lines | type=int

        == Merge - merge files
        -i, --input  Input | type=string minOccurs=1 maxOccurs=*

        == Remote - list remotes | runnable=true
        === Add - add a remote
        === Remove - remove a remote

        == Raw - raw data | noinherit
        -a, --all  All items
        === Child - child

        = Split - split files
        -n, --parts  Parts | type=uint
        """;

    private static (IOptionsProcessor Result, string Output, string Error) Parse(
        string[] args, string definition = Definition, string program = "FileUtil", int width = 80,
        ColourMode colour = ColourMode.Never)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = OptionsProcessor.FromText(definition, program, args, new OptionsProcessorSettings
        {
            Output = output, Error = error, TerminalWidth = width, ColourMode = colour
        });
        return (result, output.ToString(), error.ToString());
    }

    [Fact]
    public void AttachedValuesAndOptionsAfterPositionals()
    {
        var (result, _, error) = Parse(["Print", "-n10", "file.txt", "--colour=red"]);
        Assert.True(result.IsValid, error);
        Assert.Equal(["Print"], result.CommandPath);
        Assert.Equal(["file.txt"], result.PositionalArguments);
        Assert.Equal(10, result.GetValue<int>('n'));
        Assert.Equal("red", result.GetValue<string>("colour"));
        Assert.Equal(result.IsOptionPresent('c'), result.IsOptionPresent("colour"));
    }

    [Fact]
    public void RepeatedAliasesPreserveValueOrder()
    {
        var (result, _, error) = Parse(["Merge", "-ione", "--input", "two", "-i", "three"]);
        Assert.True(result.IsValid, error);
        Assert.Equal(["one", "two", "three"], result.GetValues<string>('i'));
        Assert.Equal(3, result.GetOccurrenceCount("input"));
        Assert.Throws<InvalidOperationException>(() => result.GetValue<string>('i'));
    }

    [Fact]
    public void AliasOccurrencesShareTheLimit()
    {
        var (result, _, error) = Parse(["Print", "-v", "--verbose"]);
        Assert.False(result.IsValid);
        Assert.Contains("at most 1", error);
        Assert.Throws<InvalidOperationException>(() => result.IsOptionPresent('v'));
    }

    [Fact]
    public void RequiredValuesAreValidatedButHelpBypassesThem()
    {
        Assert.False(Parse(["Merge"]).Result.IsValid);
        var (help, output, _) = Parse(["help", "Merge"]);
        Assert.True(help.IsValid);
        Assert.True(help.HelpRequested);
        Assert.Contains("--input", output);
    }

    [Fact]
    public void NoInheritBlocksAncestorsButDescendantsInheritLocalOptions()
    {
        var result = Parse(["Raw", "Child", "-a"]).Result;
        Assert.True(result.IsValid);
        Assert.True(result.IsOptionPresent("all"));
        Assert.False(Parse(["Raw", "Child", "-v"]).Result.IsValid);
    }

    [Fact]
    public void TerminatorAndFirstPositionalEndCommandMatching()
    {
        var result = Parse(["Remote", "--", "Add", "-v"]).Result;
        Assert.True(result.IsValid);
        Assert.Equal(["Remote"], result.CommandPath);
        Assert.Equal(["Add", "-v"], result.PositionalArguments);
        result = Parse(["Remote", "origin", "Add"]).Result;
        Assert.Equal(["origin", "Add"], result.PositionalArguments);
    }

    [Fact]
    public void OptionsCannotInterruptCommandPath()
    {
        var result = Parse(["Remote", "-v", "Add"]).Result;
        Assert.True(result.IsValid);
        Assert.Equal(["Remote"], result.CommandPath);
        Assert.Equal(["Add"], result.PositionalArguments);
    }

    [Fact]
    public void CustomHelpDoesNotReplaceTargetedHelp()
    {
        Assert.Contains("CUSTOM OVERVIEW", Parse(["help"]).Output);
        var (_, output, _) = Parse(["help", "Print"]);
        Assert.Contains("--lines", output);
        Assert.DoesNotContain("CUSTOM OVERVIEW", output);
        Assert.False(Parse(["help", "Missing"]).Result.IsValid);
    }

    [Fact]
    public void RootsAreIndependentAndManuallySelected()
    {
        var result = Parse(["-n3"], program: "Split").Result;
        Assert.Equal((uint)3, result.GetValue<uint>("parts"));
        Assert.False(Parse(["--verbose"], program: "Split").Result.IsValid);
        Assert.Throws<ArgumentException>(() => Parse([], program: "Missing"));
    }

    [Fact]
    public void ContainerAloneShowsHelpButUnknownChildErrors()
    {
        const string text = "= tool\n== child\n";
        var (result, output, _) = Parse([], text, "tool");
        Assert.True(result.IsValid);
        Assert.True(result.HelpRequested);
        Assert.Contains("tool", output);
        Assert.False(Parse(["missing"], text, "tool").Result.IsValid);
    }

    [Theory]
    [InlineData("-bc", true)]
    [InlineData("-bcn10", true)]
    [InlineData("-n=10", false)]
    [InlineData("-missing", false)]
    public void ShortGroups(string argument, bool expected)
    {
        const string text = "= tool\n-b  B\n-c  C\n-n, --number  N | type=int\n";
        var result = Parse([argument], text, "tool").Result;
        Assert.Equal(expected, result.IsValid);
        if (argument == "-bcn10") Assert.Equal(10, result.GetValue<int>('n'));
    }

    [Fact]
    public void ValueTakingShortOptionConsumesRemainderEvenWhenLongNameExists()
    {
        const string text = "= tool\n-a  A | type=string\n--abc  ABC\n";
        var result = Parse(["-abc"], text, "tool").Result;
        Assert.Equal("bc", result.GetValue<string>('a'));
        Assert.False(result.IsOptionPresent("abc"));
        var (_, _, error) = Parse(["-abc"], "= tool\n--abc  ABC\n", "tool");
        Assert.Contains("two dashes", error);
    }

    [Theory]
    [InlineData("--max--count")]
    [InlineData("--max_-count")]
    [InlineData("--max_")]
    [InlineData("--a")]
    [InlineData("-a, --all | nonsense=true")]
    public void BadDefinitionsThrow(string option)
    {
        Assert.Throws<OptionsSpecificationException>(() => Parse([], "= tool\n" + option, "tool"));
    }

    [Fact]
    public void InvalidHierarchyGroupsAndCollisionsThrow()
    {
        Assert.Throws<OptionsSpecificationException>(() => Parse([], "= tool\n=== skip", "tool"));
        Assert.Throws<OptionsSpecificationException>(() => Parse([], "= tool\n@usegroup missing", "tool"));
        Assert.Throws<OptionsSpecificationException>(() => Parse([], "= tool\n-a A\n== child\n-a B", "tool"));
    }

    [Fact]
    public void EmptyAndDashLeadingStringsHaveUnambiguousForms()
    {
        const string text = "= tool\n-s, --string S | type=string";
        Assert.Equal("", Parse(["--string="], text, "tool").Result.GetValue<string>('s'));
        Assert.Equal("--all", Parse(["--string=--all"], text, "tool").Result.GetValue<string>('s'));
        Assert.False(Parse(["-s", "--all"], text, "tool").Result.IsValid);
    }

    [Fact]
    public void NumericParsingUsesInvariantCultureAndRejectsNonfiniteValues()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
            const string text = "= tool\n-f Float | type=float\n-n Number | type=int";
            Assert.Equal(1.25f, Parse(["-f1.25"], text, "tool").Result.GetValue<float>('f'));
            Assert.Equal(-3, Parse(["-n", "-3"], text, "tool").Result.GetValue<int>('n'));
            Assert.False(Parse(["-fNaN"], text, "tool").Result.IsValid);
            Assert.False(Parse(["-f1,25"], text, "tool").Result.IsValid);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void LookupErrorsAndAbsentDefaultsArePredictable()
    {
        var result = Parse(["Print"]).Result;
        Assert.Equal(20, result.GetValueOrDefault("lines", 20));
        Assert.Empty(result.GetValues<int>('n'));
        Assert.Throws<ArgumentException>(() => result.IsOptionPresent("unknown"));
        Assert.Throws<ArgumentException>(() => result.GetValues<string>('n'));
        Assert.Throws<InvalidOperationException>(() => result.GetValue<bool>('v'));
    }

    [Fact]
    public void MacroOnlyShowsChildrenAndUsesItsIndent()
    {
        const string text = "= tool\n    $(subcommands)\n== one - first\n=== hidden - grandchild\n== two - second";
        var (_, output, _) = Parse(["help"], text, "tool");
        Assert.Contains("    one - first", output);
        Assert.Contains("    two - second", output);
        Assert.DoesNotContain("hidden", output);
    }

    [Fact]
    public void WrappingNeverBreaksWordsAndPlainOutputRemovesMarkup()
    {
        const string word = "averylongunbrokenword";
        string text = "= tool\n[green]#alpha beta# *gamma* " + word;
        var (_, output, _) = Parse(["help"], text, "tool", width: 12);
        Assert.Contains(word, output);
        Assert.DoesNotContain("[green]", output);
        Assert.DoesNotContain("*gamma*", output);
        Assert.DoesNotContain("\u001b", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RgbAndEscapedPipesAreRenderedAndInvalidRgbThrows()
    {
        const string text = "= tool\n-a [1, 2, 3]#C# reference \\| literal | maxOccurs=1";
        Assert.Contains("C reference | literal", Parse(["help"], text, "tool").Output);
        Assert.Contains("\u001b[38;2;1;2;3m", Parse(["help"], text, "tool", colour: ColourMode.Always).Output);
        Assert.Throws<OptionsSpecificationException>(() =>
            Parse([], "= tool\n[256,0,0]#bad#", "tool"));
    }

    [Fact]
    public void PipeInsideMarkupDoesNotStartDirectives()
    {
        const string text = "= tool\n-a *this | that* | maxOccurs=1\n-b [green]#left | right#";
        var (_, output, _) = Parse(["help"], text, "tool");
        Assert.Contains("this | that", output);
        Assert.Contains("left | right", output);
    }

    [Fact]
    public void EscapedOptionLinesDoNotCreateDefinitionsAndCommentsDisappear()
    {
        const string text = "= tool\n\\-a example only\n# hidden comment\n-b real option\n  continued description";
        var (result, output, _) = Parse(["help"], text, "tool");
        Assert.Contains("-a example only", output);
        Assert.Contains("real option continued description", output);
        Assert.DoesNotContain("hidden comment", output);
        Assert.Throws<ArgumentException>(() => result.IsOptionPresent('a'));
    }

    [Fact]
    public void FullDefinitionIsValidWithCrLfLineEndings()
    {
        var result = Parse(["Print", "-n1"], Definition.Replace("\n", "\r\n")).Result;
        Assert.True(result.IsValid);
        Assert.Equal(1, result.GetValue<int>("lines"));
    }

    [Fact]
    public void PrintHelpUsesAlreadySelectedCommand()
    {
        using var output = new StringWriter();
        var result = OptionsProcessor.FromText(Definition, "FileUtil", ["Print"],
            new OptionsProcessorSettings { Output = output, Error = TextWriter.Null, ColourMode = ColourMode.Never });
        Assert.Equal("", output.ToString());
        result.PrintHelp();
        Assert.Contains("--lines", output.ToString());
        Assert.DoesNotContain("CUSTOM OVERVIEW", output.ToString());
    }
}
