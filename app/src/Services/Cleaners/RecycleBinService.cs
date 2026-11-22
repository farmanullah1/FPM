using System.Runtime.InteropServices;
using PCManager.Services.Abstractions;
using PCManager.Services.Interop;

namespace PCManager.Services.Cleaners;

/// <summary>
/// Recycle bin helper built on shell32. SHQueryRecycleBin returns the total size and
/// item count; SHEmptyRecycleBin performs the actual purge without showing a dialog.
/// </summary>
public sealed class RecycleBinService : IRecycleBinService
{
    public long GetSize()
    {
        var info = new NativeMethods.SHQUERYRBINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.SHQUERYRBINFO>()
        };

        // Passing null queries the bin of all drives.
        int hr = NativeMethods.SHQueryRecycleBin(null, ref info);
        return hr == 0 ? info.i64Size : 0;
    }

    public long Empty()
    {
        long before = GetSize();

        const uint flags = NativeMethods.SHERB_NOCONFIRMATION
                           | NativeMethods.SHERB_NOPROGRESSUI
                           | NativeMethods.SHERB_NOSOUND;

        int hr = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, flags);

        // Some shells report a failure code even after emptying; the measured
        // difference is the only trustworthy number, so the result is ignored on purpose.
        long after = GetSize();
        long freed = Math.Max(0, before - after);
        return hr == 0 || freed > 0 ? Math.Max(freed, 0) : 0;
    }
}
