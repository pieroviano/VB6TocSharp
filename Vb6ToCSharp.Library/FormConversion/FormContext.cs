using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Vb6ToCSharp.CodeConversion.Model;
using Vb6ToCSharp.FormConversion.Model;
using Vb6ToCSharp.Parsing.Model;

namespace Vb6ToCSharp.FormConversion;

/// <summary>The designer file being converted, shared by the UI emitters and the code conversion (event adapters, member rewrites).</summary>
public sealed class FormContext
{
    private static readonly Regex subRx = new(@"^[ \t]*(?:(?:Private|Public|Friend|Static)[ \t]+)*Sub[ \t]+([A-Za-z_]\w*)[ \t]*\(", RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public FormControlFile ControlFile { get; }
    public UiTarget Ui { get; }
    public ProjectInfo Project { get; }
    /// <summary>Names of the Subs in the code section.</summary>
    public HashSet<string> Handlers { get; }
    public HashSet<string> Arrays { get; }

    /// <summary>Form being converted (null while converting modules and classes).</summary>
    public static FormContext Current { get; set; }

    public FormContext(FormControlFile controlFile, UiTarget ui, ProjectInfo project = null)
    {
        ControlFile = controlFile;
        Ui = ui;
        Project = project;
        Handlers = new HashSet<string>(subRx.Matches(controlFile.Code ?? "").Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
        Arrays = controlFile.ControlArrays;
    }

    public string ClassName => ControlFile.Name;

    /// <summary>Handler prefix of the root (<c>Form_Load</c>, <c>MDIForm_Load</c>, <c>UserControl_Resize</c>).</summary>
    public string RootPrefix => ControlFile.Root?.Type switch
    {
        "VB.MDIForm" => "MDIForm",
        "VB.UserControl" => "UserControl",
        "VB.PropertyPage" => "PropertyPage",
        _ => "Form",
    };

    public ControlInfo Info(ControlWithType c) => ControlCatalog.Lookup(c.Type, Ui, Project);

    public string HandlerName(ControlWithType c, string vbEvent) => (c == ControlFile.Root ? RootPrefix : c.Name) + "_" + vbEvent;

    /// <summary>Control (first array element) and VB event a Sub handles, by trying every <c>_</c> split.</summary>
    public bool TryResolveHandler(string method, out ControlWithType controlWithType, out string vbEvent)
    {
        controlWithType = null;
        vbEvent = "";
        for (var i = method.LastIndexOf('_'); i > 0; i = method.LastIndexOf('_', i - 1))
        {
            var name = method.Substring(0, i);
            var ev = method.Substring(i + 1);
            var c = name.Equals(RootPrefix, StringComparison.OrdinalIgnoreCase) ? ControlFile.Root : ControlFile.Find(name);
            if (c != null && ev != "")
            {
                controlWithType = c;
                vbEvent = ev;
                return true;
            }
        }
        return false;
    }

    /// <summary>Mapped events that have a handler in the code, in subscription order.</summary>
    public List<(string vbEvent, EventBinding binding, string handler)> EventsOf(ControlWithType c)
    {
        var res = new List<(string, EventBinding, string)>();
        var info = Info(c);
        var order = c == ControlFile.Root ? EventCatalog.RootEventOrder.Concat(EventCatalog.AllEvents).Distinct() : EventCatalog.AllEvents;
        foreach (var ev in order)
        {
            var h = HandlerName(c, ev);
            if (!Handlers.Contains(h)) continue;
            var b = EventCatalog.Resolve(info, ev, Ui);
            if (b == null || b.Special != "") continue;
            res.Add((ev, b, ExactHandler(h)));
        }
        return res;
    }

    /// <summary>Handler name with the capitalization used in the code.</summary>
    public string ExactHandler(string h) => Handlers.FirstOrDefault(x => x.Equals(h, StringComparison.OrdinalIgnoreCase)) ?? h;
}