using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.VisualBasic.PowerPacks.Printing.Compatibility.VB6;
using Vb6ToCSharp.Modules;
using static Microsoft.VisualBasic.Constants;

namespace Vb6ToCSharp
{
    public static class VbExtension
    {
        public enum vbTriState
        {
            vbFalse = 0,
            vbTrue = -1,
            vbUseDefault = -2
        }

        private static Printer mPrinter = new Printer();
        private static List<Printer> mPrinters;

        private static readonly Action EmptyDelegate = delegate { };

        public static int MousePointer
        {
            get => 0;
            set { }
        }

        public static List<Printer> Printers
        {
            get
            {
                if (mPrinters == null)
                {
                    mPrinters = new List<Printer>();
                    foreach (var P in new PrinterCollection())
                    {
                        mPrinters.Add((Printer)P);
                    }
                }

                return mPrinters;
            }
        }

        public static Printer Printer
        {
            get => mPrinter ?? new Printer();
            set
            {
                foreach (var P in Printers)
                {
                    if (P.DeviceName == value.DeviceName)
                    {
                        mPrinter = P;
                    }
                }
            }
        }

        public static List<Window> Forms
        {
            get
            {
                var ret = new List<Window>();
                if (Application.Current == null)
                {
                    return ret;
                }

                foreach (Window w in Application.Current.Windows)
                {
                    ret.Add(w);
                }

                return ret;
            }
        }

        public static ModifierKeys Shift => Keyboard.Modifiers;
        public static ScreenMetrics Screen => new ScreenMetrics();

        public static int AddItem(this ComboBox c, string C)
        {
            return c.Items.Add(new ComboboxItem(C));
        }

        public static int AddItem(this ComboBox c, string C, int d)
        {
            return c.Items.Add(new ComboboxItem(C, d));
        }

        public static int AddItem(this ComboBox c, string C, bool select)
        {
            var x = new ComboboxItem(C);
            var res = c.Items.Add(x);
            if (select)
            {
                c.SelectedItem = x;
            }

            return res;
        }

        public static int AddItem(this ComboBox c, string C, int d, bool select)
        {
            var x = new ComboboxItem(C, d);
            var res = c.Items.Add(x);
            if (select)
            {
                c.SelectedItem = x;
            }

            return res;
        }

        public static int AddItem(this ListBox c, string C)
        {
            return c.Items.Add(new ComboboxItem(C));
        }

        public static int AddItem(this ListBox c, string C, int d)
        {
            return c.Items.Add(new ComboboxItem(C, d));
        }

        public static int AddItem(this ListBox c, string C, bool selected)
        {
            var x = c.Items.Add(new ComboboxItem(C));
            return c.SelectItem(x, selected);
        }

        public static int AddItem(this ListBox c, string C, int d, bool selected)
        {
            var x = c.Items.Add(new ComboboxItem(C, d));
            return c.SelectItem(x, selected);
        }

        public static TreeViewItem AddItem(this TreeView t, string Value)
        {
            return TreeViewAddItem(t, Value);
        }

        public static TreeViewItem AddItem(this TreeView t, string Value, string key)
        {
            return TreeViewAddItem(t, Value, key);
        }

        public static TreeViewItem AddItem(this TreeView t, string Value, string key, TreeViewItem parent)
        {
            return TreeViewAddItem(t, Value, key, parent);
        }

        public static string AppHelpFile()
        {
            return "";
        }

        public static void Box(this Printer P, float x1, float y1, float x2, float y2, int style = 0)
        {
            P.Line(x1, y1, x2, y2, style, true);
        }

        public static void BoxStep(this Printer P, float x1, float y1, float x2, float y2, int style = 0)
        {
            P.Line(x1, y1, x1 + x2, y1 + y2, style, true);
        }

        public static bool CBool(object A)
        {
            {
                return A is IConvertible ? ((IConvertible)A).ToBoolean(null) : false;
            }
        }

        public static decimal CCur(decimal A)
        {
            return A;
        }

        public static DateTime CDate(dynamic A)
        {
            if (A is DateTime)
            {
                return A;
            }

            // was inverted: returned MinValue for valid dates and parsed invalid ones
            return IsDate(A.ToString()) ? DateTime.Parse(A.ToString()) : DateTime.MinValue;
        }

        public static double CDbl(object A)
        {
            return A is IConvertible ? ((IConvertible)A).ToDouble(null) : 0;
        }

        public static decimal CDec(object A)
        {
            return (decimal)(A is IConvertible ? ((IConvertible)A).ToDouble(null) : 0);
        }

        public static int CInt(object A)
        {
            return A is IConvertible ? ((IConvertible)A).ToInt32(null) : 0;
        }

        public static long CLng(object A)
        {
            return A is IConvertible ? ((IConvertible)A).ToInt64(null) : 0;
        }

        public static short CShort(object A)
        {
            short z = 0;
            return A is IConvertible ? ((IConvertible)A).ToInt16(null) : z;
        }

        public static string CStr(object A)
        {
            return "" + A;
        }

        public static void CenterInScreen(this Window Ob)
        {
            Ob.Left = (SystemParameters.PrimaryScreenWidth - Ob.Width) / 2;
            Ob.Top = (SystemParameters.PrimaryScreenHeight - Ob.Height) / 2;
        }

        public static void Circle(this Printer P, float x1, float y1, float x2, float y2, float radius = 0,
            bool box = false)
        {
        }

        public static void Clear(this ComboBox c)
        {
            c.Items.Clear();
        }

        public static bool Clear(this ListBox c)
        {
            c.Items.Clear();
            return true;
        }

        public static void Clear(this TreeView t, string Value, string key = null)
        {
            t.Items.Clear();
        }

        public static Brush ColorToBrush(string C)
        {
            return (Brush)new BrushConverter().ConvertFromString(C);
        }

        public static Brush ColorToBrush(uint C)
        {
            return (Brush)new BrushConverter().ConvertFromString("#" + C.ToString("X"));
        }

        public static bool Contains(this ListBox c, string s)
        {
            return c.IndexOf(s) != -1;
        }

        public static List<string> ControlNames(this Window w, bool recurse = true)
        {
            var res = new List<string>();
            foreach (var c in w.Controls(recurse))
            {
                res.Add(c.Name);
            }

            return res;
        }

