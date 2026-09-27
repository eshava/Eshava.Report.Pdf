using System.Text;
using Eshava.Report.Pdf.PdfFonts;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharpCore.Pdf;

namespace Eshava.Test.Report.Pdf.NetCore.PdfFonts
{
	[TestClass, TestCategory("Eshava.Report.Pdf.NetCore")]
	public class CMapTests
	{
		[TestMethod]
		public void EncodeLeavesOutAnUnmappedCodeAtTheEndOfTheTextTest()
		{
			// Arrange
			var cMap = CreateCMap("<0000> <FFFF>", "1 beginbfrange\n<0001> <0003> <0061>\nendbfrange");

			// Act
			var result = cMap.Encode("\u0000\u0001\u0000\u0099");

			// Assert
			result.Should().Be("a");
		}

		[TestMethod]
		public void EncodeTwoByteCodesTest()
		{
			// Arrange
			var cMap = CreateCMap("<0000> <FFFF>", "1 beginbfrange\n<0001> <0003> <0061>\nendbfrange");

			// Act
			var result = cMap.Encode("\u0000\u0003\u0000\u0001");

			// Assert
			result.Should().Be("ca");
		}

		[TestMethod]
		public void EncodeSingleByteCodesTest()
		{
			// Arrange
			var cMap = CreateCMap("<00> <FF>", "1 beginbfrange\n<41> <43> <0061>\nendbfrange");

			// Act
			var result = cMap.Encode("ABC");

			// Assert
			result.Should().Be("abc");
		}

		[TestMethod]
		public void EncodeThreeByteCodesTest()
		{
			// Arrange
			var cMap = CreateCMap("<000000> <FFFFFF>", "1 beginbfchar\n<000001> <0041>\nendbfchar");

			// Act
			var result = cMap.Encode("\u0000\u0000\u0001");

			// Assert
			result.Should().Be("A");
		}

		[TestMethod]
		public void EncodeFourByteCodesTest()
		{
			// Arrange
			var cMap = CreateCMap("<00000000> <7FFFFFFF>", "1 beginbfchar\n<01000001> <0041>\nendbfchar");

			// Act
			var result = cMap.Encode("\u0001\u0000\u0000\u0001");

			// Assert
			result.Should().Be("A");
		}

		[TestMethod]
		public void EncodeWithAMapOfCharactersOnlyTest()
		{
			// Arrange
			var cMap = CreateCMap("<0000> <FFFF>", "2 beginbfchar\n<0001> <0041>\n<0002> <0042>\nendbfchar");

			// Act
			var result = cMap.Encode("\u0000\u0001\u0000\u0002");

			// Assert
			result.Should().Be("AB");
		}

		private static CMap CreateCMap(string codeSpaceRange, string mappings)
		{
			var dictionary = new PdfDictionary(new PdfDocument());
			dictionary.CreateStream(Encoding.ASCII.GetBytes(
				"/CIDInit /ProcSet findresource begin\n"
				+ "12 dict begin\n"
				+ "begincmap\n"
				+ $"1 begincodespacerange\n{codeSpaceRange}\nendcodespacerange\n"
				+ $"{mappings}\n"
				+ "endcmap\n"
				+ "end\nend\n"
			));

			return new CMap(dictionary.Stream, "/F0");
		}
	}
}