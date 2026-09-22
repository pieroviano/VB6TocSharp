using System;
using static Microsoft.VisualBasic.Constants;
using static Microsoft.VisualBasic.FileSystem;
using static Microsoft.VisualBasic.Interaction;
using static Microsoft.VisualBasic.Strings;
using static Vb6ToCSharp.Modules.ModUtils;
using static Vb6ToCSharp.VbExtension;


namespace Vb6ToCSharp.Modules;

static class ModTextFiles
{
    // Option Explicit
    //@NO-LINT-DEPR
    //::::modTextFiles
    //:::SUMMARY
    //: A processing module for text files.
    //:
    //:::DESCRIPTION
    //: Straight-forward, disposable methods for using text files.  Drastically reduces the complexity required to interact
    //: with flat text files, abstracting the developer.
    //:
    //:::INTERFACE
    //::Public Interface
    //:- ReadFile
    //:- WriteFile
    //:- CountLines
    //:- VBFileCountLines
    //:- VBFileCountLines_Stat
    //:- ReadEntireFile
    //:- ReadEntireFileAndDelete
    //:- TailFile
    //:- HeadFile
    //:
    //:::SEE ALSO
    //:    - modXML, modCSV, modPath
    private static dynamic mFso = null;


    static dynamic Fso
    {
        get
        {
            if (mFso == null)
            {
                mFso = CreateObject("Scripting.FileSystemObject");
            }
            var fso = mFso;

            return fso;
        }
    }


    public static bool DeleteFileIfExists(string sFIle, bool bNoAttributeClearing = false)
    {
        bool deleteFileIfExists = false;
        // TODO (not supported): On Error Resume Next
        if (!FileExists(sFIle))
        {
            return deleteFileIfExists;

        }
        if (!bNoAttributeClearing)
        {
            SetAttr(sFIle, 0);
        }
        if (FileExists(sFIle))
        {
            System.IO.File.Delete(sFIle);
        }
        //  DeleteFileIfExists = FileExists(sFile)
        deleteFileIfExists = true;
        return deleteFileIfExists;
    }

    public static string ReadEntireFile(string tFileName)
    {
        string readEntireFile =
            //::::ReadEntireFile
            //:::SUMMARY
            //:Read an entire file.
            //:::DESCRIPTION
            //:Reads  the full contents of a file and returns the value as a string (without modification).
            //:::PARAMETERS
            //:- tFileName - The name of the file to read.
            //:::RETURN
            //:  String - The string contents of the file.
            //:::SEE ALSO
            //:  ReadFile, WriteFile, ReadEntireFileAndDelete
            // TODO (not supported): On Error Resume Next
            Fso.OpenTextFile(tFileName, 1).ReadAll;

        if (FileLen(tFileName) / 10 != Len(readEntireFile) / 10)
        {
            MsgBox("ReadEntireFile was short: " + FileLen(tFileName) + " vs " + Len(readEntireFile));
        }

        //  Dim intFile As Long
        //  intFile = FreeFile
        //On Error Resume Next
        //  Open tFileName For Input As #intFile
        //  ReadEntireFile = Input$(LOF(intFile), #intFile)  '  LOF returns Length of File
        //  Close #intFile
        return readEntireFile;
    }

