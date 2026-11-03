using System.Diagnostics;
using System.Runtime.InteropServices;

namespace chrelyonly_xray_win.Services;

internal static class ChildProcessTracker
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        JOBOBJECTINFOCLASS JobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    private static readonly IntPtr s_jobHandle;

    static ChildProcessTracker()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        s_jobHandle = CreateJobObject(IntPtr.Zero, null);
        if (s_jobHandle == IntPtr.Zero)
            return;

        var basicInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        };

        var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = basicInfo
        };

        int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
            SetInformationJobObject(
                s_jobHandle,
                JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                extendedInfoPtr,
                (uint)length);
        }
        catch
        {
            // Ignored
        }
        finally
        {
            Marshal.FreeHGlobal(extendedInfoPtr);
        }
    }

    public static void AddProcess(Process process)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && s_jobHandle != IntPtr.Zero && process != null && !process.HasExited)
        {
            try
            {
                AssignProcessToJobObject(s_jobHandle, process.Handle);
            }
            catch
            {
                // Ignored
            }
        }
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    private enum JOBOBJECTINFOCLASS
    {
        JobObjectExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }
}