        public static FrameworkElement ControlOf(this Window w, Type T, int n = 0)
        {
            var lst = w.Controls(T);
            if (lst.Count == 0)
            {
                return null;
            }

            return lst[n < 0 ? 0 : n >= lst.Count ? lst.Count - 1 : n];
        }

        public static FrameworkElement ControlOf(this Panel w, Type T, int n = 0)
        {
            var lst = new List<FrameworkElement>();
            foreach (var l in w.getControls())
            {
                if (l.GetType() == T)
                {
                    lst.Add(l);
                }
            }

            if (lst.Count == 0)
            {
                return null;
            }

            return lst[n < 0 ? 0 : n >= lst.Count ? lst.Count - 1 : n];
        }

        public static List<FrameworkElement> Controls(this Window w, bool recurse = true)
        {
            var g = (Panel)w.Content;
            var children = g.Children;
            var cts = new List<FrameworkElement>();
            foreach (var e in children)
            {
                cts.Add((FrameworkElement)e);
                if (recurse && e is GroupBox)
                {
                    foreach (var f in ((GroupBox)e).Controls(recurse))
                    {
                        cts.Add(f);
                    }
                }
            }

            return cts;
        }

        public static List<FrameworkElement> Controls(this GroupBox w, bool recurse = true)
        {
            var g = (Panel)w.Content;
            var children = g.Children;
            var cts = new List<FrameworkElement>();
            foreach (var e in children)
            {
                cts.Add((FrameworkElement)e);
                if (recurse && e is GroupBox)
                {
                    foreach (var f in ((GroupBox)e).Controls(recurse))
                    {
                        cts.Add(f);
                    }
                }
            }

            return cts;
        }

        public static List<FrameworkElement> Controls(this Window w, Type T)
        {
            List<FrameworkElement> lst = w.Controls(), res = new List<FrameworkElement>();
            foreach (var l in lst)
            {
                if (l.GetType() == T)
                {
                    res.Add(l);
                }
            }

            return res;
        }

        public static dynamic CreateObject(string IdName)
        {
            var ObjectType = Type.GetTypeFromProgID(IdName);
            dynamic ObjectInst = Activator.CreateInstance(ObjectType);
            return ObjectInst;
        }
        //public static DateTime DateAdd1(string unit, int amount, DateTime when) { return DateAndTime.DateAdd(getDateInterval(unit), amount, when); }

        public static DateTime DateValue(object A)
        {
            if (A is string)
            {
                try
                {
                    return DateTime.Parse((string)A);
                }
                catch
                {
                    return DateTime.MinValue;
                }
            }

            return CDate(A);
        }

        public static bool DoEvents(Window Frm = null)
        {
            // Frm is optional: pump the current thread's dispatcher when no window is given
            (Frm?.Dispatcher ?? Dispatcher.CurrentDispatcher).Invoke(delegate { }, DispatcherPriority.ContextIdle);
            return true;
        }

        public static bool End()
        {
            Application.Current.Shutdown();
            return false;
        }

        public static void FocusSelect(this TextBox c)
        {
            c.SelectionStart = 0;
            c.SelectionLength = c.Text.Length;
            c.Focus();
        }

        public static DataGridCell GetCell(this DataGrid grid, DataGridRow row, int column)
        {
            if (row != null)
            {
                var presenter = GetVisualChild<DataGridCellsPresenter>(row);

                if (presenter == null)
                {
                    grid.ScrollIntoView(row, grid.Columns[column]);
                    presenter = GetVisualChild<DataGridCellsPresenter>(row);
                }

                if (presenter == null)
                {
                    return null;
                }

                var cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                return cell;
            }

            return null;
        }

        public static DataGridCell GetCell(this DataGrid grid, int row, int column)
        {
            var rowContainer = grid.GetRow(row);
            return grid.GetCell(rowContainer, column);
        }

        public static IEnumerable<Visual> GetChildren(this Visual parent, bool recurse = true)
        {
            if (parent != null)
            {
                var count = VisualTreeHelper.GetChildrenCount(parent);
                for (var i = 0; i < count; i++)
                {
                    // Retrieve child visual at specified index value.
                    var child = VisualTreeHelper.GetChild(parent, i) as Visual;

                    if (child != null)
                    {
                        yield return child;

                        if (recurse)
                        {
                            foreach (var grandChild in child.GetChildren())
                            {
                                yield return grandChild;
                            }
                        }
                    }
                }
            }
        }

        public static DataGridRow GetRow(this DataGrid grid, int index)
        {
            var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(index);
            if (row == null)
            {
                // May be virtualized, bring into view and try again.
                grid.UpdateLayout();
                try
                {
                    grid.ScrollIntoView(grid.Items[index]);
                }
                catch
                {
                }

                row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(index);
            }

            return row;
        }

        public static DataGridRow GetSelectedRow(this DataGrid grid)
        {
            return (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(grid.SelectedItem);
        }

        //public class KeyedTreeViewItem
        //{
        //    public ObservableCollection<KeyedTreeViewItem> Items { get; set; }
        //    public string Key;
        //    public string Name;
        //    public KeyedTreeViewItem Parent;
        //    private void setup(KeyedTreeViewItem parent, string vKey, string vName)
        //    {
        //        Parent = parent;
        //        Items = new ObservableCollection<KeyedTreeViewItem>();
        //        Key = vKey;
        //        Name = vName;
        //    }

        //    public KeyedTreeViewItem(string vKey, string vName) : base()
        //    { setup(null, vKey, vName); }

        //    private KeyedTreeViewItem(KeyedTreeViewItem parent, string vKey, string vName) : base()
        //    { setup(parent, vKey, vName); }

        //    public void Add(string vKey, string vName)
        //    { Items.Add(new KeyedTreeViewItem(this, vKey, vName)); }

        //    public new string ToString() { return Name; }
        //}
        //public static KeyedTreeViewItem SelectedItemKeyed(this TreeView T)
        //{ return (KeyedTreeViewItem)T.SelectedItem; }

        //public static KeyedTreeViewItem getItemByKey(this TreeView T, string key)
        //{
        //    foreach (KeyedTreeViewItem I in T.Items)
        //        if (I.Key == key) return I;
        //    return null;
        //}

        public static T GetVisualChild<T>(Visual parent) where T : Visual
        {
            var child = default(T);
            var numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < numVisuals; i++)
            {
                var v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }

                if (child != null)
                {
                    break;
                }
            }

            return child;
        }

