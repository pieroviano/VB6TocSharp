using System;
using System.Runtime.InteropServices;
using System.Threading;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Information;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModTextFiles;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

static class ModShell
{
    // Option Explicit
    public const int swHide = 0;
    public const int swShownormal = 1;
    public const int swShowminimized = 2;
    public const int swShowmaximized = 3;
    public const int swShow = 5;
    public const int swShowdefault = 10;
    public const int createNoWindow = 0x8000000;
    public const int infinite = -1;
    private static int lastProcessId = 0;
    private const string dirsep = "\\";
    public const int normalPriorityClass = 0x20;
    public enum EnSw
    {
        EnSwHide = 0,
        EnSwNormal = 1,
        EnSwMaximize = 3,
        EnSwMinimize = 6
    }
    public class Startupinfo
    {
        public int cb = 0;
        public string lpReserved = "";
        public string lpDesktop = "";
        public string lpTitle = "";
        public int dwX = 0;
        public int dwY = 0;
        public int dwXSize = 0;
        public int dwYSize = 0;
        public int dwXCountChars = 0;
        public int dwYCountChars = 0;
        public int dwFillAttribute = 0;
        public int dwFlags = 0;
        public int wShowWindow = 0;
        public int cbReserved2 = 0;
        public int lpReserved2 = 0;
        public int hStdInput = 0;
        public int hStdOutput = 0;
        public int hStdError = 0;
    }
    public class ProcessInformation
    {
        public int hProcess = 0;
        public int hThread = 0;
        public int dwProcessId = 0;
        public int dwThreadId = 0;
    }
    [DllImport("kernel32.dll")] private static extern void Sleep(int dwMilliseconds);
    [DllImport("kernel32.dll")] private static extern int CreateProcessA(int lpApplicationName, string lpCommandLine, int lpProcessAttributes, int lpThreadAttributes, int bInheritHandles, int dwCreationFlags, int lpEnvironment, int lpCurrentDirectory, ref Startupinfo lpStartupInfo, ref ProcessInformation lpProcessInformation);
    [DllImport("kernel32.dll")] private static extern int WaitForSingleObject(int hHandle, int dwMilliseconds);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(ref int hObject);
    [DllImport("user32.dll")] private static extern int GetDesktopWindow();
    [DllImport("shell32.dll", EntryPoint = "ShellExecuteA")] private static extern int ShellExecute(int hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);


    public static string RunCmdToOutput(string cmd, out string errStr, bool asAdmin = false)
    {
        // TODO (not supported): On Error GoTo RunError
        string c = "";

        var a = TempFile();
        var b = TempFile();
        if (!asAdmin)
        {
            ShellAndWait("cmd /c " + cmd + " 1> " + a + " 2> " + b, EnSw.EnSwHide);
        }
        else
        {
            c = TempFile("", "tmp_", ".bat");
            WriteFile(c, cmd + " 1> " + a + " 2> " + b, true);
            RunFileAsAdmin(c);
        }

        var iter = 0;
        const int maxIter = 10;
        while (true)
        {
            var tLen = FileLen(a);
            Sleep(800);
            if (iter > maxIter || FileLen(a) == tLen)
            {
                break;
            }
            iter = iter + 1;
        }
        var runCmdToOutput = ReadEntireFileAndDelete(a);
        if (iter > maxIter)
        {
            runCmdToOutput = runCmdToOutput + vbCrLf2 + "<<< OUTPUT TRUNCATED >>>";
        }
        errStr = ReadEntireFileAndDelete(b);
        DeleteFileIfExists(c);
        return runCmdToOutput;
    }

    /*
' to allow for Shell.
' This routine shells out to another application and waits for it to exit.
*/
    public static void ShellAndWait(string appToRun, EnSw sw = EnSw.EnSwNormal)
    {
        ProcessInformation nameOfProc = null;

        Startupinfo nameStart = null;

        int rc = 0;


        // TODO (not supported): On Error GoTo ErrorRoutineErr
        nameStart.cb = Len(nameStart);
        if (sw == EnSw.EnSwHide)
        {
            rc = CreateProcessA(0, appToRun, 0, 0, CInt(sw), createNoWindow, 0, 0, ref nameStart, ref nameOfProc);
        }
        else
        {
            rc = CreateProcessA(0, appToRun, 0, 0, CInt(sw), normalPriorityClass, 0, 0, ref nameStart, ref nameOfProc);
        }
        lastProcessId = nameOfProc.dwProcessId;
        rc = WaitForSingleObject(nameOfProc.hProcess, infinite);
        CloseHandle(ref nameOfProc.hProcess);
    }

    public static string TempFile(string useFolder = "", string usePrefix = "tmp_", string extension = ".tmp", bool testWrite = true)
    {
        if (useFolder != "" && !DirExists(useFolder))
        {
            useFolder = "";
        }
        if (useFolder == "")
        {
            useFolder = AppDomain.CurrentDomain.BaseDirectory + dirsep;
        }
        if (Right(useFolder, 1) != dirsep)
        {
            useFolder = useFolder + dirsep;
        }
        var fn = Replace(usePrefix + CDbl(DateTime.Now) +"_" + Thread.CurrentThread.ManagedThreadId + "_" + Random(999999), ".", "_");
        while (FileExists(useFolder + fn + ".tmp"))
        {
            fn = fn + Chr(Random(25) + Asc("a"));
        }
        var tempFile = useFolder + fn + extension;

        if (testWrite)
        {
            // TODO (not supported): On Error GoTo TestWriteFailed
            WriteFile(tempFile, "TEST", true, true);
            // TODO (not supported): On Error GoTo TestReadFailed
            var res = ReadFile(tempFile);
            if (res != "TEST")
            {
                MsgBox("Test write to temp file " + tempFile + " failed." + vbCrLf + "Result (Len=" + Len(res) + "):" + vbCrLf + res, vbCritical);
            }
            // TODO (not supported): On Error GoTo TestClearFailed
            System.IO.File.Delete(tempFile);
        }
        return tempFile;
    }

    public static void RunShellExecuteAdmin(string app, int nHwnd = 0, int windowState = swShownormal)
    {
        if (nHwnd == 0)
        {
            nHwnd = GetDesktopWindow();
        }
        lastProcessId = ShellExecute(nHwnd, "runas", app, vbNullString, vbNullString, windowState);
        //  ShellExecute nHwnd, "runas", App, Command & " /admin", vbNullString, SW_SHOWNORMAL
    }

    public static bool RunFileAsAdmin(string app, int nHwnd = 0, int windowState = swShownormal)
    {
        //  If Not IsWinXP Then
        RunShellExecuteAdmin(app, nHwnd, windowState);
        //  Else
        //    ShellOut App
        //  End If
        var runFileAsAdmin = true;
        return runFileAsAdmin;
    }
}