using System;
using System.Linq;

namespace Eshava.Report.Pdf.PdfFonts
{
	/// <summary>
	/// Origin of the code
	/// https://github.com/cleversolutions/pdfSharpTextExtrationTests
	/// </summary>
	public class FontResource
	{
		private readonly CMap _cmap;
		private PdfSharpCore.Pdf.PdfArray _differences;
		private readonly bool _isStandardEncoding;

		///
		/// Parse the fonts from the page's resources structure including the encoding differences and CMAPs
		///
		public FontResource(string fontName, PdfSharpCore.Pdf.Advanced.PdfReference resource)
		{
			var resourceElements = (resource?.Value as PdfSharpCore.Pdf.PdfDictionary)?.Elements;
			
			// Extract the encoding differences array
			var differences = resourceElements?.GetDictionary("/Encoding")?.Elements?.GetArray("/Differences");
			if (differences != null)
			{
				_differences = differences;
			}

			// A simple font naming the standard encoding, whose codes differ from the Latin-1 the fallback below assumes.
			// The other named encodings are not read yet.
			_isStandardEncoding = resourceElements?["/Encoding"] is PdfSharpCore.Pdf.PdfName encoding && encoding.Value == "/StandardEncoding";

			// Extract the CMAPs
			var unicodeDictionary = resourceElements?.GetDictionary("/ToUnicode");
			var stream = unicodeDictionary?.Stream;
			if (stream != null)
			{
				_cmap = new CMap(stream, fontName);
			}
		}

		public string Encode(string text)
		{

			// Convert any characters that fall in the /Encoding /Differences array
			if (_differences != null && _differences.Elements.Count > 0)
			{
				var glyphMap = AdobeGlyfList.Instance;
				var chars = text.ToCharArray().Select(ch =>
				{
					if (_differences.Elements.Count > ch)
					{
						var item = _differences.Elements[ch];
						if (item is PdfSharpCore.Pdf.PdfName name)
						{
							return glyphMap.Lookup(name.Value);
						}
						if (item is PdfSharpCore.Pdf.PdfInteger number)
						{
							return ((char)number.Value).ToString();
						}
					}

					return ch.ToString();
				});

				return String.Concat(chars);
			}

			// If this font has a /ToUnciode CMAP then we will first resolve the text through it
			if (_cmap != null)
			{
				return _cmap.Encode(text);
			}

			// Without a map the codes are read through the font's encoding
			if (_isStandardEncoding)
			{
				return StandardEncoding.Decode(text);
			}

			// Fallback on just displaying the text

			return text;
		}
	}
}