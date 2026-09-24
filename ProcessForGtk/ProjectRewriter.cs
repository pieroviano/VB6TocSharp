using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProcessForGtk;

/// <summary>Rewrites an SDK-style project so it targets Gtk instead of Windows Forms.</summary>
/// <remarks>
/// Text-based on purpose: an <see cref="System.Xml.Linq.XDocument"/> round trip would reformat the
/// whole file, so only the lines that actually change are touched.
/// </remarks>
internal static class ProjectRewriter
{
    /// <summary>The package a WinForms-converted project references.</summary>
    internal const string WinFormsPackage = "Net4x.Vb6ToCSharp.WinForms.UpgradeHelpers";

    /// <summary>The package its Gtk counterpart must reference instead.</summary>
    internal const string GtkPackage = "Net4x.Vb6ToCSharp.Gtk.UpgradeHelpers";

    /// <summary>The Gtk implementation of Windows Forms, which a Gtk project also needs.</summary>
    internal const string GtkWinFormsPackage = "Gtk.Windows.Forms";

    /// <summary>The version range referenced for <see cref="GtkWinFormsPackage"/>.</summary>
    internal const string GtkWinFormsVersion = "1.3.24.*";

    /// <summary>A platform suffix of a target framework moniker, e.g. <c>-windows10.0.19041.0</c>.</summary>
    private static readonly Regex WindowsSuffix =
        new(@"-windows[0-9.]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TargetFrameworkElement =
        new(@"(?<open><TargetFrameworks?\s*>)(?<value>[^<]*)(?<close></TargetFrameworks?\s*>)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>A whole <c>UseWindowsForms</c> element, with the line it occupies.</summary>
    private static readonly Regex UseWindowsFormsElement =
        new(@"[ \t]*<UseWindowsForms\s*>[^<]*</UseWindowsForms\s*>[ \t]*(\r?\n)?"
            + @"|[ \t]*<UseWindowsForms\s*/>[ \t]*(\r?\n)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WinFormsPackageReference =
        new(@"(?<open><PackageReference\b[^>]*?\bInclude\s*=\s*"")"
            + Regex.Escape(WinFormsPackage) + @"(?<close>"")",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The whole line of the Gtk UpgradeHelpers reference: the anchor the Gtk WinForms one follows.</summary>
    private static readonly Regex GtkPackageReferenceLine =
        new(@"(?<indent>[ \t]*)<PackageReference\b[^>]*?\bInclude\s*=\s*"""
            + Regex.Escape(GtkPackage) + @"""[^>]*?/>[ \t]*(?<eol>\r?\n|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>An existing reference to <see cref="GtkWinFormsPackage"/> (that exact id, not a sibling).</summary>
    private static readonly Regex GtkWinFormsPackageReference =
        new(@"<PackageReference\b[^>]*?\bInclude\s*=\s*"""
            + Regex.Escape(GtkWinFormsPackage) + @"""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The closing tag of the project: the fallback insertion point.</summary>
    private static readonly Regex ProjectEnd =
        new(@"[ \t]*</Project\s*>[ \t]*\r?\n?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The indentation of one nesting level, as the file itself writes it.</summary>
    private static readonly Regex FirstIndentedElement =
        new(@"^(?<indent>[ \t]+)<", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Applies every Gtk rule to the text of a project file.</summary>
    /// <returns>The rewritten text, equal to <paramref name="project"/> when nothing matched.</returns>
    internal static string Rewrite(string project)
    {
        string result = DropWindowsPlatform(project);
        result = UseWindowsFormsElement.Replace(result, string.Empty);
        result = WinFormsPackageReference.Replace(result, "${open}" + GtkPackage + "${close}");
        return AddGtkWinForms(result);
    }

    /// <summary>Strips the <c>-windows</c> platform from every moniker of TargetFramework(s).</summary>
    private static string DropWindowsPlatform(string project) =>
        TargetFrameworkElement.Replace(project, match =>
        {
            string rewritten = string.Join(";", Monikers(match.Groups["value"].Value));
            return match.Groups["open"].Value + rewritten + match.Groups["close"].Value;
        });

    /// <summary>The monikers of a TargetFramework(s) value, de-platformed and de-duplicated.</summary>
    private static IEnumerable<string> Monikers(string value)
    {
        var seen = new List<string>();
        foreach (string moniker in value.Split(';'))
        {
            string bare = WindowsSuffix.Replace(moniker.Trim(), string.Empty);
            if (bare.Length > 0 && !seen.Contains(bare, StringComparer.OrdinalIgnoreCase))
            {
                seen.Add(bare);
            }
        }

        return seen;
    }

    /// <summary>Adds the Gtk Windows Forms reference, unless the project already has one.</summary>
    private static string AddGtkWinForms(string project)
    {
        if (GtkWinFormsPackageReference.IsMatch(project))
        {
            return project;
        }

        string eol = project.Contains("\r\n") ? "\r\n" : "\n";
        Match anchor = GtkPackageReferenceLine.Match(project);
        if (anchor.Success)
        {
            // The anchor ends at a line break unless it is the last line of the file, which then needs one.
            string ended = anchor.Groups["eol"].Value;
            return project.Insert(anchor.Index + anchor.Length,
                (ended.Length > 0 ? string.Empty : eol)
                + anchor.Groups["indent"].Value + Reference()
                + (ended.Length > 0 ? ended : eol));
        }

        Match end = ProjectEnd.Match(project);
        if (!end.Success)
        {
            return project;
        }

        Match indented = FirstIndentedElement.Match(project);
        string step = indented.Success ? indented.Groups["indent"].Value : "  ";
        string group = step + "<ItemGroup>" + eol
                       + step + step + Reference() + eol
                       + step + "</ItemGroup>" + eol;
        return project.Insert(end.Index, group);
    }

    /// <summary>The Gtk Windows Forms <c>PackageReference</c> element, without indentation.</summary>
    private static string Reference() =>
        $"<PackageReference Include=\"{GtkWinFormsPackage}\" Version=\"{GtkWinFormsVersion}\" />";
}
