using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PCManager.Helper;

/// <summary>
/// Builds the helper's single pipe endpoint.
/// <para>
/// <see cref="PipeOptions.CurrentUserOnly"/> is not usable here: on Windows it writes a descriptor
/// whose owner and sole ACE are the creating token's owner. An elevated token's owner is
/// BUILTIN\Administrators and the pipe inherits the High integrity label, so the unelevated app -
/// which runs as the same signed-in user with an administrator-filtered token - is refused even
/// though the account is identical. The explicit descriptor instead grants full control to the
/// user's own SID and labels the pipe Medium with no-write-up, which keeps the endpoint private to
/// that user while letting their unelevated app write one request.
/// </para>
/// </summary>
internal static class PipeEndpoint
{
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x00000002;
    private const int ErrorNotAllAssigned = 1300;

    internal static NamedPipeServerStream Create(string pipeName)
    {
        string userSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("The signed-in user's SID could not be resolved.");

        // Creating the pipe with a mandatory label needs SeSecurityPrivilege, which an elevated
        // administrator holds but Windows starts disabled.
        EnablePrivilege("SeSecurityPrivilege");

        var security = new PipeSecurity();

        // D:P  protected DACL, so no inherited ACE can widen it
        // A    full control for the signed-in user's SID only
        // S:(ML;;NW;;;ME)  Medium integrity label with no-write-up
        security.SetSecurityDescriptorSddlForm(
            $"D:P(A;;FA;;;{userSid})S:(ML;;NW;;;ME)",
            AccessControlSections.Access | AccessControlSections.Audit);

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 4096,
            outBufferSize: 4096,
            pipeSecurity: security);
    }

    private static void EnablePrivilege(string name)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out IntPtr token))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Could not open the helper's process token to enable {name}.");
        }

        try
        {
            if (!LookupPrivilegeValue(null, name, out Luid luid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not look up {name}.");
            }

            var state = new TokenPrivileges
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SePrivilegeEnabled
            };

            if (!AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not enable {name}.");
            }

            // AdjustTokenPrivileges reports success even when the privilege was not assigned.
            int error = Marshal.GetLastWin32Error();

            if (error == ErrorNotAllAssigned)
            {
                throw new Win32Exception(error, $"The helper's token does not hold {name}.");
            }
        }
        finally
        {
            CloseHandle(token);
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        bool disableAllPrivileges,
        ref TokenPrivileges newState,
        int bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }
}
