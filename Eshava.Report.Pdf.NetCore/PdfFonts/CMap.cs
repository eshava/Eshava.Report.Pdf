using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Eshava.Report.Pdf.PdfFonts
{
	/// <summary>
	/// Origin of the code
	/// https://github.com/cleversolutions/pdfSharpTextExtrationTests
	/// </summary>
	public class CMap
	{
		private readonly string _cMapStr;
		private readonly string _fontName;

		public List<CodeSpaceRange> CodeSpaceRanges { get; set; }

		public CMap(PdfSharpCore.Pdf.PdfDictionary.PdfStream stream, string fontName)
		{
			CodeSpaceRanges = new List<CodeSpaceRange>();

			_cMapStr = stream.ToString();
			_fontName = fontName;

			// First parse the code space ranges
			ParseCodeSpaceRanges();

			// Parse the bfchar and bfrange mappings
			ParseMappings(_cMapStr);
		}

		public string Encode(string text)
		{
			var chars = text.ToCharArray();
			var result = new StringBuilder();

			// Find a substitution for every char in the text
			for (var chrIdx = 0; chrIdx < chars.Length; chrIdx++)
			{
				// Ranges should not overlap, but the spec and the real world...
				// Start with 1 byte, see if we find a 1 byte match. If not try 2 bytes etc.
				// CodeSpaceRange.NumberOfBytes should indicate how many bytes we map, but it doesn't in real life
				// A longer code is only tried while the text still holds that many bytes
				for (var numberOfBytes = 1; numberOfBytes <= 4 && chrIdx + numberOfBytes <= chars.Length; numberOfBytes++)
				{
					var cid = 0;
					for (var byteIdx = 0; byteIdx < numberOfBytes; byteIdx++)
					{
						cid = cid << 8 | chars[chrIdx + byteIdx];
					}

					var range = CodeSpaceRanges.FirstOrDefault(r => r.Low <= cid && r.High >= cid);
					if (range != null && range.Mapping.TryGetValue(cid, out var map) && map.SourceByteLength == numberOfBytes)
					{
						result.Append(map.UnicodeValue);
						chrIdx += numberOfBytes - 1;

						break;
					}
				}

				// A code without a mapping is left out: its value identifies a glyph, not a character
			}

			return result.ToString();
		}

		///
		/// Parse the code space ranges begincodespacerange to endcodespacerange
		/// There can be several ranges, and they may be 1, 2, 3 or 4 bytes that don't overlap
		/// Parse each range, then build a cid to unicode map in each range.
		///
		private void ParseCodeSpaceRanges()
		{
			// Find the text between begincodespacerange and endcodespacerange
			var codespaceStartIdx = _cMapStr.IndexOf("begincodespacerange");
			var codespaceLength = _cMapStr.IndexOf("endcodespacerange") - codespaceStartIdx;
			var ranges = _cMapStr.Substring(codespaceStartIdx + 19, codespaceLength - 19);

			// Extract each low/high pair
			Match match;
			var pattern = @"\s?<([a-fA-F0-9]+)>\s?<([a-fA-F0-9]+)>";
			while ((match = Regex.Match(ranges, pattern)).Success)
			{
				// Pop the match from the ranges
				ranges = ranges.Substring(match.Length);

				// We should match 2 numbers
				if (match.Groups.Count == 3)
				{
					// Each range must be representable by an int
					if (match.Groups[1].Value.Length > 8)
					{
						continue;
					}

					var strLength = match.Groups[1].Length;
					var low = Convert.ToInt32(match.Groups[1].Value, 16);
					var high = Convert.ToInt32(match.Groups[2].Value, 16);
					CodeSpaceRanges.Add(new CodeSpaceRange
					{
						Low = low,
						High = high,
						NumberOfBytes = match.Groups[1].Length / 2
					});
				}
			}

			// Order our code space ranges for the lookups
			CodeSpaceRanges = CodeSpaceRanges
				.OrderBy(r => r.NumberOfBytes)
				.ThenBy(r => r.Low)
				.ToList();
		}

		public void ParseMappings(string cMap)
		{
			// ToDo: Check for usecmap -- we can refer to other CMAPs including built-in ones...
			var beginbfcharIdx = cMap.IndexOf("beginbfchar");
			var beginbfrangeIdx = cMap.IndexOf("beginbfrange");
			var bfCharLen = cMap.IndexOf("endbfchar") - beginbfcharIdx;
			var bfRangeLen = cMap.IndexOf("endbfrange") - beginbfrangeIdx;

			// If we have both, take the first one
			if (beginbfcharIdx >= 0 && beginbfrangeIdx >= 0)
			{
				if (beginbfcharIdx < beginbfrangeIdx)
				{
					ParseBFChar(cMap.Substring(beginbfcharIdx + 11, bfCharLen - 11));
					cMap = cMap.Substring(beginbfcharIdx + 11 + bfCharLen + 9 - 11);
				}
				else
				{
					ParseBFRange(cMap.Substring(beginbfrangeIdx + 12, bfRangeLen - 12));
					cMap = cMap.Substring(beginbfrangeIdx + 12 + bfRangeLen + 10 - 12);
				}
			}
			else if (beginbfcharIdx >= 0)
			{
				ParseBFChar(cMap.Substring(beginbfcharIdx + 11, bfCharLen - 11));
				cMap = cMap.Substring(beginbfcharIdx + 11 + bfCharLen + 9 - 11);
			}
			else if (beginbfrangeIdx >= 0)
			{
				ParseBFRange(cMap.Substring(beginbfrangeIdx + 12, bfRangeLen - 12));
				cMap = cMap.Substring(beginbfrangeIdx + 12 + bfRangeLen + 10 - 12);
			}
			else
			{
				// There is nothing left to parse

				return;
			}

			// Recurse until there is nothing left to parse
			ParseMappings(cMap);
		}

		private void AddMapping(int cid, string ucode, int lengthInBytes)
		{
			// Find the proper codespace range and add the mapping
			var range = CodeSpaceRanges.FirstOrDefault(r => r.Low <= cid && r.High >= cid);
			if (range != null)
			{
				range.AddMap(cid, ucode, lengthInBytes);
			}
		}

		private string ConvertDstCode(string dstCode)
		{
			string ucode = null;
			var length = dstCode.Length;

			if (length <= 4)
			{
				// If the dstCode is 4 digit hex, convert it to a 1 char string
				var ch = (char)Convert.ToInt16(dstCode, 16);
				ucode = ch.ToString();
			}
			else if (length % 4 == 0)
			{
				// If dstCode is a multiple of 4, convert it into several char string
				var chars = Enumerable.Range(0, length / 4)
					.Select(i => dstCode.Substring(i * 4, 4))
					.Select(str => (char)Convert.ToInt16(str, 16));

				ucode = String.Concat(chars);
			}
			return ucode;
		}

		///
		/// Pase the contents of a CMAP table from beginbfchar to endbfchar
		///
		private void ParseBFChar(string bfChar)
		{
			// ToDO: This assumes src and dst are both in hex format; however dst can be dstCharname 
			var pattern = @"\s?<([a-fA-F0-9]+)>\s?<([a-fA-F0-9]+)>";
			Match match;
			
			while ((match = Regex.Match(bfChar, pattern)).Success)
			{
				// Pop the match from bfChar
				bfChar = bfChar.Substring(match.Length);

				// Extract the cid and unicode and add it to our mapping
				if (match.Groups.Count == 3)
				{
					try
					{
						// The srcCode must be representable by an int
						var srcCode = Convert.ToInt32(match.Groups[1].Value, 16);
						var srcCodeByteLength = match.Groups[1].Value.Length / 2;

						// Convert the dstCode to a string
						var ucode = ConvertDstCode(match.Groups[2].Value);
						if (ucode == null)
						{
							continue;
						}

						AddMapping(srcCode, ucode, srcCodeByteLength);
					}
					catch (Exception)
					{
						// ToDo:  I think this happens when multiple cids match one ucode
						// They are all crammed into 1 big number. We need to know how many bytes
						// The map uses, and loop over the cids.
					}
				}
				else
				{
					break;
				}
			}
		}

		///
		/// Parse the contents of a CMAP file from beginbfrange to endbfrange
		/// This will generate a mapping for each character in each range
		///
		private void ParseBFRange(string fbRange)
		{
			var pattern = @"\s?<([a-fA-F0-9]+)>\s?<([a-fA-F0-9]+)>\s?<([a-fA-F0-9]+)>";
			Match match;
			
			while ((match = Regex.Match(fbRange, pattern)).Success)
			{
				// Pop the match from bfChar
				fbRange = fbRange.Substring(match.Length);

				if (match.Groups.Count == 4)
				{
					// Convert our matches to ints
					var srcCodeLow = Convert.ToInt32(match.Groups[1].Value, 16);
					var srcCodeHigh = Convert.ToInt32(match.Groups[2].Value, 16);
					var dstCodeLow = (char)Convert.ToInt16(match.Groups[3].Value, 16);

					// Ensure to is > then from
					if (srcCodeLow > srcCodeHigh)
					{
						continue;
					}

					// Map all chars from fromGlyf to toGlyf and add
					for (var i = 0; srcCodeLow + i <= srcCodeHigh; i++)
					{
						var glyf = srcCodeLow + i;
						var dstCode = (char)(dstCodeLow + i);
						
						AddMapping(glyf, dstCode.ToString(), match.Groups[1].Value.Length / 2);
					}
				}
				else
				{
					// The format may have been srcCodeLo srcCodeHi [/dstCharName1../dstCharNamen]
					// where dstCharName is a postscript language name object ie. /quotesingle
				}
			}
		}
	}
}