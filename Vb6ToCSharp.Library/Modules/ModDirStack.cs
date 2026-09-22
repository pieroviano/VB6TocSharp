using System;
using Microsoft.VisualBasic;
using static Microsoft.VisualBasic.FileSystem;


namespace Vb6ToCSharp.Modules;

public static class ModDirStack
{
    // Option Explicit
    private static Collection dirStack = null; // PushDir creates it with its "n" counter (an empty one made PushDir throw)


    public static string PushDir(string newDir, bool doSet = true)
    {
        //::::PushDir
        //:::SUMMARY
        //:Basic Directory Stack - Push cur dir to stack and CD to parameter.
        //:::DESCRIPTION
        //:1. Push Current Dir to stack
        //:2. CD to new folder.
        //:::PAREMETERS
        //: - sNewDir - String - Directory to CD into.
        //: - [doSet] = True - Boolean - Pass FALSE if you don't want to change current directory.
        //:::RETURNS
        //:Returns current directory.
        //:::SEE ALSO
        //: PopDir, PeekDir


        // TODO (not supported): On Error Resume Next
        if (dirStack == null)
        {
            dirStack = new Collection(); ;
            dirStack.Add(0, "n");
        }

        int n = Convert.ToInt32(dirStack.Item("n")) + 1;
        dirStack.Remove("n");
        dirStack.Add(n, "n");
        dirStack.Add(CurDir(), "_" + n);

        if (doSet)
        {
            ChDir(newDir);
        }

        var pushDir = CurDir();
        return pushDir;
    }

    public static string PopDir(bool doSet = true)
    {
        var popDir = "";
        //::::PopDir
        //:::SUMMARY
        //:Remove to dir from stack.  Error Safe.  Generally to change current directory.
        //:::DESCRIPTION
        //:1. Pop Dir from stack.
        //:2. CD to dir.
        //:::PAREMETERS
        //: - [doSet] = True - Boolean - Pass FALSE if you don't want to change current directory.
        //:::RETURNS
        //:Returns directory popped.
        //:::SEE ALSO
        //: PopDir, PeekDir


        // TODO (not supported): On Error Resume Next
        if (dirStack == null)
        {
            return popDir;

        }

        int n = Convert.ToInt32(dirStack.Item("n"));
        popDir = dirStack.Item("_" + n);

        if (n > 1)
        {
            n = n - 1;
            dirStack.Remove("n");
            dirStack.Add(n, "n");
        }
        else
        {
            dirStack = null;
        }

        if (doSet)
        {
            ChDir(popDir);
        }
        return popDir;
    }

    public static string PeekDir(bool doSet = true)
    {
        var peekDir = "";
        //::::PeekDir
        //:::SUMMARY
        //:Return directory on top of stack without removing it.  Generally to change current directory.
        //:::DESCRIPTION
        //:1. Push Current Dir to stack
        //:2. CD to new folder.
        //:::PAREMETERS
        //: - [doSet] = True - Boolean - Pass FALSE if you don't want to change current directory.
        //:::RETURNS
        //:Returns top stack item (without removing it from stack).
        //:::SEE ALSO
        //: PopDir, PeekDir


        // TODO (not supported): On Error Resume Next
        if (dirStack == null)
        {
            return peekDir;
        }

        int n = Convert.ToInt32(dirStack.Item("n"));
        peekDir = dirStack.Item("_" + n);

        if (doSet)
        {
            ChDir(peekDir);
        }
        return peekDir;
    }
}