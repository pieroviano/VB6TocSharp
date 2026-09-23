using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Vb6ToCSharp.Runtime;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.ItemConversion.TextFiles;
using static Vb6ToCSharp.ItemConversion.ConversionUtility;
using static Vb6ToCSharp.Runtime.RuntimeExtension;


namespace Vb6ToCSharp.Parsing;

public static class ShellHandler
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

    [DllImport("kernel32.dll")] private static extern void Sleep(int dwMilliseconds);
    [DllImport("user32.dll")] private static extern int GetDesktopWindow();
    [DllImport("shell32.dll", EntryPoint = "ShellExecuteA")] private static extern int ShellExecute(int hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);


    public static string RunCmdToOutput(string cmd, out string errStr, bool asAdmin = false)
    {
        // TODO (not supported): On Error GoTo RunError
        var c = "";

        if (!asAdmin)
        {
            // capture the output directly (was: temp-file redirection through a broken CreateProcess P/Invoke)
            using (var p = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + cmd)
                   {
                       UseShellExecute = false,
                       CreateNoWindow = true,
                       RedirectStandardOutput = true,
                       RedirectStandardError = true,
                       WorkingDirectory = System.IO.Directory.GetCurrentDirectory()
                   }))
            {
                lastProcessId = p.Id;
                var errTask = p.StandardError.ReadToEndAsync();
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                errStr = errTask.Result;
                return output;
            }
        }

        // "runas" cannot redirect: the elevated batch writes to temp files that are polled below
        var a = TempFile();
        var b = TempFile();
        c = TempFile("", "tmp_", ".bat");
        WriteFile(c, cmd + " 1> " + Quote(a) + " 2> " + Quote(b), true);
        RunFileAsAdmin(c);

        var iter = 0;
        const int maxIter = 10;
        while (true)
        {
            var tLen = FileExists(a) ? FileLen(a) : -1;
            Sleep(800);
            if (iter > maxIter || (FileExists(a) ? FileLen(a) : -1) == tLen)
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
    public static void ShellAndWait(string appToRun, ShowType sw = ShowType.Normal)
    {
        // the CreateProcessA P/Invoke passed null (class) structs with 32-bit handles and always threw
        SplitCommandLine(appToRun, out var exe, out var args);
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = sw == ShowType.Hide,
            WindowStyle = sw == ShowType.Hide ? ProcessWindowStyle.Hidden
                : sw == ShowType.Maximize ? ProcessWindowStyle.Maximized
                : sw == ShowType.Minimize ? ProcessWindowStyle.Minimized
                : ProcessWindowStyle.Normal
        };
        using (var p = Process.Start(psi))
        {
            lastProcessId = p.Id;
            p.WaitForExit();
        }
    }

    // "exe" args  |  exe args  (CreateProcess command-line convention)
    internal static void SplitCommandLine(string commandLine, out string exe, out string args)
    {
        commandLine = Trim(commandLine);
        int end;
        if (Left(commandLine, 1) == "\"")
        {
            end = InStr(2, commandLine, "\"");
            end = end == 0 ? Len(commandLine) : end;
            exe = Mid(commandLine, 2, Math.Max(0, end - 2));
        }
        else
        {
            end = InStr(commandLine, " ");
            end = end == 0 ? Len(commandLine) : end - 1;
            exe = Left(commandLine, end);
        }
        args = Trim(Mid(commandLine, end + 1));
    }

    public static string TempFile(string useFolder = "", string usePrefix = "tmp_", string extension = ".tmp", bool testWrite = true)
    {
        if (useFolder != "" && !DirExists(useFolder))
        {
            useFolder = "";
        }
        if (useFolder == "")
        {
            useFolder = AppDomain.CurrentDomain.BaseDirectory; // already ends with a separator
        }
        if (Right(useFolder, 1) != dirsep)
        {
            useFolder = useFolder + dirsep;
        }
        var fn = Replace(usePrefix + CDbl(DateTime.Now).ToString(CultureInfo.InvariantCulture) + "_" + Thread.CurrentThread.ManagedThreadId + "_" + Random(999999), ".", "_");
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
            var res = ReadEntireFile(tempFile); // ReadFile caches by name+timestamp and can return stale text
            if (res != "TEST")
            {
                Notify("Test write to temp file " + tempFile + " failed." + vbCrLf + "Result (Len=" + Len(res) + "):" + vbCrLf + res);
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