# Lance une commande sur un bureau Windows invisible (« RempartTest ») : la fenêtre du jeu n'apparaît jamais sur le bureau de
# l'utilisateur (ni au premier plan, ni dans la barre des tâches), mais le rendu fonctionne (captures d'écran des tests).
# Usage : powershell -File tools/hidden_run.ps1 -Exe <chemin> -Args "<arguments>" [-Log <fichier>] [-TimeoutSec 600]
# Sortie standard et erreurs du programme : redirigées vers -Log (cmd /c ... > log 2>&1).
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Args = "",
    [string]$Log = "",
    [string]$WorkDir = (Get-Location).Path,
    [int]$TimeoutSec = 900
)

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class HiddenDesktop
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO
    {
        public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr devmode, int flags, uint access, IntPtr sa);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool CloseDesktop(IntPtr h);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcess(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string dir,
        ref STARTUPINFO si, out PROCESS_INFORMATION pi);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(IntPtr h, uint ms);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetExitCodeProcess(IntPtr h, out uint code);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool TerminateProcess(IntPtr h, uint code);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);

    public static int Run(string cmdLine, string dir, int timeoutMs)
    {
        const uint GENERIC_ALL = 0x10000000;
        var desk = CreateDesktop("RempartTest", IntPtr.Zero, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero);
        if (desk == IntPtr.Zero) throw new Exception("CreateDesktop: " + Marshal.GetLastWin32Error());
        var si = new STARTUPINFO { cb = Marshal.SizeOf(typeof(STARTUPINFO)), lpDesktop = "RempartTest", dwFlags = 1, wShowWindow = 7 };
        PROCESS_INFORMATION pi;
        // CREATE_NO_WINDOW (pas de console visible) et BELOW_NORMAL_PRIORITY_CLASS (le poste de travail reste fluide).
        if (!CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, false, 0x08000000 | 0x00004000, IntPtr.Zero, dir, ref si, out pi))
            throw new Exception("CreateProcess: " + Marshal.GetLastWin32Error());
        var r = WaitForSingleObject(pi.hProcess, (uint)timeoutMs);
        if (r != 0) TerminateProcess(pi.hProcess, 1);
        uint code;
        GetExitCodeProcess(pi.hProcess, out code);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        CloseDesktop(desk);
        return (int)code;
    }
}
"@

$cmd = "`"$Exe`" $Args"
if ($Log -ne "") { $cmd = "cmd.exe /c `"`"$Exe`" $Args > `"$Log`" 2>&1`"" }
$code = [HiddenDesktop]::Run($cmd, $WorkDir, $TimeoutSec * 1000)
exit $code
