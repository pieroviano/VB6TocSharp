using System;
using static Vb6ToCSharp.Runtime.VbConstants;
using static Vb6ToCSharp.Runtime.VbInteraction;
using static Vb6ToCSharp.Runtime.VbStrings;
using static Vb6ToCSharp.Runtime.RuntimeExtension;
using static Vb6ToCSharp.Infrastructure.DirStack;
using static Vb6ToCSharp.Infrastructure.ShellHandler;
using static Vb6ToCSharp.CodeConversion.ConversionUtility;
using Vb6ToCSharp.Runtime;


namespace Vb6ToCSharp.Infrastructure;

public static class GitInteraction
{
    // Option Explicit
    public const string status = "status ";
    public const string st = "status ";
    public const string commit = "commit -m ";
    public const string push = "push ";
    public const string pull = "pull ";
    public const string branch = "branch ";
    public const string br = "branch ";
    public const string stash = "stash";
    public const string checkOut = "checkout ";


    private static string GitFolder()
    {
        var gitFolder = AppDomain.CurrentDomain.BaseDirectory + "\\";
        return gitFolder;
    }

    public static string GitCmd(string c, bool noOutput = false, bool hideCommand = false)
    {
        var errSt = "";

        PushDir(GitFolder());
        if (!hideCommand)
        {
            GitOut("$ " + c);
        }
        var gitCmd = RunCmdToOutput(c, out errSt);
        PopDir();
        if (!noOutput)
        {
            GitOut(gitCmd);
        }
        if (errSt != "")
        {
            GitOut("ERR: " + errSt);
        }
        return gitCmd;
    }

    private static bool GitOut(string msg)
    {
        var gitOut = false;
        msg = Trim(msg);
        while ((Left(msg, 1) == vbCr || Left(msg, 1) == vbLf))
        {
            msg = Mid(msg, 2);
        }
        if (Len(msg) > 0)
        {
            Console.WriteLine(msg);
        }
        return gitOut;
    }

    public static bool Git(string c)
    {
        if (LCase(Left(c, 4)) != "git ")
        {
            c = "git " + c;
        }
        GitCmd(c);
        var git = true;
        return git;
    }

    public static void GitConf(string vName = "", string vEMail = "", bool clear = false)
    {
        if (!IsIde())
        {
            return;

        }

        GitCmd("git config --unset user.name", true, true);
        GitCmd("git config --unset user.email", true, true);
        if (clear)
        {
            GitCmd("git config --unset --global user.name", true);
            GitCmd("git config --unset --global user.email", true);
        }
        else if (vName == "" || vEMail == "")
        {
            Console.WriteLine("user.name=" + Trim(Replace(Replace(GitCmd("git config --global user.name", true, true), vbCr, ""), vbLf, "")));
            Console.WriteLine("user.email=" + Trim(Replace(Replace(GitCmd("git config --global user.email", true, true), vbCr, ""), vbLf, "")));
        }
        else
        {
            //    GitCmd "git config --unset --global user.name", True
            //    GitCmd "git config --unset --global user.email", True
            GitCmd("git config --global user.name " + vName);
            GitCmd("git config --global user.email " + vEMail);
        }
    }

    public static bool GitPull(bool withReset = true)
    {
        var gitPull = false;
        if (!IsIde())
        {
            return gitPull;

        }
        //  If withReset Then GitReset
        if (withReset)
        {
            GitCmd("git stash");
            GitCmd("git checkout master");
        }

        GitCmd("git pull -r");
        if (MsgBox("Restarting IDE in 5s...", vbOKCancel) == vbCancel)
        {
            return gitPull;

        }
        //  RestartIDE
        gitPull = true;
        return gitPull;
    }

    public static string GitStatus()
    {
        var gitStatus = "";
        if (!IsIde())
        {
            return gitStatus;

        }
        gitStatus = GitCmd("git status");
        return gitStatus;
    }

    public static string GitVersion()
    {
        var gitVersion = "";
        if (!IsIde())
        {
            return gitVersion;

        }
        gitVersion = GitCmd("git --version");
        return gitVersion;
    }

    public static bool HasGit()
    {
        var hasGit = false;
        if (!IsIde())
        {
            return hasGit;

        }
        hasGit = GitVersion() != "";
        return hasGit;
    }

    public static bool GitReset(bool hard = false, bool toMaster = false)
    {
        var gitReset = false;
        if (!IsIde())
        {
            return gitReset;

        }
        if (!hard)
        {
            GitCmd("git checkout -- .");
            if (toMaster)
            {
                GitCmd("git checkout master -f");
            }
        }
        else
        {
            if (toMaster)
            {
                GitCmd("git checkout master -f");
            }
            GitCmd("git reset --hard");
            GitCmd("git pull -r --force");
        }
        //  RestartIDE
        gitReset = true;
        return gitReset;
    }

    public static bool GitPush(string committerUnused, string commitMessage)
    {
        var gitPush = false;
        if (!IsIde())
        {
            return gitPush;

        }

        GitCmd("git add .");
        GitCmd("git status");

        //  If MsgBox("Continue with Commit?", vbOKCancel + vbQuestion + vbDefaultButton1, "git push", , , 10) = vbCancel Then
        //    GitCmd "git stash clear"
        //    GitCmd "Stash cleared."
        //    Exit Function
        //  End If

        GitCmd("git commit -m \"" + commitMessage + "\"");
        GitCmd("git pull -r");

        //  If MsgBox("Continue with Push?", vbOKCancel + vbQuestion + vbDefaultButton1, "git push", , , 10) = vbCancel Then
        //    GitCmd "git stash clear"
        //    GitCmd "Stash cleared."
        //    Exit Function
        //  End If

        GitCmd("git push", true);
        GitCmd("git status");

        GitOut("GitPush Complete.");

        //  If MsgBox("Clear Credentials?", vbYesNo + vbExclamation + vbDefaultButton2, "Done?", , , 10) = vbYes Then
        //    GitProgress True
        //    GitConf Clear:=True
        //    GitProgress
        //  End If

        gitPush = true;
        return gitPush;
    }

    public static void GitLog(int charLimit = 3000)
    {
        if (!IsIde())
        {
            return;

        }
        var res = GitCmd("git log", true);
        res = Left(res, charLimit);
        Console.WriteLine(res);
    }

    public static bool GitCommits()
    {
        GitCmd("git log --pretty=format:\"%h - %an, %ar : %s\" -10");

        var gitCommits = true;
        return gitCommits;
    }

    public static bool GitRemoteBranches()
    {
        GitCmd("git branch --remote --list");
        var gitRemoteBranches = true;
        return gitRemoteBranches;
    }
}