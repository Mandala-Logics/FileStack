using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MandalaLogics.Command
{
    public interface ITableRenderer
    {
        public string Render(ConsoleTable table);
    }

    public enum ColumnAlignment
    {
        Left,
        Right,
        Center
    }

    public sealed class ConsoleColumn
    {
        public string Header { get; }
        public ColumnAlignment Alignment { get; }

        public ConsoleColumn(string header, ColumnAlignment alignment = ColumnAlignment.Left)
        {
            Header = header;
            Alignment = alignment;
        }
    }

    public sealed class ConsoleTable
    {
        public IReadOnlyList<ConsoleColumn> Columns { get; }
        public List<IReadOnlyList<string>> Rows { get; } = new List<IReadOnlyList<string>>();

        public ConsoleTable(params ConsoleColumn[] columns)
        {
            Columns = columns;
        }

        public void AddRow(params object?[] values)
        {
            if (values.Length != Columns.Count)
                throw new ArgumentException("Row value count must match column count.");

            Rows.Add(values.Select(v => v?.ToString() ?? string.Empty).ToArray());
        }
    }

    public sealed class ConsoleTableRenderer : ITableRenderer
    {
        public string Render(ConsoleTable table)
        {
            var widths = CalculateWidths(table);

            var sb = new StringBuilder();

            sb.AppendLine(RenderSeparator(widths));
            sb.AppendLine(RenderRow(
                table.Columns.Select(c => c.Header),
                widths,
                table.Columns.Select(c => c.Alignment)
            ));
            sb.AppendLine(RenderSeparator(widths));

            foreach (var row in table.Rows)
            {
                sb.AppendLine(RenderRow(row, widths, table.Columns.Select(c => c.Alignment)));
            }

            sb.AppendLine(RenderSeparator(widths));

            return sb.ToString();
        }

        private static int[] CalculateWidths(ConsoleTable table)
        {
            return table.Columns
                .Select((c, i) =>
                    Math.Max(
                        c.Header.Length,
                        table.Rows.Any() ? table.Rows.Max(r => r[i].Length) : 0
                    )
                )
                .ToArray();
        }

        private static string RenderRow(
            IEnumerable<string> cells,
            int[] widths,
            IEnumerable<ColumnAlignment> alignments)
        {
            var formatted = cells
                .Zip(widths, (cell, width) => (cell, width))
                .Zip(alignments, (x, align) => Align(x.cell, x.width, align));

            return "| " + string.Join(" | ", formatted) + " |";
        }

        private static string RenderSeparator(int[] widths)
        {
            return "+-" + string.Join("-+-", widths.Select(w => new string('-', w))) + "-+";
        }

        private static string Align(string text, int width, ColumnAlignment alignment)
        {
            return alignment switch
            {
                ColumnAlignment.Right => text.PadLeft(width),
                ColumnAlignment.Center =>
                    text.PadLeft((width + text.Length) / 2).PadRight(width),
                _ => text.PadRight(width),
            };
        }
    }

    public sealed class JsonTableRenderer : ITableRenderer
    {
        public string Render(ConsoleTable table)
        {
            var sb = new StringBuilder();
            sb.Append("[\n");

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                sb.Append("  {\n");

                for (int c = 0; c < table.Columns.Count; c++)
                {
                    sb.Append("    \"")
                    .Append(JsonEscape(table.Columns[c].Header))
                    .Append("\": ");

                    sb.Append("\"")
                    .Append(JsonEscape(row[c]))
                    .Append("\"");

                    if (c < table.Columns.Count - 1)
                        sb.Append(",");

                    sb.Append("\n");
                }

                sb.Append("  }");

                if (r < table.Rows.Count - 1)
                    sb.Append(",");

                sb.Append("\n");
            }

            sb.Append("]");
            return sb.ToString();
        }

        public static string JsonEscape(string s)
        {
            var sb = new StringBuilder(s.Length + 8);

            foreach (var c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

    }
}