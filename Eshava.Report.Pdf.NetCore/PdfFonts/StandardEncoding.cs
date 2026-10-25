using System.Collections.Generic;
using System.Linq;

namespace Eshava.Report.Pdf.PdfFonts
{
	/// <summary>
	/// The Adobe standard Latin-text encoding, PDF 32000-1:2008, Annex D.2
	/// Only the codes that differ from Latin-1 are listed, every other code reads as Latin-1
	/// </summary>
	public static class StandardEncoding
	{
		private static readonly Dictionary<char, char> _differencesToLatin1 = new Dictionary<char, char>
		{
			{ (char)0x0027, (char)0x2019 }, // quoteright
			{ (char)0x0060, (char)0x2018 }, // quoteleft
			{ (char)0x00A4, (char)0x2044 }, // fraction
			{ (char)0x00A6, (char)0x0192 }, // florin
			{ (char)0x00A8, (char)0x00A4 }, // currency
			{ (char)0x00A9, (char)0x0027 }, // quotesingle
			{ (char)0x00AA, (char)0x201C }, // quotedblleft
			{ (char)0x00AC, (char)0x2039 }, // guilsinglleft
			{ (char)0x00AD, (char)0x203A }, // guilsinglright
			{ (char)0x00AE, (char)0xFB01 }, // fi
			{ (char)0x00AF, (char)0xFB02 }, // fl
			{ (char)0x00B1, (char)0x2013 }, // endash
			{ (char)0x00B2, (char)0x2020 }, // dagger
			{ (char)0x00B3, (char)0x2021 }, // daggerdbl
			{ (char)0x00B4, (char)0x00B7 }, // periodcentered
			{ (char)0x00B7, (char)0x2022 }, // bullet
			{ (char)0x00B8, (char)0x201A }, // quotesinglbase
			{ (char)0x00B9, (char)0x201E }, // quotedblbase
			{ (char)0x00BA, (char)0x201D }, // quotedblright
			{ (char)0x00BC, (char)0x2026 }, // ellipsis
			{ (char)0x00BD, (char)0x2030 }, // perthousand
			{ (char)0x00C1, (char)0x0060 }, // grave
			{ (char)0x00C2, (char)0x00B4 }, // acute
			{ (char)0x00C3, (char)0x02C6 }, // circumflex
			{ (char)0x00C4, (char)0x02DC }, // tilde
			{ (char)0x00C5, (char)0x00AF }, // macron
			{ (char)0x00C6, (char)0x02D8 }, // breve
			{ (char)0x00C7, (char)0x02D9 }, // dotaccent
			{ (char)0x00C8, (char)0x00A8 }, // dieresis
			{ (char)0x00CA, (char)0x02DA }, // ring
			{ (char)0x00CB, (char)0x00B8 }, // cedilla
			{ (char)0x00CD, (char)0x02DD }, // hungarumlaut
			{ (char)0x00CE, (char)0x02DB }, // ogonek
			{ (char)0x00CF, (char)0x02C7 }, // caron
			{ (char)0x00D0, (char)0x2014 }, // emdash
			{ (char)0x00E1, (char)0x00C6 }, // AE
			{ (char)0x00E3, (char)0x00AA }, // ordfeminine
			{ (char)0x00E8, (char)0x0141 }, // Lslash
			{ (char)0x00E9, (char)0x00D8 }, // Oslash
			{ (char)0x00EA, (char)0x0152 }, // OE
			{ (char)0x00EB, (char)0x00BA }, // ordmasculine
			{ (char)0x00F1, (char)0x00E6 }, // ae
			{ (char)0x00F5, (char)0x0131 }, // dotlessi
			{ (char)0x00F8, (char)0x0142 }, // lslash
			{ (char)0x00F9, (char)0x00F8 }, // oslash
			{ (char)0x00FA, (char)0x0153 }, // oe
			{ (char)0x00FB, (char)0x00DF }, // germandbls
		};

		public static string Decode(string text)
		{
			return new string(text
				.Select(ch => _differencesToLatin1.TryGetValue(ch, out var unicode) ? unicode : ch)
				.ToArray());
		}
	}
}