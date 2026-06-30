using System.Diagnostics;

public static class BuildInfo
{
    public static readonly string Branch = ResolveBranch();
    public static readonly string Commit = ResolveCommit();

    private static string ResolveBranch()
    {
        var branch = Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
                  ?? Environment.GetEnvironmentVariable("GIT_BRANCH");

        if (branch is not null)
            return branch;

        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi)!;
            var result = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && result is not ("" or "HEAD") ? result : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string ResolveCommit()
    {
        var version = ThisAssembly.AssemblyInformationalVersion;

        var dashG = version.IndexOf("-g", StringComparison.Ordinal);
        if (dashG > 0) return version[(dashG + 2)..];

        var plus = version.IndexOf('+', StringComparison.Ordinal);
        if (plus > 0) return version[(plus + 1)..];

        return "unknown";
    }
}
