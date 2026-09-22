using System;
using static Vb6ToCSharp.Modules.ModGit;


namespace Vb6ToCSharp.Modules;

public static class ModTestCases
{
    // Option Explicit
    // This module exists solely to list test conversion caess to make sure the converter can convert itself containing them.
    // There should be no active and/or used code in this module.
    // These tests are not run, they are conversion tests.  They should be converted correctly when this project is converted.


    public static void TestCallModuleFunction()
    {
        // module name (w/, w/o)
        // assign value (w/w/o)
        // empty args parans (w/w/o)

        ModGit.GitVersion();
        GitVersion();
        ModGit.GitCmd("git --version");
        var s = ModGit.GitCmd("git --verison");
        GitCmd("git --version");
        s = GitCmd("git --verison");

        s = ModGit.GitVersion();
        ModGit.GitVersion();
        s = GitVersion();
        GitVersion();
    }

    public static void TestBooleans()
    {
        // not (w/w/o)
        // if (w/w/o)
        // fcall (w/w/o)


        var b = HasGit();
        b = HasGit();
        b = ModGit.HasGit();
        b = ModGit.HasGit();

        b = !HasGit();
        b = !HasGit();
        b = !ModGit.HasGit();
        b = !ModGit.HasGit();

        TestCallWithBooleanFunction(HasGit());
        TestCallWithBooleanFunction(!HasGit());
        TestCallWithBooleanFunction(ModGit.HasGit());
        TestCallWithBooleanFunction(!ModGit.HasGit());
        TestCallWithBooleanFunction(HasGit());
        TestCallWithBooleanFunction(!HasGit());
        TestCallWithBooleanFunction(ModGit.HasGit());
        TestCallWithBooleanFunction(!ModGit.HasGit());

        if (HasGit())
        {
            Console.WriteLine("");
        }
        if (HasGit())
        {
            Console.WriteLine("");
        }
        if (ModGit.HasGit())
        {
            Console.WriteLine();
        }
        if (ModGit.HasGit())
        {
            Console.WriteLine();
        }

        if (!HasGit())
        {
            Console.WriteLine("");
        }
        if (!HasGit())
        {
            Console.WriteLine("");
        }
        if (!ModGit.HasGit())
        {
            Console.WriteLine();
        }
        if (!ModGit.HasGit())
        {
            Console.WriteLine();
        }
    }

    public static bool TestCallWithBooleanFunction(bool bUnused)
    {
        var testCallWithBooleanFunction = true;
        return testCallWithBooleanFunction;
    }

    /*
' Also have Property in a comment
*/
    public static string[] TestFunctionWithPropertyInName()
    {
        var testFunctionWithPropertyInName = new string[0];
        return testFunctionWithPropertyInName;
    }

    public static void TestPrivateLocalFunctionCall()
    {
        PrivateLocalFunctionCall();
        PrivateLocalFunctionCall();
    }

    private static void PrivateLocalFunctionCall()
    {
        // empty
    }

    /*
' This will only be readable if the file converts with correct braces.
*/
    public static bool TestFileFinishesWell()
    {
        var testFileFinishesWell = true;
        return testFileFinishesWell;
    }
}