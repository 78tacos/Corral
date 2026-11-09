using System;
using System.Collections.Generic;
using Xunit;

namespace CorralTests;

/// <summary>
/// Shortcut import, the LinkStore checks that need no shortcut, and the settings file each
/// have their own test, so one failure cannot hide the others. Each LinkStore check that
/// needs a <c>.lnk</c> is its own case and is skipped when <c>WScript.Shell</c> is unavailable.
/// On Windows those cases share a single run of that group.
/// </summary>
public sealed class CorralChecks
{
    private static readonly Lazy<bool> LnkAvailable = new(LinkStoreChecks.CanCreateLnk);

    /// <summary>Runs the .lnk group once, the first time a case actually executes it.</summary>
    private static readonly Lazy<CheckRunner> LnkSuite = new(RunLnkSuite);

    public static IEnumerable<object[]> LnkCheckNames()
    {
        foreach (string name in LinkStoreChecks.LnkCheckNames)
            yield return new object[] { name };
    }

    [Fact]
    public void ShortcutImport()
    {
        var runner = new CheckRunner();
        ShortcutImportTests.Run(runner.Check);
        AssertChecks(runner, expectedPassed: 7);
    }

    [Fact]
    public void LinkStoreChecksThatDoNotNeedLnk()
    {
        var runner = new CheckRunner();
        LinkStoreChecks.RunWithoutLnk(runner);
        AssertChecks(runner, LinkStoreChecks.ShellFreeCheckNames.Length);
    }

    [SkippableTheory]
    [MemberData(nameof(LnkCheckNames))]
    public void LinkStoreCheckThatNeedsLnk(string name)
    {
        Skip.IfNot(LnkAvailable.Value, LinkStoreChecks.SkipReason + ": " + name);
        CheckRunner runner = LnkSuite.Value;
        Assert.True(runner.Failed == 0, name + Environment.NewLine + runner.FailureDetails);
        Assert.Equal(LinkStoreChecks.LnkCheckNames.Length, runner.Passed);
    }

    [Fact]
    public void SettingsFile()
    {
        var runner = new CheckRunner();
        ConfigTests.Run(runner.Check);
        AssertChecks(runner, expectedPassed: 18);
    }

    private static CheckRunner RunLnkSuite()
    {
        var runner = new CheckRunner();
        LinkStoreChecks.RunRequiringLnk(runner);
        return runner;
    }

    private static void AssertChecks(CheckRunner runner, int expectedPassed)
    {
        Assert.True(runner.Failed == 0, runner.FailureDetails);
        Assert.Equal(expectedPassed, runner.Passed);
        Assert.Equal(0, runner.Skipped);
    }
}
