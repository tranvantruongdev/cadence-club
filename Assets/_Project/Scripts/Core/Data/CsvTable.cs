using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Template.Core.Data
{
    public sealed class CsvFormatException : Exception
    {
        public CsvFormatException(string sourceName, int line, string message)
            : base($"{sourceName} line {line}: {message}")
        {
            SourceName = sourceName;
            Line = line;
        }

        public string SourceName { get; }
        public int Line { get; }
    }

    /// <summary>
    /// Master-data CSV: first row is the header, quoted fields may contain commas, quotes ("") and newlines.
    /// Blank lines and rows whose first cell starts with '#' are skipped, so designers can leave comments.
    /// </summary>
    public sealed class CsvTable
    {
        private readonly Dictionary<string, int> _columns;

        private CsvTable(string source, string[] headers, List<CsvRow> rows, Dictionary<string, int> columns)
        {
            SourceName = source;
            Headers = headers;
            Rows = rows;
            _columns = columns;
        }

        public string SourceName { get; }
        public IReadOnlyList<string> Headers { get; }
        public IReadOnlyList<CsvRow> Rows { get; }

        public bool HasColumn(string name) => _columns.ContainsKey(name);

        public static CsvTable Parse(string text, string sourceName = "csv")
        {
            var raw = ParseRaw(text ?? string.Empty, sourceName);
            if (raw.Count == 0)
            {
                throw new CsvFormatException(sourceName, 1, "File is empty; expected a header row.");
            }

            var header = raw[0];
            var headers = new string[header.Cells.Length];
            var columns = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < header.Cells.Length; i++)
            {
                string name = header.Cells[i].Trim();
                if (name.Length == 0)
                {
                    throw new CsvFormatException(sourceName, header.Line, $"Header column {i + 1} is empty.");
                }

                if (columns.ContainsKey(name))
                {
                    throw new CsvFormatException(sourceName, header.Line, $"Duplicate header '{name}'.");
                }

                headers[i] = name;
                columns[name] = i;
            }

            var rows = new List<CsvRow>(raw.Count - 1);
            for (int r = 1; r < raw.Count; r++)
            {
                var cells = raw[r].Cells;
                if (cells.Length > headers.Length)
                {
                    throw new CsvFormatException(sourceName, raw[r].Line, $"Row has {cells.Length} cells but the header has {headers.Length}.");
                }

                rows.Add(new CsvRow(sourceName, raw[r].Line, cells, columns));
            }

            return new CsvTable(sourceName, headers, rows, columns);
        }

        private struct RawRow
        {
            public int Line;
            public string[] Cells;
        }

        private static List<RawRow> ParseRaw(string text, string source)
        {
            var rows = new List<RawRow>();
            var cells = new List<string>();
            var field = new StringBuilder();
            int i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
            int line = 1;
            int rowStartLine = 1;
            int quoteStartLine = 1;
            bool inQuotes = false;
            bool fieldWasQuoted = false;

            void EndRow()
            {
                cells.Add(field.ToString());
                field.Clear();
                bool blank = cells.Count == 1 && cells[0].Trim().Length == 0 && !fieldWasQuoted;
                bool comment = cells.Count > 0 && cells[0].TrimStart().StartsWith("#", StringComparison.Ordinal);
                if (!blank && !comment)
                {
                    rows.Add(new RawRow { Line = rowStartLine, Cells = cells.ToArray() });
                }

                cells.Clear();
                fieldWasQuoted = false;
            }

            while (i < text.Length)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }

                        inQuotes = false;
                        i++;
                        continue;
                    }

                    if (c == '\n')
                    {
                        line++;
                    }

                    field.Append(c);
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        if (field.Length == 0)
                        {
                            inQuotes = true;
                            fieldWasQuoted = true;
                            quoteStartLine = line;
                        }
                        else
                        {
                            field.Append(c);
                        }

                        i++;
                        break;
                    case ',':
                        cells.Add(field.ToString());
                        field.Clear();
                        i++;
                        break;
                    case '\r':
                        i++;
                        if (i < text.Length && text[i] == '\n')
                        {
                            i++;
                        }

                        EndRow();
                        line++;
                        rowStartLine = line;
                        break;
                    case '\n':
                        i++;
                        EndRow();
                        line++;
                        rowStartLine = line;
                        break;
                    default:
                        field.Append(c);
                        i++;
                        break;
                }
            }

            if (inQuotes)
            {
                throw new CsvFormatException(source, quoteStartLine, "Quoted field is never closed.");
            }

            if (field.Length > 0 || cells.Count > 0 || fieldWasQuoted)
            {
                EndRow();
            }

            return rows;
        }
    }

    public sealed class CsvRow
    {
        private readonly string _source;
        private readonly string[] _cells;
        private readonly Dictionary<string, int> _columns;

        internal CsvRow(string source, int line, string[] cells, Dictionary<string, int> columns)
        {
            _source = source;
            LineNumber = line;
            _cells = cells;
            _columns = columns;
        }

        /// <summary>1-based line in the file where this row starts. Use it in validation messages.</summary>
        public int LineNumber { get; }

        public string Get(string column)
        {
            if (!_columns.TryGetValue(column, out int index))
            {
                throw new CsvFormatException(_source, LineNumber, $"Unknown column '{column}'.");
            }

            return index < _cells.Length ? _cells[index].Trim() : string.Empty;
        }

        public string GetOrDefault(string column, string fallback = "")
        {
            if (!_columns.ContainsKey(column))
            {
                return fallback;
            }

            string value = Get(column);
            return value.Length == 0 ? fallback : value;
        }

        public int GetInt(string column)
        {
            string value = Get(column);
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                throw new CsvFormatException(_source, LineNumber, $"Column '{column}' must be a whole number, got '{value}'.");
            }

            return result;
        }

        public float GetFloat(string column)
        {
            string value = Get(column);
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                throw new CsvFormatException(_source, LineNumber, $"Column '{column}' must be a number (use '.' for decimals), got '{value}'.");
            }

            return result;
        }

        public bool GetBool(string column)
        {
            string value = Get(column).ToLowerInvariant();
            switch (value)
            {
                case "true":
                case "1":
                case "yes":
                    return true;
                case "false":
                case "0":
                case "no":
                case "":
                    return false;
                default:
                    throw new CsvFormatException(_source, LineNumber, $"Column '{column}' must be true/false, got '{value}'.");
            }
        }
    }
}
