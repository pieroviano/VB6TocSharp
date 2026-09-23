using System;
using System.Collections.Generic;
using System.Data;

namespace Extras;

/// <summary>
/// Minimal CopyToDataTable extension for DataRow sequences.
/// Behaves like DataTableExtensions.CopyToDataTable but returns an empty DataTable when the
/// source contains no rows (instead of throwing).
/// </summary>
public static class DataRowExtensions
{
    public static DataTable CopyToDataTable(this IEnumerable<DataRow> rows)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));

        DataTable result = null;

        foreach (var row in rows)
        {
            if (row == null) continue;

            if (result == null)
            {
                // Clone schema from the first available row's table
                result = row.Table?.Clone() ?? new DataTable();
            }

            // ImportRow preserves the row state and schema
            result.ImportRow(row);
        }

        // If no rows were present, return an empty DataTable
        if (result == null)
        {
            return new DataTable();
        }

        return result;
    }

    public static DataTable CopyToDataTable(this DataRow[] rows)
    {
        return CopyToDataTable((IEnumerable<DataRow>)rows);
    }
}