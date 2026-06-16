using System.Diagnostics;

namespace DamascusUI;

internal static class MacFocus
{
    public static bool? TryIsFocusLikelyActive()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        // Best-effort heuristic:
        // `NSStatusItem Visible FocusModes` is commonly used by macOS tooling to infer
        // whether Focus/DND is active. It is not a perfect public API, so callers should
        // treat `true` as "likely active".
        var output = TryRun("/usr/bin/defaults", "read com.apple.controlcenter 'NSStatusItem Visible FocusModes'");
        if (output is "1")
        {
            return true;
        }

        if (output is "0")
        {
            return false;
        }

        return null;
    }

    private static string? TryRun(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(1000);
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
