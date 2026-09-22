using System;
using System.Text;
using System.Collections.Generic;
using System.Data;

// Minimal CSV helper functions used by CsvRecord
public static class ModCsv
{
        public static string ProtectCSV(string s)
        {
            if (s == null) return "";
            bool needQuotes = false;
            if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0)
                needQuotes = true;

            string outStr = s.Replace("\"", "\"\"");
            if (needQuotes) outStr = "\"" + outStr + "\"";
            return outStr;
        }

        public static string CSVLine(string[] fields)
        {
            if (fields == null || fields.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ProtectCSV(fields[i] ?? string.Empty));
            }
            return sb.ToString();
        }

        public static string CSVField(string line, int index)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            int len = line.Length;
            int field = 0;
            int i = 0;

            while (i <= len)
            {
                if (i == len)
                {
                    // end reached, if asking for this field return empty
                    if (field == index) return string.Empty;
                    break;
                }

                StringBuilder cur = new StringBuilder();
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

        public static int CSVFieldCount(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0;
            int len = line.Length;
            int i = 0;
            int count = 0;

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
