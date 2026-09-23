using System.Collections.Generic;
using System.Text;

// Minimal Csv helper functions used by CsvRecord
namespace Extras;

public static class CsvHandler
{
    public static string ProtectCsv(string s)
    {
        if (s == null) return "";
        var needQuotes = false;
        if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0)
            needQuotes = true;

        var outStr = s.Replace("\"", "\"\"");
        if (needQuotes) outStr = "\"" + outStr + "\"";
        return outStr;
    }

    // Splits a Csv file into its records. A newline inside a quoted field belongs to the field, so
    // the file cannot simply be split on '\n'.
    public static List<string> CsvRecords(string contents)
    {
        var res = new List<string>();
        if (string.IsNullOrEmpty(contents)) return res;

        var cur = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < contents.Length; i++)
        {
            var c = contents[i];
            if (c == '"')
            {
                // An escaped "" flips this twice, which leaves it where it was.
                inQuotes = !inQuotes;
                cur.Append(c);
                continue;
            }

            if (!inQuotes && (c == '\n' || c == '\r'))
            {
                if (c == '\r' && i + 1 < contents.Length && contents[i + 1] == '\n') i++;
                res.Add(cur.ToString());
                cur.Length = 0;
                continue;
            }

            cur.Append(c);
        }

        if (cur.Length > 0) res.Add(cur.ToString());
        return res;
    }

    public static string CsvLine(string[] fields)
    {
        if (fields == null || fields.Length == 0) return string.Empty;
        var sb = new StringBuilder();
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(ProtectCsv(fields[i] ?? string.Empty));
        }
        return sb.ToString();
    }

    public static string CsvField(string line, int index)
    {
        if (string.IsNullOrEmpty(line)) return string.Empty;
        var len = line.Length;
        var field = 0;
        var i = 0;

        while (i <= len)
        {
            if (i == len)
            {
                // end reached, if asking for this field return empty
                if (field == index) return string.Empty;
                break;
            }

            var cur = new StringBuilder();
            if (line[i] == '"')
            {
                // quoted field
                i++; // skip opening quote
                while (i < len)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < len && line[i + 1] == '"')
                        {
                            cur.Append('"');
                            i += 2; // escaped quote
                            continue;
                        }
                        else
                        {
                            i++; // closing quote
                            break;
                        }
                    }
                    cur.Append(line[i]);
                    i++;
                }

                // skip until comma or end
                while (i < len && line[i] != ',') i++;
            }
            else
            {
                // unquoted field
                while (i < len && line[i] != ',')
                {
                    cur.Append(line[i]);
                    i++;
                }
            }

            // At this point current field parsed
            if (field == index) return cur.ToString();

            field++;

            // skip comma
            if (i < len && line[i] == ',') i++;
        }

        return string.Empty;
    }

    public static int CsvFieldCount(string line)
    {
        if (string.IsNullOrEmpty(line)) return 0;
        var len = line.Length;
        var i = 0;
        var count = 0;

        while (i <= len)
        {
            if (i == len)
            {
                // reached end: if last char was comma, there is an empty final field
                if (len > 0 && line[len - 1] == ',') count++;
                break;
            }

            if (line[i] == '"')
            {
                i++; // skip opening quote
                while (i < len)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < len && line[i + 1] == '"')
                        {
                            i += 2; // escaped quote
                            continue;
                        }
                        else
                        {
                            i++; // closing quote
                            break;
                        }
                    }
                    i++;
                }

                // skip until comma or end
                while (i < len && line[i] != ',') i++;
            }
            else
            {
                // unquoted
                while (i < len && line[i] != ',') i++;
            }

            // consume comma if present and count a field
            if (i < len && line[i] == ',')
            {
                count++;
                i++; // skip comma and continue
            }
            else if (i >= len)
            {
                // end of line -> count last field
                count++;
                break;
            }
        }

        return count;
    }
}