        public static bool HasEmptyText(this TextBox textBox)
        {
            return string.IsNullOrEmpty(textBox.Text);
        }

        public static bool IIf(bool a, bool b, bool c)
        {
            return !!a ? b : c;
        }

        public static string IIf(bool a, string b, string c)
        {
            return !!a ? b : c;
        }

        public static double IIf(bool a, double b, double c)
        {
            return !!a ? b : c;
        }

        public static decimal IIf(bool a, decimal b, decimal c)
        {
            return !!a ? b : c;
        }

        public static int IIf(bool a, int b, int c)
        {
            return !!a ? b : c;
        }

        public static DateTime IIf(bool a, DateTime b, DateTime c)
        {
            return !!a ? b : c;
        }

        public static int IndexOf(this ListBox c, string s)
        {
            foreach (var l in c.Items)
            {
                if (Strings.Trim(((ComboboxItem)l).ToString()) == s)
                {
                    return c.Items.IndexOf(l);
                }
            }

            return -1;
        }

        public static bool IsDate(string D)
        {
            try
            {
                DateTime.Parse(D);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public static bool IsEmpty(object A)
        {
            return false;
        }

        public static bool IsInStr(string src, string find)
        {
            return Strings.InStr(src, find) != 0;
        }

        public static bool IsLike(string A, string b)
        {
            return LikeOperator.LikeString(A, b, CompareMethod.Binary);
        }

        public static bool IsList(object A)
        {
            return A != null && A is IList;
        }

        public static bool IsMissing(object A)
        {
            return false;
        }

        public static bool IsNothing(object A)
        {
            return IsNull(A);
        }

        public static bool IsNull(object A)
        {
            return A == null || A is DBNull;
        }

        public static bool IsObject(object A)
        {
            return !IsNothing(A);
        }

        public static dynamic Item(this Collection C, dynamic key)
        {
            return C[key];
        }

        public static TreeViewItemObject Item(this TreeView t, int x)
        {
            return (TreeViewItemObject)t.Items.GetItemAt(x);
        }

        public static TreeViewItemObject Item(this TreeView t, string x)
        {
            foreach (var l in t.Items)
            {
                if (((TreeViewItemObject)l).getKey() == x)
                {
                    return (TreeViewItemObject)l;
                }
            }

            return null;
        }

        public static int LBound(object A)
        {
            // lower bound is always 0 (was -1 for empty lists, making LBound..UBound loops run once)
            return 0;
        }

        public static void Line(this Printer P, float x1, float y1, float x2, float y2, int style = 0, bool box = false)
        {
        }

        public static void LineStep(this Printer P, float x1, float y1, float x2, float y2, int style = 0, bool box = false)
        {
        }

        public static string List(this ComboBox c, int Index)
        {
            return Index < c.Items.Count ? c.Items[Index].ToString() : null;
        }

        public static string List(this ListBox c, int Index)
        {
            return Index >= 0 && Index < c.Items.Count ? c.Items[Index].ToString() : "";
        }

        public static void Load(Window Ob)
        {
        }

        public static bool LoadFile(this RichTextBox r, string f)
        {
            return true;
        }

        public static bool Locked(this TextBox t, bool value = true)
        {
            return false;
        }

        public static bool Locked(this ComboBox t, bool value = true)
        {
            return false;
        }

        public static bool Locked(this ListBox t, bool value = true)
        {
            return false;
        }

        public static Size MeasureString(this Label el, string candidate)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var formattedText = new FormattedText(SanitizeNls(candidate, "\n"), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(el.FontFamily, el.FontStyle, el.FontWeight, el.FontStretch),
                el.FontSize, Brushes.Black, new NumberSubstitution(), TextFormattingMode.Display);
#pragma warning restore CS0618 // Type or member is obsolete
            return new Size(formattedText.Width, formattedText.Height);
        }

        public static Size MeasureString(this TextBox el, string candidate)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var formattedText = new FormattedText(candidate, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(el.FontFamily, el.FontStyle, el.FontWeight, el.FontStretch),
                el.FontSize, Brushes.Black, new NumberSubstitution(), TextFormattingMode.Display);
#pragma warning restore CS0618 // Type or member is obsolete
            return new Size(formattedText.Width, formattedText.Height);
        }

