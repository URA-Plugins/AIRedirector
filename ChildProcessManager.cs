using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AIRedirector;

/// <summary>
/// 使用 Windows Job Object 管理子进程的生命周期。
/// 将子进程绑定到当前进程的 Job Object 后，
/// 当父进程退出（包括异常退出）时，所有子进程会被自动终止。
/// </summary>
internal sealed class ChildProcessManager : IDisposable
{
    private readonly nint _jobHandle;
    private bool _disposed;

    public ChildProcessManager()
    {
        _jobHandle = CreateJobObject(nint.Zero, null);
        if (_jobHandle == nint.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法创建 Job Object");

        // 配置 Job Object：当最后一个句柄关闭时，终止所有关联进程
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        nint infoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, infoPtr, false);
            if (!SetInformationJobObject(_jobHandle, JobObjectInfoType.ExtendedLimitInformation, infoPtr, (uint)length))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法设置 Job Object 信息");
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }
    }

    /// <summary>
    /// 将已启动的进程添加到 Job Object 中。
    /// 进程必须已经启动（即已拥有有效的进程句柄）。
    /// </summary>
    public void AddProcess(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!AssignProcessToJobObject(_jobHandle, process.Handle))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法将进程 {process.Id} 分配到 Job Object");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_jobHandle != nint.Zero)
            CloseHandle(_jobHandle);
    }

    #region P/Invoke

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObject(nint lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(nint hJob, JobObjectInfoType infoType, nint lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    private enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public long Affinity;
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
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    #endregion
}
