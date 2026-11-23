using System.Runtime.InteropServices;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Interop;

namespace PCManager.Services.Cleaners;

/// <summary>
/// Sends one file to the Recycle Bin with SHFileOperation + FOF_ALLOWUNDO. No permanent delete
/// path exists here; the shell move is the only operation.
/// </summary>
public sealed class ShellFileRecycler : IFileRecycler
{
    public OperationResult Recycle(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return OperationResult.Fail("The file no longer exists.");
        }

        try
        {
            var operation = new NativeMethods.SHFILEOPSTRUCT
            {
                wFunc = NativeMethods.FO_DELETE,
                // pFrom must be double-null terminated; the marshaler adds the final null.
                pFrom = path + "\0",
                fFlags = NativeMethods.FOF_ALLOWUNDO
                         | NativeMethods.FOF_NOCONFIRMATION
                         | NativeMethods.FOF_SILENT
                         | NativeMethods.FOF_NOERRORUI
            };

            int result = NativeMethods.SHFileOperation(ref operation);

            if (result != 0)
            {
                return OperationResult.Fail(
                    $"Windows refused to recycle the file (shell error 0x{result:X}).");
            }

            if (operation.fAnyOperationsAborted || File.Exists(path))
            {
                return OperationResult.Fail("Windows did not move the file to the Recycle Bin.");
            }

            return OperationResult.Ok("Moved to the Recycle Bin.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}
