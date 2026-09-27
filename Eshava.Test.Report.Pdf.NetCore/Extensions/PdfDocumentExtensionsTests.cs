using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Eshava.Report.Pdf.Extensions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.IO;

namespace Eshava.Test.Report.Pdf.NetCore.Extensions
{
	[TestClass, TestCategory("Eshava.Report.Pdf.NetCore")]
	public class PdfDocumentExtensionsTests
	{
		// Both fonts use two byte codes, as a PDF writer does for embedded Unicode fonts.
		// The codes of the bold font do not exist in the regular one, so decoding with the wrong font is visible.
		private const string CMAP_REGULAR = "<0001> <0003> <0061>";
		private const string CMAP_BOLD = "<0010> <0012> <0058>";

		[TestMethod]
		public void ExtractTextUsesTheFontSetLastInTheTextObjectTest()
		{
			// Arrange
			var content = "BT /F0 10 Tf 50 700 Td <00010002> Tj /F1 10 Tf 0 -20 Td <00100011> Tj ET";

			// Act
			var result = ExtractText(content);

			// Assert
			result.Should().Equal("ab", "XY");
		}

		[TestMethod]
		public void ExtractTextKeepsTheFontAcrossTextObjectsTest()
		{
			// Arrange
			var content = "BT /F1 10 Tf 50 700 Td <0010> Tj ET BT 50 600 Td <00120010> Tj ET";

			// Act
			var result = ExtractText(content);

			// Assert
			result.Should().Equal("X", "ZX");
		}

		[TestMethod]
		public void ExtractTextRestoresTheFontWithTheGraphicsStateTest()
		{
			// Arrange
			var content = "BT /F1 10 Tf 50 700 Td <0010> Tj ET q BT /F0 10 Tf 50 600 Td <0003> Tj ET Q BT 50 500 Td <0011> Tj ET";

			// Act
			var result = ExtractText(content);

			// Assert
			result.Should().Equal("X", "c", "Y");
		}

		[TestMethod]
		public void ExtractTextAcceptsAFontSetOutsideATextObjectTest()
		{
			// Arrange
			var content = "q /F0 10 Tf BT 50 700 Td <00010003> Tj ET Q";

			// Act
			var result = ExtractText(content);

			// Assert
			result.Should().Equal("ac");
		}

		private static List<string> ExtractText(string content)
		{
			using var stream = new MemoryStream();
			CreateDocument(content).Save(stream, false);
			stream.Position = 0;

			var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

			return document.ExtractText()
				.Select(textContent => textContent.Value)
				.ToList();
		}

		private static PdfDocument CreateDocument(string content)
		{
			var document = new PdfDocument();
			var page = document.AddPage();

			var fonts = new PdfDictionary(document);
			fonts.Elements["/F0"] = CreateFont(document, CMAP_REGULAR);
			fonts.Elements["/F1"] = CreateFont(document, CMAP_BOLD);
			page.Resources.Elements["/Font"] = fonts;

			page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes(content));

			return document;
		}

		/// <summary>
		/// A font is only read for its /ToUnicode map, so the font program itself is left out
		/// </summary>
		private static PdfReference CreateFont(PdfDocument document, string bfRange)
		{
			var toUnicode = new PdfDictionary(document);
			toUnicode.CreateStream(Encoding.ASCII.GetBytes(
				"/CIDInit /ProcSet findresource begin\n"
				+ "12 dict begin\n"
				+ "begincmap\n"
				+ "1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n"
				+ $"1 beginbfrange\n{bfRange}\nendbfrange\n"
				+ "endcmap\n"
				+ "end\nend\n"
			));
			document.Internals.AddObject(toUnicode);

			var font = new PdfDictionary(document);
			font.Elements["/Type"] = new PdfName("/Font");
			font.Elements["/Subtype"] = new PdfName("/Type0");
			font.Elements["/ToUnicode"] = toUnicode.Reference;
			document.Internals.AddObject(font);

			return font.Reference;
		}
	}
}