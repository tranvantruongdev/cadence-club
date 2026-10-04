using System.Collections.Generic;
using NUnit.Framework;
using Template.Core.Data;

namespace Template.Core.Tests
{
    public class CsvTableTests
    {
        [Test]
        public void Parses_header_and_rows()
        {
            var table = CsvTable.Parse("id,name,price\na,Apple,10\nb,Banana,20\n", "fruit.csv");

            CollectionAssert.AreEqual(new[] { "id", "name", "price" }, table.Headers);
            Assert.AreEqual(2, table.Rows.Count);
            Assert.AreEqual("Banana", table.Rows[1].Get("name"));
            Assert.AreEqual(20, table.Rows[1].GetInt("price"));
            Assert.AreEqual(3, table.Rows[1].LineNumber);
        }

        [Test]
        public void Handles_quotes_commas_escaped_quotes_and_newlines()
        {
            const string csv = "id,text\n1,\"Hello, world\"\n2,\"She said \"\"hi\"\"\"\n3,\"two\nlines\"\n4,after\n";
            var table = CsvTable.Parse(csv);

            Assert.AreEqual("Hello, world", table.Rows[0].Get("text"));
            Assert.AreEqual("She said \"hi\"", table.Rows[1].Get("text"));
            Assert.AreEqual("two\nlines", table.Rows[2].Get("text"));
            Assert.AreEqual(4, table.Rows[2].LineNumber);
            Assert.AreEqual(6, table.Rows[3].LineNumber, "Line numbers account for the newline inside quotes.");
        }

        [Test]
        public void Handles_crlf_bom_blank_lines_and_comments()
        {
            const string csv = "﻿id,value\r\n# designer note\r\na,1\r\n\r\nb,2";
            var table = CsvTable.Parse(csv);

            Assert.AreEqual("id", table.Headers[0]);
            Assert.AreEqual(2, table.Rows.Count);
            Assert.AreEqual("b", table.Rows[1].Get("id"));
        }

        [Test]
        public void Missing_trailing_cells_read_as_empty()
        {
            var table = CsvTable.Parse("id,name,note\na,Apple\n");
            Assert.AreEqual("", table.Rows[0].Get("note"));
            Assert.AreEqual("fallback", table.Rows[0].GetOrDefault("note", "fallback"));
            Assert.AreEqual("fallback", table.Rows[0].GetOrDefault("unknown", "fallback"));
        }

        [Test]
        public void Too_many_cells_reports_line()
        {
            var e = Assert.Throws<CsvFormatException>(() => CsvTable.Parse("id,name\na,b,c\n", "x.csv"));
            Assert.AreEqual(2, e.Line);
            StringAssert.Contains("x.csv line 2", e.Message);
        }

        [Test]
        public void Unclosed_quote_reports_line()
        {
            var e = Assert.Throws<CsvFormatException>(() => CsvTable.Parse("id,text\n1,ok\n2,\"broken\n"));
            Assert.AreEqual(3, e.Line);
        }

        [Test]
        public void Bad_header_is_rejected()
        {
            Assert.Throws<CsvFormatException>(() => CsvTable.Parse(""));
            Assert.Throws<CsvFormatException>(() => CsvTable.Parse("id,,name\n"));
            Assert.Throws<CsvFormatException>(() => CsvTable.Parse("id,id\n"));
        }

        [Test]
        public void Typed_getters_use_invariant_culture_and_explain_errors()
        {
            var table = CsvTable.Parse("n,f,b\n12,1.5,yes\nx,1,maybe\n");
            Assert.AreEqual(12, table.Rows[0].GetInt("n"));
            Assert.AreEqual(1.5f, table.Rows[0].GetFloat("f"));
            Assert.IsTrue(table.Rows[0].GetBool("b"));

            var e = Assert.Throws<CsvFormatException>(() => table.Rows[1].GetInt("n"));
            StringAssert.Contains("whole number", e.Message);
            Assert.Throws<CsvFormatException>(() => table.Rows[1].GetBool("b"));
            Assert.Throws<CsvFormatException>(() => table.Rows[1].Get("missing"));
        }

        [Test]
        public void Sample_items_collects_all_validation_errors()
        {
            const string csv = "id,name,rarity,price\n" +
                               "sword,Sword,R,100\n" +
                               "sword,Copy,R,100\n" +
                               "gem,Gem,UR,50\n" +
                               ",Nameless,R,1\n" +
                               "coin,Coin,SR,-5\n" +
                               "bad,Bad,R,ten\n" +
                               "crown,Crown,SSR,999\n";
            var errors = new List<string>();
            var items = SampleItemTable.Read(CsvTable.Parse(csv, SampleItemTable.FileName), errors);

            Assert.AreEqual(2, items.Count);
            Assert.AreEqual("crown", items[1].id);
            Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("duplicate id 'sword'", errors[0]);
        }
    }
}
