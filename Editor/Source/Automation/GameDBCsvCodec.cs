using System;
using System.Collections.Generic;
using System.Text;

namespace GameDBEditorLibrary.Automation
{
    internal sealed class GameDBCsvCell
    {
        internal string Text { get; set; }
        internal int RecordNumber { get; set; }
        internal int LineNumber { get; set; }
        internal int ColumnNumber { get; set; }
    }

    internal sealed class GameDBCsvRecord
    {
        internal int RecordNumber { get; set; }
        internal int LineNumber { get; set; }
        internal List<GameDBCsvCell> Cells { get; } = new List<GameDBCsvCell>();
    }

    internal sealed class GameDBCsvParseResult
    {
        internal bool Success => Error == null;
        internal List<GameDBCsvRecord> Records { get; } = new List<GameDBCsvRecord>();
        internal GameDBCsvError Error { get; set; }
    }

    internal static class GameDBCsvCodec
    {
        internal static GameDBCsvParseResult Parse(string text)
        {
            var result = new GameDBCsvParseResult();
            if (text == null)
            {
                result.Error = Error("csv.textRequired", "CSV text is required.", 1, 1, 1);
                return result;
            }

            var index = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
            var line = 1;
            var recordNumber = 1;
            while (index < text.Length)
            {
                var record = new GameDBCsvRecord
                {
                    RecordNumber = recordNumber,
                    LineNumber = line
                };
                var columnNumber = 1;
                while (true)
                {
                    var cellLine = line;
                    var builder = new StringBuilder();
                    var quoted = false;
                    var closedQuote = false;
                    var endedByDelimiter = false;
                    if (index < text.Length && text[index] == '"')
                    {
                        quoted = true;
                        index++;
                    }

                    while (index < text.Length)
                    {
                        var current = text[index];
                        if (quoted && !closedQuote)
                        {
                            if (current == '"')
                            {
                                if (index + 1 < text.Length && text[index + 1] == '"')
                                {
                                    builder.Append('"');
                                    index += 2;
                                    continue;
                                }

                                closedQuote = true;
                                index++;
                                continue;
                            }

                            if (current == '\r')
                            {
                                if (index + 1 < text.Length && text[index + 1] == '\n')
                                {
                                    builder.Append("\r\n");
                                    index += 2;
                                }
                                else
                                {
                                    builder.Append('\r');
                                    index++;
                                }
                                line++;
                                continue;
                            }

                            if (current == '\n')
                            {
                                builder.Append('\n');
                                index++;
                                line++;
                                continue;
                            }

                            builder.Append(current);
                            index++;
                            continue;
                        }

                        if (current == ',')
                        {
                            endedByDelimiter = true;
                            index++;
                            break;
                        }

                        if (current == '\r' || current == '\n')
                        {
                            if (current == '\r')
                            {
                                if (index + 1 >= text.Length || text[index + 1] != '\n')
                                {
                                    result.Error = Error("csv.bareCarriageReturn",
                                        "A carriage return outside a quoted cell must be followed by a line feed.",
                                        recordNumber, line, columnNumber);
                                    return result;
                                }
                                index += 2;
                            }
                            else
                            {
                                index++;
                            }
                            line++;
                            AddCell(record, builder.ToString(), cellLine, columnNumber);
                            result.Records.Add(record);
                            recordNumber++;
                            goto RecordComplete;
                        }

                        if (quoted)
                        {
                            result.Error = Error("csv.trailingQuotedData",
                                "Only a delimiter or record ending may follow a closing quote.",
                                recordNumber, line, columnNumber);
                            return result;
                        }

                        if (current == '"')
                        {
                            result.Error = Error("csv.quoteInUnquotedField",
                                "A quote may only appear at the start of a quoted cell.",
                                recordNumber, line, columnNumber);
                            return result;
                        }

                        builder.Append(current);
                        index++;
                    }

                    if (quoted && !closedQuote)
                    {
                        result.Error = Error("csv.unterminatedQuote", "Quoted cell is not terminated.",
                            recordNumber, cellLine, columnNumber);
                        return result;
                    }

                    AddCell(record, builder.ToString(), cellLine, columnNumber);
                    if (index >= text.Length)
                    {
                        if (endedByDelimiter)
                        {
                            AddCell(record, string.Empty, line, columnNumber + 1);
                        }
                        result.Records.Add(record);
                        return ValidateWidths(result);
                    }
                    columnNumber++;
                }

            RecordComplete:
                if (index >= text.Length)
                {
                    break;
                }
            }

            return ValidateWidths(result);
        }

        internal static string Write(IEnumerable<IReadOnlyList<string>> records)
        {
            var builder = new StringBuilder();
            var firstRecord = true;
            foreach (var record in records)
            {
                if (!firstRecord)
                {
                    builder.Append("\r\n");
                }
                firstRecord = false;

                for (var index = 0; index < record.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }
                    AppendCell(builder, record[index] ?? string.Empty);
                }
            }
            return builder.ToString();
        }

        internal static string EscapeFormula(string value)
        {
            value = value ?? string.Empty;
            return value.Length > 0 && IsFormulaPrefix(value[0]) ? "'" + value : value;
        }

        internal static string UnescapeFormula(string value)
        {
            value = value ?? string.Empty;
            return value.Length > 1 && value[0] == '\'' && IsFormulaPrefix(value[1])
                ? value.Substring(1)
                : value;
        }

        private static bool IsFormulaPrefix(char value)
        {
            return value == '\'' || value == '=' || value == '+' || value == '-'
                || value == '@' || value == '\t' || value == '\r' || value == '\n';
        }

        private static void AddCell(GameDBCsvRecord record, string text, int line, int column)
        {
            record.Cells.Add(new GameDBCsvCell
            {
                Text = text,
                RecordNumber = record.RecordNumber,
                LineNumber = line,
                ColumnNumber = column
            });
        }

        private static GameDBCsvParseResult ValidateWidths(GameDBCsvParseResult result)
        {
            if (result.Records.Count == 0)
            {
                result.Error = Error("csv.headerRequired", "CSV must contain a header record.", 1, 1, 1);
                return result;
            }

            var width = result.Records[0].Cells.Count;
            for (var index = 1; index < result.Records.Count; index++)
            {
                if (result.Records[index].Cells.TrueForAll(cell => cell.Text.Length == 0))
                {
                    result.Error = Error("csv.blankRecord", "Blank records are not allowed.",
                        result.Records[index].RecordNumber, result.Records[index].LineNumber, 1);
                    return result;
                }

                if (result.Records[index].Cells.Count != width)
                {
                    result.Error = Error("csv.recordWidth",
                        $"Record has {result.Records[index].Cells.Count} cell(s); expected {width}.",
                        result.Records[index].RecordNumber, result.Records[index].LineNumber,
                        result.Records[index].Cells.Count < width
                            ? result.Records[index].Cells.Count + 1
                            : width + 1);
                    return result;
                }
            }
            return result;
        }

        private static void AppendCell(StringBuilder builder, string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                builder.Append(value);
                return;
            }

            builder.Append('"');
            builder.Append(value.Replace("\"", "\"\""));
            builder.Append('"');
        }

        private static GameDBCsvError Error(string code, string message,
            int recordNumber, int lineNumber, int columnNumber)
        {
            return new GameDBCsvError
            {
                Code = code,
                Message = message,
                RecordNumber = recordNumber,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber
            };
        }
    }
}
