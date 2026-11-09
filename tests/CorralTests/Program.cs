using System;

namespace CorralTests;

/// <summary>
/// Console entry point. <c>dotnet test</c> runs the same groups through <see cref="CorralChecks"/>
/// and does not call <see cref="Main"/>. A failure in one group does not stop the others.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        var runner = new CheckRunner();
        RunGroup(runner, "shortcut import", () => ShortcutImportTests.Run(runner.Check));
        RunGroup(runner, "LinkStore checks that do not need .lnk", () => LinkStoreChecks.RunWithoutLnk(runner));
        RunGroup(runner, "LinkStore .lnk checks", () =>
        {
            if (LinkStoreChecks.CanCreateLnk())
                LinkStoreChecks.RunRequiringLnk(runner);
            else
                LinkStoreChecks.SkipRequiringLnk(runner);
        });
        RunGroup(runner, "settings file", () => ConfigTests.Run(runner.Check));
        runner.WriteSummary();
        return runner.Failed == 0 ? 0 : 1;
    }

    private static void RunGroup(CheckRunner runner, string name, Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            runner.Check(name + " group", false, ex.Message);
        }
    }
}

/// <summary>Counts PASS, FAIL, and SKIP lines for one run of the checks.</summary>
internal sealed class CheckRunner
{
    public int Passed { get; private set; }
    public int Failed { get; private set; }
    public int Skipped { get; private set; }
    public string FailureDetails { get; private set; } = "";

    public void Check(string name, bool ok, string detail = "")
    {
        if (ok)
        {
            Passed++;
        }
        else
        {
            Failed++;
            string line = detail == "" ? name : name + "  -> " + detail;
            FailureDetails = FailureDetails.Length == 0 ? line : FailureDetails + Environment.NewLine + line;
        }

        Console.WriteLine("{0,-4} {1}{2}", ok ? "PASS" : "FAIL", name, ok || detail == "" ? "" : "  -> " + detail);
    }

    public void Skip(string name, string reason)
    {
        Skipped++;
        Console.WriteLine("{0,-4} {1}  -> {2}", "SKIP", name, reason);
    }

    public void WriteSummary()
    {
        Console.WriteLine();
        Console.WriteLine("{0} passed, {1} failed, {2} skipped", Passed, Failed, Skipped);
    }
}
