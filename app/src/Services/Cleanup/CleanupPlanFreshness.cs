namespace PCManager.Services.Cleanup;

/// <summary>
/// An approval is a statement about the scan, not a licence to delete whatever happens to be
/// there later. Before execution the folder is measured again; a material change invalidates
/// that row and the user must scan and approve again. The tolerances allow for ordinary log
/// churn and filesystem rounding without masking a real content change.
/// </summary>
public static class CleanupPlanFreshness
{
    private const long MinimumByteTolerance = 64 * 1024;
    private const double FractionalTolerance = 0.05;
    private const int MinimumFileTolerance = 3;

    public static bool IsStale(
        long expectedBytes,
        int expectedFiles,
        long actualBytes,
        int actualFiles)
    {
        // An inaccessible elevated target can legitimately measure 0/0 before and after.
        // There is no baseline to invalidate; the helper repeats its own checks.
        if (expectedBytes == 0 && expectedFiles == 0)
        {
            return false;
        }

        long byteTolerance = Math.Max(
            MinimumByteTolerance,
            (long)(expectedBytes * FractionalTolerance));

        if (Math.Abs(actualBytes - expectedBytes) > byteTolerance)
        {
            return true;
        }

        if (expectedFiles == 0)
        {
            return false;
        }

        int fileTolerance = Math.Max(
            MinimumFileTolerance,
            (int)Math.Ceiling(expectedFiles * FractionalTolerance));

        return Math.Abs(actualFiles - expectedFiles) > fileTolerance;
    }
}