    public static string ReadEntireFileAndDelete(string tFileName)
    {
        string readEntireFileAndDelete = "";
        //::::ReadEntireFileAndDelete
        //:::SUMMARY
        //:Read an entire file and safely delete it..
        //:::DESCRIPTION
        //:Reads the full contents of the file and then safely deletes it.
        //:
        //:If the file does not exist, no error is thrown, and an empty string is returned.
        //:::PARAMETERS
        //:- tFileName - The name of the file to read.
        //:::RETURN
        //:  String - The string contents of the file.
        //:::SEE ALSO
        //:  ReadEntireFile

        // TODO (not supported): On Error Resume Next
        try
        {
            readEntireFileAndDelete = ReadEntireFile(tFileName);
            System.IO.File.Delete(tFileName);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        return readEntireFileAndDelete;
    }

    private static string cacheFileName = "";
    private static DateTime cacheFileDate;
    private static string[] cacheFileLoad = null;

    public static string ReadFile(string tFileName, int startline = 1, int numLines = 0)
    {//, Optional ByRef WasEOF As Boolean = False)
        string readFile = "";
        //::::ReadFile
        //:::SUMMARY
        //:Random Access Read a given file based on line number.
        //:::DESCRIPTION
        //:Reads the specified lines from a given file.
        //:
        //:If the file does not exist, no error is thrown, and an empty string is returned.
        //:::PARAMETERS
        //:- tFileName - The name of the file to read.
        //:- StartLine - The line number to begin reading (the first line is 1).  If you try to read beyond the end of the file, an empty string is returned.
        //:- NumLines - If passed, attempts to read the specified number of lines.  Reading beyond the end of the file simply returns as many lines as possible.  Zero means read rest of file.  Default is zero.
        //:- WasEOF - If EOF checking is required, this ByRef parameter can be passed and checked later.  True if the file's EOF was reached.  False otherwise.
        //:::RETURN
        //:  String - The string contents of the file.
        //:::SEE ALSO
        //:  ReadEntireFile, WriteFile, CountLines, TailFile, HeadFile
        int fNum = 0;
        string line = "";
        int lineNum = 0;
        int count = 0;


        if (tFileName == "" || !FileExists(tFileName))
        {
            //    WasEOF = True
            return readFile;

        }

        if (tFileName == cacheFileName)
        {
            if (FileDateTime(tFileName) != cacheFileDate)
            {
                cacheFileName = "";
            }
        }

        if (tFileName != cacheFileName)
        {
            cacheFileName = tFileName;
            cacheFileDate = FileDateTime(tFileName);
            cacheFileLoad = Split(Replace(ReadEntireFile(tFileName), vbLf, ""), vbCr);
        }

        if (startline == 1 && numLines == 0)
        {
            readFile = Join(cacheFileLoad, vbCrLf);
        }
        else
        {
            readFile = Join(SubArr(cacheFileLoad, startline - 1, numLines), vbCrLf);
            //    ReadFile = LineByNumber(CacheFileLoad, Startline, NumLines)
        }

        return readFile;


        //  If Startline < 1 Then Startline = 1
        //  LineNum = 0
        //  FNum = FreeFile
        //  Open tFileName For Input As #FNum
        //  Do While Not EOF(FNum)
        //    LineNum = LineNum + 1
        //    Line Input #FNum, Line
        //    If LineNum >= Startline Then
        //      ReadFile = ReadFile & IIf(Len(ReadFile) > 0, vbCrLf, "") & Line
        //      Count = Count + 1
        //    End If
        //    If NumLines > 0 And Count >= NumLines Then GoTo Done
        //'    DoEvents
        //  Loop
        //'  WasEOF = True
        //Done:
        //  Close #FNum
        return readFile;
    }

    public static int CountFileLines(string sourceFile, bool ignoreBlank = false, string ignorePrefix = "")
    {
        var countFileLines =
            //::::CountFileLines
            //:::SUMMARY
            //:Returns the number of lines in a given file.
            //:::DESCRIPTION
            //:Retruns the number of lines in a file, based on the number of vbCr characters.
            //:
            //:- vbLf is completely ignored.
            //:- Blank lines can be optionally ignored
            //:- A prefix (such as # or ') can also be omitted from the count.
            //:
            //:If the file does not exist, no error is thrown, and an empty string is returned.
            //:::PARAMETERS
            //:- Source - The name of the file to read.
            //:- IgnoreBlank - Ignore blank lines in count.  Set to False to count all lines.  Default == TRUE
            //:- IgnorePrefix - Specify a string prefix to ignore in the count.  Popular options are the VB comment character (') and the utility file comment character (#).
            //:::RETURN
            //:  Long - The number of lines.
            //:::SEE ALSO
            //:  WriteFile, ReadFile, VBFileCountLines, CountLines
            CountLines(ReadEntireFile(sourceFile), ignoreBlank, ignorePrefix);
        return countFileLines;
    }

    public static int CountLines(string source, bool ignoreBlank = true, string ignorePrefix = "'")
    {
        int countLines = 0;
        //::::CountLines
        //:::SUMMARY
        //:Returns the number of lines in a given string (not a file).
        //:::DESCRIPTION
        //:Retruns the number of lines in a string, based on the number of vbCr characters.
        //:
        //:- vbLf is completely ignored.
        //:- Blank lines can be optionally ignored
        //:- A prefix (such as # or ') can also be omitted from the count.
        //:
        //:If the file does not exist, no error is thrown, and an empty string is returned.
        //:::PARAMETERS
        //:- Source - The string to count lines in.
        //:- IgnoreBlank - Ignore blank lines in count.  Set to False to count all lines.  Default == TRUE
        //:- IgnorePrefix - Specify a string prefix to ignore in the count.  Popular options are the VB comment character (') and the utility file comment character (#).
        //:::RETURN
        //:  Long - The number of lines.
        //:::SEE ALSO
        //:  WriteFile, ReadFile, VBFileCountLines, CountFileLines, LineByNumber

        source = Replace(source, vbLf, "");
        foreach (var iterL in Split(source, vbCr))
        {
            dynamic l = iterL;
            if (Trim(l) == "" & ignoreBlank)
            {
                // Don't count...
            }
            else if (ignorePrefix != "" && Left(LTrim(l), Len(ignorePrefix)) == ignorePrefix)
            {
                // Don't count...
            }
            else
            {
                countLines = countLines + 1;
            }
        }
        return countLines;
    }

    public static string LineByNumber(string source, int startline, int numLinesUnused = 0, string nl = vbCrLf)
    {
        string lineByNumber = "";
        //::::LineByNumber
        //:::SUMMARY
        //:Returns the line(s) specified by the <StartLine> and <NumLines> parameters from a given <Source> string.
        //:::DESCRIPTION
        //:Similar to ReadFile, but for a string.
        //:
        //:If the file does not exist, no error is thrown, and an empty string is returned.
        //:
        //:- Reading before or end of multi-line string returns empty string.
        //:- Reading from center of lines beyond end of lines returns as many lines as possible.
        //:- Passing <NumLines> set to zero (0) returns remainder of lines (if any).
        //:::PARAMETERS
        //:- Source - The string to count lines in.
        //:- Startline - Ignore blank lines in count.  Set to False to count all lines.  Default == TRUE
        //:- NumLines - Specify a string prefix to ignore in the count.  Popular options are the VB comment character (') and the utility file comment character (#).
        //:- NL - The New Line charater(s) to use.  Default = vbCrLf
        //:::RETURN
        //:  String - The string at the specified location.
        //:::SEE ALSO
        //:  WriteFile, ReadFile, VBFileCountLines, CountFileLines, CountLines

        int I = 0;

        var a = 0;
        if (startline <= 0)
        {
            startline = 1;
        }

        if (startline == 1)
        {
            a = 1;
        }
        else
        {
            for (I = 1; I <= startline - 1; I++)
            {
                a = InStr(a + 1, source, nl);
                if (a == 0)
                {
                    return lineByNumber;

                }
            }
            a = a + Len(nl);
        }

        var b = a;
        if (Left(Mid(source, a), Len(nl)) != nl)
        {
            for (I = 1; I <= numLinesUnused; I++)
            {
                b = InStr(b + 1, source, nl);
                if (b == 0)
                {
                    lineByNumber = Mid(source, a);
                    return lineByNumber;

                }
            }
        }

        lineByNumber = Mid(source, a, b - a);
        return lineByNumber;
    }

    public static bool VbFileCountLines(string tFileName, ref int totl, ref int code, ref int blnk, ref int cmnt)
    {
        bool vbFileCountLines = false;
        //::::VBFileCountLines
        //:::SUMMARY
        //:Count lines in a VB6 file.
        //:::DESCRIPTION
        //:Count number of lines in a VB6 file.  Specifically tailored to account for the given parameters for VB6 code files.
        //:
        //:Returns the total line count, plus a breakdown of the following:
        //:- Code - Non-blank, non-comment-starting.
        //:- Blank - Count of blank lines.
        //:- Comment - Count of lines which are 100% comment (first character is ').
        //:
        //:If the file does not exist, no error is thrown, and an empty string is returned.
        //:::PARAMETERS
        //:- tFileName - The name of the file to read.
        //:- [Totl] - ByRef.  Returns total number of lines in file.
        //:- [Code] - ByRef.  Returns total number of code lines in file.
        //:- [Blnk] - ByRef.  Returns total number of blank lines in file.
        //:- [Cmnt] - ByRef.  Returns total number of comment lines in file.
        //:::RETURN
        //:  String - The string contents of the file.
        //:::SEE ALSO
        //:  ReadEntireFile, WriteFile, CountLines, VBFileCountLines_Stat

        totl = 0;
        code = 0;
        blnk = 0;
        cmnt = 0;

        // TODO (not supported): On Error Resume Next
        if (!FileExists(tFileName))
        {
            return vbFileCountLines;

        }
        var s = ReadEntireFile(tFileName);
        totl = CountLines(s, false, "");
        code = CountLines(s);
        var n = CountLines(s, true, "");
        cmnt = n - code;
        blnk = totl - n;
        vbFileCountLines = true;
        return vbFileCountLines;
    }

    public static void VBFileCountLines_Stat(string tFileName)
    {
        //::::VBFileCountLines_Stat
        //:::SUMMARY
        //:Print line count statistics for a file.
        //:::DESCRIPTION
        //:Raises a message box showing the file line count numbers.
        //:
        //:::PARAMETERS
        //:- tFileName - The name of the file to read.
        //:::SEE ALSO
        //:  ReadEntireFile, WriteFile, CountLines, VBFileCountLines
        int T = 0;
        int c = 0;
        int b = 0;
        int m = 0;

        if (VbFileCountLines(tFileName, ref T, ref c, ref b, ref m))
        {
            MsgBox("File Line Stat: " + vbCrLf + " Totl: " + T + vbCrLf + "Code: " + c + vbCrLf + "Blnk: " + b + vbCrLf + "Cmnt: " + m, vbMsgBoxRtlReading);
        }
        else
        {
            MsgBox("File Not Found: " + tFileName);
        }
    }

    public static bool WriteFile(string file, string str, bool overWrite = false, bool preventNl = false)
    {
        //::::WriteFile
        //:::SUMMARY
        //:Write the given string to a file.
        //:::DESCRIPTION
        //:Writes a given text string to a file.
        //:
        //:Text may or may not contain new lines (multi-line write supported).
        //:
        //:A New-line is appended by default if not specified in thes tring.
        //:::PARAMETERS
        //:- File - The name of the file to read.
        //:- str - The text to write to the file.  Can be an empty string (blank line).
        //:- [OverWrite] - Default is to append.  Set to TRUE to delete file before write (overwrite contents).
        //:- [PreventNL] - By default, the end of the string is checked for a new line.  Use this to write to a file without a new-line.
        //:::RETURN
        //:  Boolean - Returns True.
        //:::SEE ALSO
        //:  ReadEntireFile, WriteFile, CountLines

        var fNo =
            // TODO (not supported): On Error Resume Next
            FreeFile(); ;
        if (overWrite)
        {
            System.IO.File.Delete(file);
            VBOpenFile(fNo, file); ;
        }
        else
        {
            VBOpenFile(fNo, file); ;
        }
        if (preventNl || Right(str, 2) == vbCrLf)
        {
            VBWriteFile(fNo, str); ;
        }
        else
        {
            VBWriteFile(fNo, str); ;
        }
        VBCloseFile(fNo);
        var writeFile = true;
        return writeFile;
    }
}