using GameDBEditorLibrary.Automation;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace GameDBLibrary.Tests
{
    public class GameDBCsvCodecTests
    {
        [Test]
        public void Parse_AcceptsBomLfCrLfQuotesEmbeddedNewlinesAndTrailingEmptyCells()
        {
            var parsed = GameDBCsvCodec.Parse(
                "\uFEFF__key,Name,Empty\r\nSword,\"line 1, \"\"quoted\"\"\r\nline 2\"," +
                "\nAxe,Axe,");

            Assert.That(parsed.Success, Is.True, parsed.Error?.Message);
            Assert.That(parsed.Records.Count, Is.EqualTo(3));
            Assert.That(parsed.Records[0].Cells.Select(cell => cell.Text),
                Is.EqualTo(new[] { "__key", "Name", "Empty" }));
            Assert.That(parsed.Records[1].Cells.Select(cell => cell.Text),
                Is.EqualTo(new[] { "Sword", "line 1, \"quoted\"\r\nline 2", string.Empty }));
            Assert.That(parsed.Records[2].Cells.Select(cell => cell.Text),
                Is.EqualTo(new[] { "Axe", "Axe", string.Empty }));
            Assert.That(parsed.Records[2].LineNumber, Is.EqualTo(4));
        }

        [Test]
        public void Write_UsesDeterministicCrLfAndMinimalRfc4180Quoting()
        {
            var csv = GameDBCsvCodec.Write(new List<IReadOnlyList<string>>
            {
                new[] { "Key", "Name" },
                new[] { "Sword", "line 1, \"quoted\"\nline 2" },
                new[] { "Axe", string.Empty }
            });

            Assert.That(csv, Is.EqualTo(
                "Key,Name\r\nSword,\"line 1, \"\"quoted\"\"\nline 2\"\r\nAxe,"));
        }

        [TestCase("=SUM(A1:A2)", "'=SUM(A1:A2)")]
        [TestCase("+command", "'+command")]
        [TestCase("-12", "'-12")]
        [TestCase("@name", "'@name")]
        [TestCase("\tvalue", "'\tvalue")]
        [TestCase("\rvalue", "'\rvalue")]
        [TestCase("\nvalue", "'\nvalue")]
        [TestCase("'literal", "''literal")]
        [TestCase("plain", "plain")]
        [TestCase("", "")]
        public void FormulaEscaping_IsReversible(string original, string escaped)
        {
            Assert.That(GameDBCsvCodec.EscapeFormula(original), Is.EqualTo(escaped));
            Assert.That(GameDBCsvCodec.UnescapeFormula(escaped), Is.EqualTo(original));
        }

        [TestCase("__key,Name\r\nSword,\"unterminated", "csv.unterminatedQuote", 2, 2, 2)]
        [TestCase("__key,Name\r\nSword,bad\"quote", "csv.quoteInUnquotedField", 2, 2, 2)]
        [TestCase("__key,Name\r\nSword,\"closed\"tail", "csv.trailingQuotedData", 2, 2, 2)]
        [TestCase("__key,Name\rSword,Sword", "csv.bareCarriageReturn", 1, 1, 2)]
        [TestCase("__key,Name\r\nSword", "csv.recordWidth", 2, 2, 2)]
        [TestCase("__key,Name\r\nSword,Sword,Extra", "csv.recordWidth", 2, 2, 3)]
        [TestCase("__key,Name\r\n\r\nSword,Sword", "csv.blankRecord", 2, 2, 1)]
        public void Parse_RejectsMalformedCsvWithCoordinates(string csv, string code,
            int record, int line, int column)
        {
            var parsed = GameDBCsvCodec.Parse(csv);

            Assert.That(parsed.Success, Is.False);
            Assert.That(parsed.Error.Code, Is.EqualTo(code));
            Assert.That(parsed.Error.RecordNumber, Is.EqualTo(record));
            Assert.That(parsed.Error.LineNumber, Is.EqualTo(line));
            Assert.That(parsed.Error.ColumnNumber, Is.EqualTo(column));
        }
    }
}