        public static Size MeasureString(this Window el, string candidate)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var formattedText = new FormattedText(candidate, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(el.FontFamily, el.FontStyle, el.FontWeight, el.FontStretch),
                el.FontSize, Brushes.Black, new NumberSubstitution(), TextFormattingMode.Display);
#pragma warning restore CS0618 // Type or member is obsolete
            return new Size(formattedText.Width, formattedText.Height);
        }

        //public static bool Move(this Control c, double X = -10000, double Y = -10000, double W = -1000, double H = -10000, bool MakeVisible = false)
        //{ return c.Move((decimal)X, (decimal)Y, (decimal)W, (decimal)H, MakeVisible); }
        public static bool Move(this FrameworkElement c, decimal X = -10000, decimal y = -10000, decimal w = -10000,
            decimal h = -10000, bool makeVisible = false)
        {
            if (c == null)
            {
                return false;
            }

            if (w > 0)
            {
                c.Width = (double)w;
            }

            if (h > 0)
            {
                c.Height = (double)h;
            }

            //if (c.Parent is Grid) {
            var t = c.Margin;
            if (X != -10000 && X != -1)
            {
                t.Left = (double)X;
            }

            if (y != -10000 && y != -1)
            {
                t.Top = (double)y;
            }

            c.Margin = t;
            //}
            //else if (c.Parent is Canvas)
            //{
            //    if (X != -10000 && X != -1) Canvas.SetLeft(c, (double)X);
            //    if (Y != -10000 && Y != -1) Canvas.SetTop(c, (double)Y);
            //}

            c.Margin = new Thickness(
                X == -10000 || X == -1 ? c.Margin.Left : (double)X,
                y == -10000 || y == -1 ? c.Margin.Top : (double)y,
                0, 0
            );
            if (makeVisible)
            {
                c.Visibility = Visibility.Visible;
            }

            //try { c.Focus(); } catch { }
            return false;
        }

        public static bool Move(this FrameworkElement c, double X = -10000, double y = -10000, double w = -10000,
            double h = -10000, bool makeVisible = false)
        {
            return c.Move((decimal)X, (decimal)y, (decimal)w, (decimal)h, makeVisible);
        }

        public static BitmapImage PackageImage(string s, bool placeholder = true)
        {
            if (Strings.Left(s, 1) != "/")
            {
                s = "/Resources/Images/" + s;
            }

            s = "pack://application:,,," + s;
            try
            {
                return new BitmapImage(new Uri(s));
            }
            catch
            {
                if (!placeholder)
                {
                    return null;
                }

                var d = "/Resources/Images/none.bmp";
                return new BitmapImage(new Uri(d, UriKind.Relative));
            }
        }

        public static void PaintPicture(this Printer P, Image I, dynamic x1 = null, dynamic y1 = null, dynamic w1 = null,
            dynamic h1 = null, dynamic x2 = null, dynamic y2 = null, dynamic w2 = null, dynamic h2 = null)
        {
            System.Drawing.Image I2 = null;
            P.PaintPicture(I2, ValF(x1), ValF(y1), ValF(w1), ValF(h1), ValF(x2), ValF(h2), ValF(w2), ValF(h2));
        }

        public static void PaintPicture(this Image P, Image I, dynamic x1 = null, dynamic y1 = null, dynamic w1 = null,
            dynamic h1 = null, dynamic x2 = null, dynamic y2 = null, dynamic w2 = null, dynamic h2 = null)
        {
        }

        public static void PrintNNL(this Printer p, params string[] s)
        {
            var Y = p.CurrentY;
            p.Print(s);
            p.CurrentY = Y;
        }

        public static void PrintPicture(this Printer P, BitmapImage I, dynamic x1 = null, dynamic y1 = null,
            dynamic w1 = null, dynamic h1 = null, dynamic x2 = null, dynamic y2 = null, dynamic w2 = null,
            dynamic h2 = null)
        {
            System.Drawing.Image I2 = null;
            P.PaintPicture(I2, ValF(x1), ValF(y1), ValF(w1), ValF(h1), ValF(x2), ValF(h2), ValF(w2), ValF(h2));
        }

        public static void PrintPicture(this Printer P, ImageSource I, dynamic x1 = null, dynamic y1 = null,
            dynamic w1 = null, dynamic h1 = null, dynamic x2 = null, dynamic y2 = null, dynamic w2 = null,
            dynamic h2 = null)
        {
            System.Drawing.Image I2 = null;
            P.PaintPicture(I2, ValF(x1), ValF(y1), ValF(w1), ValF(h1), ValF(x2), ValF(h2), ValF(w2), ValF(h2));
        }

        public static string PrinterName()
        {
            return Printer != null ? Printer.DeviceName : "";
        }

        public static List<string> PrinterNames()
        {
            var col = PrinterSettings.InstalledPrinters;
            var arr = new string[col.Count];
            col.CopyTo(arr, 0);
            return new List<string>(arr);
        }

        public static void Refresh(this UIElement uiElement)
        {
            uiElement.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
        }

        public static void RemoveItem(this ComboBox c, int Index)
        {
            c.Items.RemoveAt(Index);
        }

        public static void RemoveItem(this ListBox c, int Index)
        {
            c.Items.RemoveAt(Index);
        }

        public static void ResetPrinters()
        {
            mPrinters = null;
        }

        public static bool Resume()
        {
            return false;
        }

        //public static string Chr(int C) { return Chr((int)C); }
        //public static string Mid(string S, int F) { return Mid(S, (int)F); }
        //public static string Mid(string S, int F, int L) { return Mid(S, (int)F, (int)L); }
        //public static string Left(string S, int F) { return Left(S, (int)F); }
        //public static string Right(string S, int F) { return Right(S, (int)F); }
        public static decimal RndD()
        {
            return (decimal)VBMath.Rnd();
        }

        public static string SanitizeNls(string s)
        {
            return SanitizeNls(s, vbLf);
        }

        public static string SanitizeNls(string s, string desired)
        {
            var hCr = IsInStr(s, vbCr);
            var hLf = IsInStr(s, vbLf);
            if (hCr && !hLf)
            {
                return s.Replace(vbCr, desired);
            }

            if (hLf && !hCr)
            {
                return s.Replace(vbLf, desired);
            }

            return s.Replace(vbCrLf, desired);
        }

        public static double ScaleHeight(this Window w)
        {
            return w.Height;
        }

        public static double ScaleWidth(this Window w)
        {
            return w.Width;
            ;
        }

        public static int ScaleX(int X, dynamic a, dynamic b)
        {
            return X;
        }

        public static int ScaleY(int Y, dynamic a, dynamic b)
        {
            return Y;
        }

        public static void SelectContents(this TextBox c)
        {
            c.SelectionStart = 0;
            c.SelectionLength = c.Text.Length;
        }

        public static void SelectContents(this ComboBox c)
        {
        }

        public static int SelectItem(this ListBox c, int I, bool isSelected)
        {
            if (c.SelectionMode == SelectionMode.Multiple)
            {
                if (isSelected)
                {
                    c.SelectedItems.Add(c.Items[I]);
                }
                else
                {
                    c.SelectedItems.Remove(c.Items[I]);
                }
            }
            else
            {
                if (isSelected)
                {
                    c.SelectedItem = c.Items[I];
                }
                else
                {
                    if (c.SelectedItem == c.Items[I])
                    {
                        c.SelectedItem = null;
                    }
                }
            }

            return I;
        }

        public static bool SelectText(this ComboBox c, string S)
        {
            for (var i = 0; i < c.Items.Count; i++)
            {
                if (i.ToString() == S)
                {
                    c.SelectedIndex = i;
                    return true;
                }
            }

            return false;
        }

        public static bool SelectText(this ListBox c, string S)
        {
            for (var i = 0; i < c.Items.Count; i++)
            {
                if (i.ToString() == S)
                {
                    c.SelectedIndex = i;
                    return true;
                }
            }

            return false;
        }

        public static bool Selected(this ListBox c, int I)
        {
            return c.SelectedItems.Contains(c.Items[I]);
        }

        public static bool Selected(this ListBox c, int I, bool Value)
        {
            if (c.SelectionMode == SelectionMode.Single)
            {
                c.SelectedItem = c.Items[I];
                return true;
            }

            if (Value)
            {
                c.SelectedItems.Add(c.Items[I]);
            }
            else
            {
                c.SelectedItems.Remove(c.Items[I]);
            }

            return c.Selected(I);
        }

        public static string SelectedText(this ComboBox c)
        {
            return c.SelectedItem == null ? "" : ((ComboboxItem)c.SelectedItem).Text;
        }

        public static string SelectedText(this ListBox c)
        {
            return c.SelectedItem == null ? "" : ((ComboboxItem)c.SelectedItem).ToString();
        }

        public static int SelectedValue(this ComboBox c)
        {
            return ((ComboboxItem)c.SelectedItem).Value;
        }

        public static int SenderIndex(string name)
        {
            return ValI(name.Substring(name.LastIndexOf('_') + 1));
        }

        public static int SenderIndex(object sender)
        {
            return SenderIndex(((FrameworkElement)sender).Name);
        }

        public static bool SetFocus(this FrameworkElement c)
        {
            try
            {
                return c.Focus();
            }
            catch
            {
                return false;
            }
        }

        public static string SetItemText(this ComboBox c, int Index, string text)
        {
            return ((ComboboxItem)c.Items[Index]).Text = text;
        }

        public static void SetSelectedIndex(this TreeView t, int Index)
        {
            ((TreeViewItem)t.Items.GetItemAt(Index)).IsSelected = true;
        }

        public static string ShiftStr(Key v)
        {
            return ShiftStr(v.ToString());
        }

        public static string ShiftStr(string v = null)
        {
            var s = "";
            if (0 != (Keyboard.Modifiers & ModifierKeys.Windows))
            {
                s += "Win-";
            }

            if (0 != (Keyboard.Modifiers & ModifierKeys.Control))
            {
                s += "Ctrl-";
            }

            if (0 != (Keyboard.Modifiers & ModifierKeys.Alt))
            {
                s += "Alt-";
            }

            if (0 != (Keyboard.Modifiers & ModifierKeys.Shift))
            {
                s += "Shift-";
            }

            if (v != null)
            {
                s += v;
            }

            return s;
        }

        public static bool Show(this Window w, int Modal)
        {
            w.ShowDialog();
            return true;
        }

        public static string Spc(int I)
        {
            return Strings.StrDup(I, ' ');
        }

        //public static void Unload(this Window Ob) { Ob.Close(); }
        public static void Stop(int code = 1)
        {
            Environment.Exit(code);
        }

        public static string Tab(int N)
        {
            return "::TABSTOP:" + N;
        }

        public static decimal TextHeight(string S)
        {
            return ModTextFiles.CountLines(S) * 10m;
        }

        public static double TextHeight(this Canvas t, string s)
        {
            return ((Window)t.Parent).MeasureString(s).Height;
        }

        public static double TextHeight(this Label t, string s)
        {
            return t.MeasureString(s).Height;
        }

        public static decimal TextWidth(string S)
        {
            return S.Length * 10m;
        }

        public static double TextWidth(this Canvas t, string s)
        {
            return ((Window)t.Parent).MeasureString(s).Width;
        }

        public static double TextWidth(this Label t, string s)
        {
            return t.MeasureString(s).Width;
        }

        public static TreeViewItem TreeViewAddItem(TreeView t, string Value, string key = null, TreeViewItem parent = null)
        {
            TreeViewItem tvi;
            if (parent == null)
            {
                var x = t.Items.Add(new TreeViewItemObject(Value, key));
                tvi = t.Item(x);
            }
            else
            {
                var x = parent.Items.Add(new TreeViewItemObject(Value, key));
                tvi = t.Item(x);
            }

            return tvi;
        }

        public static int UBound(object A)
        {
            return A != null && A is IList ? ((IList)A).Count - 1 : 0;
        }

        public static bool VBCloseFile(dynamic a)
        {
            return false;
        }

        public static bool VBOpenFile(dynamic a, dynamic b)
        {
            return false;
        }

        public static string VBReadFileLine(dynamic a, dynamic b)
        {
            return "";
        }

        public static dynamic VBSwitch(params dynamic[] vals)
        {
            for (var i = 0; i < vals.Length; i += 2)
            {
                if (i == vals.Length - 1)
                {
                    return vals[i]; // odd number, return as default.
                }

                if (CBool(vals[i]))
                {
                    return vals[i + 1];
                }
            }

            return null;
        }

        public static bool VBWriteFile(dynamic a, dynamic b)
        {
            return false;
        }

        public static decimal ValD(string A)
        {
            return (decimal)ValDouble((A ?? "").Replace(",", ""));
        }

        public static decimal ValD(decimal A)
        {
            return A;
        }

        public static decimal ValD(int A)
        {
            return A;
        }

        public static decimal ValD(double A)
        {
            return (decimal)A;
        }

        public static double ValDouble(string s)
        {
            var f = "";

            if (s == null)
            {
                return 0;
            }

            if (s.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (s.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            // like VB Val: leading blanks ignored, one decimal point, culture-invariant
            s = s.TrimStart();
            if (s.StartsWith("-"))
            {
                f = "-";
                s = s.Substring(1);
            }

            var digits = false;
            var dot = false;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c >= '0' && c <= '9')
                {
                    digits = true;
                }
                else if (c != '.' || dot)
                {
                    break;
                }
                else
                {
                    dot = true;
                }

                f += c.ToString();
            }

            if (!digits)
            {
                return 0;
            }

            return double.Parse(f, CultureInfo.InvariantCulture);
        }

        public static float ValF(string A)
        {
            return ValF(ValD(A));
        }

        public static float ValF(decimal A)
        {
            return (float)A;
        }

        public static int ValI(string A)
        {
            return (int)ValDouble(A);
        }

        public static int ValI(int A)
        {
            return A;
        }

        public static int ValI(decimal A)
        {
            return (int)A;
        }

        public static int ValI(float A)
        {
            return (int)A;
        }

        public static int ValI(double A)
        {
            return (int)A;
        }

        public static int ValI(bool A)
        {
            return A ? 1 : 0;
        }

        public static int ValL(string A)
        {
            return ValI(A);
        }

        public static void ZOrder(this FrameworkElement c, int v)
        {
        }

        public static List<FrameworkElement> controlArray(this Window frm, string name)
        {
            var res = new List<FrameworkElement>();
            var G = (Panel)frm.Content;
            foreach (var C in G.Children)
            {
                if (((FrameworkElement)C).Name.StartsWith(name + "_"))
                {
                    res.Add((FrameworkElement)C);
                }
            }

            return res;
        }

        public static int controlIndex(string name)
        {
            // was Mid(name, pos + 1): 1-based Mid then started at the '_' and always yielded 0
            var i = name?.LastIndexOf('_') ?? -1;
            return i < 0 ? -1 : ValI(name.Substring(i + 1));
        }

        public static int controlIndex(this Control c)
        {
            return controlIndex(c.Name);
        }

        public static int controlUBound(this Window frm, string name)
        {
            var Max = -1;
            foreach (var C in frm.Controls())
            {
                var N = C.Name;
                if (N.StartsWith(name + "_"))
                {
                    var K = ValI(Strings.Mid(N, N.LastIndexOf('_') + 2));
                    if (K > Max)
                    {
                        Max = K;
                    }
                }
            }

            return Max;
        }

        public static string getCaption(this Button btn)
        {
            try
            {
                Label T = null;
                foreach (var c in ((Panel)btn.Content).Children)
                {
                    if (c is Label)
                    {
                        T = (Label)c;
                        break;
                    }
                }

                if (T is null)
                {
                    return "";
                }

                if (T.Content is null)
                {
                    return "";
                }

                return T.Content.ToString();
            }
            catch
            {
                return "";
            }
        }

        public static FrameworkElement getControlByIndex(this Window frm, string name, int idx)
        {
            foreach (var C in frm.Controls())
            {
                if (C.Name == name + "_" + idx)
                {
                    return C;
                }
            }

            return null;
        }

        public static IEnumerable<FrameworkElement> getControls(this Visual parent, bool recurse = true)
        {
            var res = new List<FrameworkElement>();
            foreach (var el in parent.GetChildren(recurse))
            {
                res.Add((FrameworkElement)el);
            }

            return res;
        }

        public static decimal getCurrency(this TextBox c)
        {
            return ValD(c.Text);
        }

        public static decimal getCurrency(this Label c)
        {
            return ValD(c.Content.ToString());
        }

        public static DateInterval getDateInterval(string s)
        {
            switch (s)
            {
                case "y": return DateInterval.Year;
                case "m": return DateInterval.Month;
                case "w": return DateInterval.WeekOfYear;
                case "h": return DateInterval.Hour;
                case "d": return DateInterval.Day;
                case "n": return DateInterval.Minute;
                case "s": return DateInterval.Second;
                default: return DateInterval.Day;
            }
        }

        public static string getDateString(this DatePicker dp)
        {
            return (dp.SelectedDate ?? dp.DisplayDate).ToShortDateString();
        }

        public static DateTime getDateTime(this DatePicker dp)
        {
            return dp.SelectedDate ?? dp.DisplayDate;
        }

        public static int getHelpContextID(this Window wId)
        {
            return 0;
        }

        public static BitmapImage getImage(this Button btn)
        {
            try
            {
                Image T = null;
                dynamic c = btn.Content;

                if (c is Image)
                {
                    T = c;
                }

                if (c is Panel)
                {
                    foreach (var l in c.Children)
                    {
                        if (l is Image)
                        {
                            T = (Image)l;
                            break;
                        }
                    }
                }

                if (T is null)
                {
                    return null;
                }

                return (BitmapImage)T.Source;
            }
            catch
            {
                return null;
            }
        }

        public static bool getSelected(this ListBox c, int I)
        {
            return c.SelectedItems.Contains(c.Items[I]);
        }

        public static string getText(this RichTextBox r)
        {
            return "";
        }

        public static string getTimeString(this DatePicker dp)
        {
            return (dp.SelectedDate ?? dp.DisplayDate).ToShortTimeString();
        }

        public static string getToolTipText(this FrameworkElement c)
        {
            return "";
        }

        public static decimal getValue(this TextBox textBox)
        {
            try
            {
                return decimal.Parse(textBox.Text);
            }
            catch
            {
                return 0;
            }
        }

        public static decimal getValue(this Label label)
        {
            try
            {
                return ValD(label.Content.ToString());
            }
            catch
            {
                return 0;
            }
        }

        public static bool getValue(this CheckBox chk)
        {
            try
            {
                return (bool)chk.IsChecked;
            }
            catch
            {
                return false;
            }
        }

        public static bool getValue(this Button btn)
        {
            try
            {
                return btn.IsPressed;
            }
            catch
            {
                return false;
            }
        }

        public static decimal getValueCurrency(this TextBox c)
        {
            return ValD(c.Text);
        }

        public static decimal getValueCurrency(this Label c)
        {
            return ValD(c.Content.ToString());
        }

        public static DateTime? getValueDate(this TextBox textBox, DateTime? defaultDate = null)
        {
            try
            {
                return DateValue(textBox.Text);
            }
            catch
            {
                return defaultDate;
            }
        }

        public static DateTime? getValueDate(this Label textBox, DateTime? defaultDate = null)
        {
            try
            {
                return DateValue(textBox.Content.ToString());
            }
            catch
            {
                return defaultDate;
            }
        }

        public static int getValueLong(this TextBox textBox)
        {
            try
            {
                return int.Parse(textBox.Text);
            }
            catch
            {
                return 0;
            }
        }

        public static int getValueLong(this Label textBox)
        {
            try
            {
                return int.Parse(textBox.Content.ToString());
            }
            catch
            {
                return 0;
            }
        }

        public static bool getVisible(this FrameworkElement c)
        {
            return c.Visibility == Visibility.Visible;
        }

        public static bool getVisible(this Window w)
        {
            return w.Visibility == Visibility.Visible;
        }

        public static IntPtr hWnd(this Window w)
        {
            return new WindowInteropHelper(Window.GetWindow(w)).Handle;
        }

        public static IntPtr hWnd(this FrameworkElement w)
        {
            return new WindowInteropHelper(Window.GetWindow(w)).Handle;
        }

        public static bool isVisible(this FrameworkElement c)
        {
            if (c == null)
            {
                return false;
            }

            return c.Visibility == Visibility.Visible;
        }

        public static int itemData(this ComboBox c, int I)
        {
            try
            {
                return ((ComboboxItem)c.Items[I]).Value;
            }
            catch
            {
                return 0;
            }
        }

        public static int itemData(this ListBox c, int I)
        {
            try
            {
                return ((ComboboxItem)c.Items[I]).Value;
            }
            catch
            {
                return 0;
            }
        }

        public static FrameworkElement loadControlByIndex(this Window frm, Type type, string name, int idx = -1)
        {
            var X = frm.getControlByIndex(name, idx);
            if (X != null)
            {
                return X;
            }

            var C = (FrameworkElement)Activator.CreateInstance(type);
            C.Name = name + "_" + idx;
            var els = frm.controlArray(name);
            Panel G;
            var el0 = frm.getControlByIndex(name, 0);
            if (els.Count > 0)
            {
                G = els[0].Parent as Panel;
            }
            else if (el0 != null)
            {
                G = el0.Parent as Panel;
            }
            else
            {
                G = frm.Content as Panel;
            }

            G.Children.Add(C);
            return C;
        }

        public static string setCaption(this Button btn, string value)
        {
            Label T = null;
            if (btn.Content is Panel)
            {
                foreach (var c in ((Panel)btn.Content).Children)
                {
                    if (c is Label)
                    {
                        T = (Label)c;
                        break;
                    }
                }
            }

            if (btn.Content is Label)
            {
                T = (Label)btn.Content;
            }

            if (btn.Content is string)
            {
                btn.Content = value.Replace("&", "_");
            }

            if (T is null)
            {
                return "";
            }

            return (string)(T.Content = value.Replace("&", "_"));
        }

        public static decimal setCurrency(this TextBox c, decimal value)
        {
            c.Text = CurrencyFormat(value);
            return c.getCurrency();
        }

        public static decimal setCurrency(this Label c, decimal value)
        {
            c.Content = CurrencyFormat(value);
            return c.getCurrency();
        }

        //public static bool Load(this Window w) { return true; }
        public static void setHelpContextID(this Window w, int id)
        {
        }

        public static BitmapImage setImage(this Button cmd, BitmapImage value)
        {
            try
            {
                if (cmd.Content is string)
                {
                    var caption = cmd.Content.ToString();
                    var C = new Canvas();
                    cmd.Content = C;
                    C.Width = cmd.Width;
                    C.Height = cmd.Height;
                    var L = new Label();
                    L.Content = caption;
                    C.Children.Add(L);
                    L.FontSize = 12d;
                    L.Padding = new Thickness(0);
                    L.Width = L.MeasureString(caption).Width;
                    L.Height = L.MeasureString(caption).Height;
                    Canvas.SetLeft(L, (cmd.Width - L.Width) / 2);
                    Canvas.SetTop(L, cmd.Height - L.Height - 10);
                    var I = new Image();
                    C.Children.Add(I);
                    I.Width = cmd.Width - 10;
                    I.Height = cmd.Height - L.Height - 12;
                    I.Stretch = Stretch.Uniform;
                    I.Source = value;
                    Canvas.SetLeft(I, (cmd.Width - I.Width) / 2);
                    Canvas.SetTop(I, 0);
                    return value;
                }

                if (cmd.Content is Panel)
                {
                    var I = (Image)((Panel)cmd.Content).ControlOf(typeof(Image));
                    if (I == null)
                    {
                        return null;
                    }

                    I.Source = value;
                    return value;
                }

                if (cmd.Content is Image)
                {
                    ((Image)cmd.Content).Source = value;
                    return value;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static void setItemColor(this TreeView t, int item, Brush backColor = null, Brush foreColor = null)
        {
            {
                var actualItem = t.Item(item);
                if (actualItem != null)
                {
                    if (backColor != null)
                    {
                        actualItem.Background = backColor;
                    }

                    if (foreColor != null)
                    {
                        actualItem.Foreground = foreColor;
                    }
                }
            }
        }

        public static void setSelected(this ListBox c, int I, bool v)
        {
            if (c.SelectionMode == SelectionMode.Multiple)
            {
                if (v)
                {
                    c.SelectedItems.Add(c.Items[I]);
                }
                else
                {
                    c.SelectedItems.Remove(c.Items[I]);
                }
            }
            else
            {
                c.SelectedItem = c.Items[I];
            }
        }

        public static string setText(this RichTextBox r, string v)
        {
            return "";
        }

        public static bool setToolTipText(this FrameworkElement c, string id)
        {
            return true;
        }

        public static decimal setValue(this TextBox textBox, decimal value)
        {
            textBox.Text = FormatQuantity(value);
            return textBox.getValue();
        }

        public static decimal setValue(this Label label, decimal value)
        {
            label.Content = FormatQuantity(value);
            return label.getValue();
        }

        public static bool setValue(this CheckBox chk, bool value)
        {
            chk.IsChecked = value;
            return chk.getValue();
        }

        //    public static int getValue(this CheckBox chk) { try { return ((bool)chk.IsChecked); } catch { return false; } }
        public static int setValue(this CheckBox chk, int value)
        {
            chk.IsChecked = value != 1;
            return chk.getValue() ? 1 : 0;
        }

        public static bool setValue(this Button btn, bool value)
        {
            try
            {
                btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static decimal setValueCurrency(this TextBox c, decimal value)
        {
            c.Text = CurrencyFormat(value);
            return c.getCurrency();
        }

        public static decimal setValueCurrency(this Label c, decimal value)
        {
            c.Content = CurrencyFormat(value);
            return c.getCurrency();
        }

        public static DateTime? setValueDate(this TextBox textBox, DateTime? value)
        {
            textBox.Text = value == null ? "" : ((DateTime)value).ToShortDateString();
            return textBox.getValueDate();
        }

        public static DateTime? setValueDate(this Label textBox, DateTime? value)
        {
            textBox.Content = value == null ? "" : ((DateTime)value).ToShortDateString();
            return textBox.getValueDate();
        }

        public static int setValueLong(this TextBox textBox, int value)
        {
            textBox.Text = value.ToString();
            return textBox.getValueLong();
        }

        public static int setValueLong(this Label textBox, int value)
        {
            textBox.Content = value.ToString();
            return textBox.getValueLong();
        }

        public static bool setVisible(this FrameworkElement c, bool value, bool collapseClose = false)
        {
            if (c == null)
            {
                return false;
            }

            c.Visibility = value ? Visibility.Visible : collapseClose ? Visibility.Collapsed : Visibility.Hidden;
            return c.getVisible();
        }

        public static bool setVisible(this Window w, bool value)
        {
            w.Visibility = value ? Visibility.Visible : Visibility.Hidden;
            return w.getVisible();
        }

        public static void setWindowState(this Window w, WindowState x)
        {
            w.WindowState = x;
        }

        public static void toUpper(this TextBox c)
        {
            if (c.Text != c.Text.ToUpper())
            {
                c.Text = c.Text.ToUpper();
            }
        }

        public static void unloadControlByIndex(this Window frm, string name, int idx = -1)
        {
            var X = frm.getControlByIndex(name, idx);
            if (X != null)
            {
                var G = (Panel)frm.Content;
                G.Children.Remove(X);
            }
        }

        public static void unloadControls(this Window frm, string name, int baseIndex = -1)
        {
            var G = (Panel)frm.Content;
            foreach (var C in frm.Controls())
            {
                var N = C.Name;
                if (N.StartsWith(name + "_"))
                {
                    if (controlIndex(N) == baseIndex)
                    {
                        continue;
                    }

                    G.Children.Remove(C);
                }
            }
        }

        private static string CurrencyFormat(decimal d)
        {
            return d.ToString("0.00");
        }

        private static string FormatQuantity(decimal d)
        {
            return d.ToString("0.##");
        }

        public class ScreenMetrics
        {
            public FrameworkElement ActiveControl;
            public int Width => (int)SystemParameters.PrimaryScreenWidth;
            public int Height => (int)SystemParameters.PrimaryScreenHeight;
        }

        public class ComboboxItem
        {
            public ComboboxItem(string vText)
            {
                Text = vText;
            }

            public ComboboxItem(string vText, int vValue)
            {
                Text = vText;
                Value = vValue;
            }

            public string Text { get; set; }
            public int Value { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }

        public class TreeViewItemObject : TreeViewItem
        {
            private string Key;
            private readonly string Text;

            public TreeViewItemObject(string Text = "", string key = "")
            {
                this.Text = Text;
                Header = Text;
                Key = key;
            }

            public new string ToString()
            {
                return Text;
            }

            public TreeViewItem getContainer(TreeView tv)
            {
                return tv.ItemContainerGenerator.ContainerFromItem(this) as TreeViewItem;
            }

            public string getKey()
            {
                return Key;
            }

            public string getValue()
            {
                return Key;
            }

            public void setKey(string s)
            {
                Key = s;
            }

            public void setValue(string s)
            {
                Key = s;
            }
        }

        public class PropIndexer<I, V>
        {
            public delegate V getProperty(I idx);

            public delegate void setProperty(I idx, V value);

            public PropIndexer(getProperty g, setProperty s)
            {
                getter = g;
                setter = s;
            }

            public PropIndexer(getProperty g)
            {
                getter = g;
                setter = setPropertyNoop;
            }

            public PropIndexer()
            {
                getter = getPropertyNoop;
                setter = setPropertyNoop;
            }

            public V this[I idx]
            {
                get => getter.Invoke(idx);
                set => setter.Invoke(idx, value);
            }

            public V getPropertyNoop(I idx)
            {
                return default;
            }

            public event getProperty getter;

            public void setPropertyNoop(I idx, V value)
            {
            }

            public event setProperty setter;
        }

        public class PropIndexer2<I, J, V>
        {
            public delegate V getProperty(I idx, J idx2);

            public delegate void setProperty(I idx, J idx2, V value);

            public PropIndexer2(getProperty g, setProperty s)
            {
                getter = g;
                setter = s;
            }

            public PropIndexer2(getProperty g)
            {
                getter = g;
                setter = setPropertyNoop;
            }

            public PropIndexer2()
            {
                getter = getPropertyNoop;
                setter = setPropertyNoop;
            }

            public V this[I idx, J idx2]
            {
                get => getter.Invoke(idx, idx2);
                set => setter.Invoke(idx, idx2, value);
            }

            public V getPropertyNoop(I idx, J idx2)
            {
                return default;
            }

            public event getProperty getter;

            public void setPropertyNoop(I idx, J idx2, V value)
            {
            }

            public event setProperty setter;
        }

        public class Timer
        {
            public Action Action;

            public Timer(Action e = null, int vInterval = 1000, bool vEnabled = false)
            {
                timer.Tick += dispatcherTimer_Tick;
                Action = e;
                Interval = vInterval;
                Enabled = vEnabled;
            }

            public DispatcherTimer timer { get; } = new DispatcherTimer();

            public bool IsEnabled
            {
                get => timer.IsEnabled;
                set
                {
                    timer.IsEnabled = value;
                    if (value)
                    {
                        timer.Start();
                    }
                    else
                    {
                        timer.Stop();
                    }
                }
            }

            public bool Enabled
            {
                get => IsEnabled;
                set => IsEnabled = value;
            }

            public int Interval
            {
                get => (int)timer.Interval.TotalMilliseconds;
                set => timer.Interval = new TimeSpan(0, 0, 0, 0, value);
            }

            public int IntervalSeconds
            {
                get => (int)timer.Interval.TotalSeconds;
                set => timer.Interval = new TimeSpan(0, 0, 0, value);
            }

            public dynamic Tag { get; set; }

            public Timer Discard()
            {
                Enabled = false;
                return null;
            }

            public TimeSpan getInterval()
            {
                return timer.Interval;
            }

            public void setInterval(TimeSpan value)
            {
                timer.Interval = value;
            }

            public void startTimer(int milliSeconds)
            {
                Enabled = false;
                Interval = milliSeconds;
                Enabled = true;
            }

            public void startTimer(int milliSeconds, dynamic setTag)
            {
                Tag = setTag;
                startTimer(milliSeconds);
            }

            public void startTimerSeconds(int seconds)
            {
                Enabled = false;
                IntervalSeconds = seconds; // was Interval (milliseconds)
                Enabled = true;
            }

            public void startTimerSeconds(int seconds, dynamic setTag)
            {
                Tag = setTag;
                startTimerSeconds(seconds);
            }

            public void stopTimer()
            {
                Enabled = false;
            }

            private void dispatcherTimer_Tick(object sender, EventArgs e)
            {
                if (Action != null)
                {
                    Action.Invoke();
                }
            }
        }

        public class CommandBase : ICommand
        {
            private readonly Func<bool> mCanExecute;
            private readonly Action<object> mExecute;

            public CommandBase(Action<object> vExecute, Func<bool> fCanExecute = null)
            {
                mCanExecute = fCanExecute;
                mExecute = vExecute;
            }

#pragma warning disable CS0067
            public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

            public bool CanExecute(object parameter)
            {
                return mCanExecute == null ? true : mCanExecute.Invoke();
            }

            public void Execute(object parameter)
            {
                mExecute.Invoke(parameter);
            }
        }
    }
}