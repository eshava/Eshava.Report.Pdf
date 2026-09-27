using System;
using System.Collections.Generic;
using System.Linq;
using Eshava.Report.Pdf.Models;
using Eshava.Report.Pdf.Models.Internal;
using Eshava.Report.Pdf.PdfFonts;
using PdfSharpCore.Pdf.Content.Objects;
using Eshava.Report.Pdf.Core.Extensions;
using Eshava.Report.Pdf.Core.Models;

namespace Eshava.Report.Pdf.Extensions
{
	/// <summary>
	/// Origin of the code
	/// https://github.com/DavidS/PdfTextract/blob/master/PdfTextract/PdfTextExtractor.cs
	/// </summary>
	public static class PdfDocumentExtensions
	{
		public static IEnumerable<TextContent> ExtractText(this PdfSharpCore.Pdf.PdfDocument document)
		{
			return ExtractText(document, 0, document.Pages.Count - 1);
		}

		public static IEnumerable<TextContent> ExtractText(this PdfSharpCore.Pdf.PdfDocument document, int pageIndex)
		{
			return ExtractText(document, pageIndex, pageIndex);
		}

		public static IEnumerable<TextContent> ExtractText(this PdfSharpCore.Pdf.PdfDocument document, int pageIndexFrom, int pageIndexTo)
		{
			var pdfOperations = new List<AbstractPdfOperation>();

			for (var pageIndex = pageIndexFrom; pageIndex <= pageIndexTo; pageIndex++)
			{
				var page = document.Pages[pageIndex];
				var content = PdfSharpCore.Pdf.Content.ContentReader.ReadContent(page);
				var fonts = page.ParseFonts();

				// Every page starts with the default graphics state, so no font is carried over from the previous one
				var textState = new TextState();

				ExtractText(content, pdfOperations, pageIndex, fonts, textState);
			}

			return ProcessPdfOperations(pdfOperations)
				.OrderBy(c => c.PageIndex)
				.ThenByDescending(c => c.PosY)
				.ThenBy(c => c.PosX)
				.ToList();
		}

		private static void ExtractText(CObject @object, IList<AbstractPdfOperation> pdfOperations, int pageIndex, Dictionary<string, FontResource> fonts, TextState textState)
		{
			if (@object is CArray array)
			{
				ExtractText(array, pdfOperations, pageIndex, fonts, textState);
			}
			else if (@object is CComment comment)
			{
				ExtractText(comment, pdfOperations, pageIndex);
			}
			else if (@object is CInteger integer)
			{
				ExtractText(integer, pdfOperations, pageIndex);
			}
			else if (@object is CName name)
			{
				ExtractText(name, pdfOperations, pageIndex);
			}
			else if (@object is CNumber number)
			{
				ExtractText(number, pdfOperations, pageIndex);
			}
			else if (@object is COperator @operator)
			{
				ExtractText(@operator, pdfOperations, pageIndex, fonts, textState);
			}
			else if (@object is CReal real)
			{
				ExtractText(real, pdfOperations, pageIndex);
			}
			else if (@object is CSequence sequence)
			{
				ExtractText(sequence, pdfOperations, pageIndex, fonts, textState);
			}
			else if (@object is CString @string)
			{
				ExtractText(@string, pdfOperations, pageIndex, fonts, textState);
			}
			else
			{
				throw new NotImplementedException(@object.GetType().AssemblyQualifiedName);
			}
		}

		private static void ExtractText(CArray array, IList<AbstractPdfOperation> pdfOperations, int pageIndex, Dictionary<string, FontResource> fonts, TextState textState)
		{
			foreach (var element in array)
			{
				ExtractText(element, pdfOperations, pageIndex, fonts, textState);
			}
		}

		private static void ExtractText(COperator @operator, IList<AbstractPdfOperation> pdfOperations, int pageIndex, Dictionary<string, FontResource> fonts, TextState textState)
		{
			if (@operator.OpCode.OpCodeName == OpCodeName.cm)
			{
				pdfOperations.Add(new Concatenate
				{
					PageIndex = pageIndex,
					Transformation = new Transformation
					{
						Value1 = GetPosition(@operator, 0),
						Value2 = GetPosition(@operator, 1),
						Value3 = GetPosition(@operator, 2),
						Value4 = GetPosition(@operator, 3),
						Value5 = GetPosition(@operator, 4),
						Value6 = GetPosition(@operator, 5),
					}
				});

				return;
			}

			if (@operator.OpCode.OpCodeName == OpCodeName.q)
			{
				textState.Save();
				pdfOperations.Add(new SaveGraphicsState
				{
					PageIndex = pageIndex
				});

				return;
			}

			if (@operator.OpCode.OpCodeName == OpCodeName.Q)
			{
				textState.Restore();
				pdfOperations.Add(new RestoreGraphicsState
				{
					PageIndex = pageIndex
				});

				return;
			}

			if (@operator.OpCode.OpCodeName == OpCodeName.BT)
			{
				pdfOperations.Add(new TextObject
				{
					PageIndex = pageIndex,
					Elements = new List<TextobjectElement>()
				});

				return;
			}

			if (@operator.OpCode.OpCodeName == OpCodeName.Tf)
			{
				var fontName = @operator.Operands.OfType<CName>().FirstOrDefault()?.Name;
				if (fontName.IsNullOrEmpty())
				{
					return;
				}

				textState.FontName = fontName;

				// Tf may also appear outside a text object, then there is no text object to note it in
				if (pdfOperations.LastOrDefault() is TextObject textObject)
				{
					textObject.Elements.Add(new TextObjectElementFont
					{
						Value = fontName
					});
				}

				return;
			}

			if (@operator.OpCode.OpCodeName == OpCodeName.Tm)
			{
				var textObject = pdfOperations[pdfOperations.Count - 1] as TextObject;
				textObject.Elements.Add(new TextObjectElementAbsolute
				{
					Value = "",
					Transformation = new Transformation
					{
						Value1 = GetPosition(@operator, 0),
						Value2 = GetPosition(@operator, 1),
						Value3 = GetPosition(@operator, 2),
						Value4 = GetPosition(@operator, 3),
						Value5 = GetPosition(@operator, 4),
						Value6 = GetPosition(@operator, 5),
					}
				});

				return;
			}

			if (@operator.OpCode.OpCodeName == OpCodeName.Td || @operator.OpCode.OpCodeName == OpCodeName.TD)
			{
				var textObject = pdfOperations[pdfOperations.Count - 1] as TextObject;
				textObject.Elements.Add(new TextObjectElementRelative
				{
					Value = "",
					ShiftPosX = GetPosition(@operator, 0),
					ShiftPosY = GetPosition(@operator, 1)
				});

				return;
			}

			if (@operator.OpCode.OpCodeName == OpCodeName.Tj || @operator.OpCode.OpCodeName == OpCodeName.TJ)
			{
				foreach (var element in @operator.Operands)
				{
					ExtractText(element, pdfOperations, pageIndex, fonts, textState);
				}
			}
		}

		private static void ExtractText(CSequence sequence, IList<AbstractPdfOperation> pdfOperations, int pageIndex, Dictionary<string, FontResource> fonts, TextState textState)
		{
			foreach (var element in sequence)
			{
				ExtractText(element, pdfOperations, pageIndex, fonts, textState);
			}
		}

		private static void ExtractText(CString @string, IList<AbstractPdfOperation> pdfOperations, int pageIndex, Dictionary<string, FontResource> fonts, TextState textState)
		{
			var textObject = pdfOperations[pdfOperations.Count - 1] as TextObject;
			var textObjectElement = textObject.Elements[textObject.Elements.Count - 1];

			// The font currently set, which may have been set in an earlier text object
			var fontName = textState.FontName;

			var text = @string.Value;
			if (!fontName.IsNullOrEmpty() && fonts.ContainsKey(fontName))
			{
				var font = fonts[fontName];
				text = font.Encode(text);
			}

			if (textObjectElement.Value == null)
			{
				textObjectElement.Value = text;
			}
			else
			{
				textObjectElement.Value += text;
			}
		}

		private static void ExtractText(CComment comment, IList<AbstractPdfOperation> pdfOperations, int pageIndex) { /* nothing */ }
		private static void ExtractText(CInteger integer, IList<AbstractPdfOperation> pdfOperations, int pageIndex) { /* nothing */ }
		private static void ExtractText(CName name, IList<AbstractPdfOperation> pdfOperations, int pageIndex) { /* nothing */ }
		private static void ExtractText(CNumber number, IList<AbstractPdfOperation> pdfOperations, int pageIndex) { /* nothing */ }
		private static void ExtractText(CReal real, IList<AbstractPdfOperation> pdfOperations, int pageIndex) { /* nothing */ }

		private static IEnumerable<TextContent> ProcessPdfOperations(List<AbstractPdfOperation> pdfOperations)
		{
			var pdfContent = new List<TextContent>();

			for (var operationIndex = 0; operationIndex < pdfOperations.Count; operationIndex++)
			{
				var textObject = pdfOperations[operationIndex] as TextObject;
				if (textObject == null || textObject.IsEmpty)
				{
					continue;
				}

				for (var elementIndex = 0; elementIndex < textObject.Elements.Count; elementIndex++)
				{
					if (textObject.Elements[elementIndex] is TextObjectElementFont)
					{
						continue;
					}

					if (textObject.Elements[elementIndex] is TextObjectElementAbsolute absoluteElement)
					{
						pdfContent.Add(new TextContent
						{
							PageIndex = textObject.PageIndex,
							Value = absoluteElement.Value,
							PosX = absoluteElement.Transformation.Value5,
							PosY = absoluteElement.Transformation.Value6,
						});

						continue;
					}

					if (textObject.Elements[elementIndex] is TextObjectElementRelative relativeElement)
					{
						var absoluteTextBlockPosition = GetAbsoluteTextBlockPositon(textObject.Elements, elementIndex);
						var absolutePosition = GetAbsolutePositon(absoluteTextBlockPosition, pdfOperations, operationIndex);

						pdfContent.Add(new TextContent
						{
							PageIndex = textObject.PageIndex,
							Value = relativeElement.Value,
							PosX = absolutePosition.X,
							PosY = absolutePosition.Y,
						});

						continue;
					}
				}
			}

			return pdfContent;
		}

		private static Point GetAbsolutePositon(Point currentPosition, List<AbstractPdfOperation> operations, int currentIndex)
		{
			var posX = currentPosition.X;
			var posY = currentPosition.Y;

			var ignoreUntilNextSave = false;
			for (var index = currentIndex - 1; index >= 0; index--)
			{
				if (operations[index].OperationCode == OpCodeName.q)
				{
					ignoreUntilNextSave = false;

					continue;
				}

				if (operations[index].OperationCode == OpCodeName.Q)
				{
					ignoreUntilNextSave = true;

					continue;
				}

				if (ignoreUntilNextSave)
				{
					continue;
				}

				if (operations[index].OperationCode == OpCodeName.cm)
				{
					var concatenate = operations[index] as Concatenate;
					var current = new Transformation
					{
						Value1 = concatenate.Transformation.Value1,
						Value2 = concatenate.Transformation.Value2,
						Value3 = concatenate.Transformation.Value3,
						Value4 = concatenate.Transformation.Value4,
						Value5 = posX,
						Value6 = posY
					};

					var result = current.Multiply(concatenate.Transformation);
					posX = result.Value5;
					posY = result.Value6;
				}
			}

			return new Point(posX, posY);
		}

		private static Point GetAbsoluteTextBlockPositon(List<TextobjectElement> elements, int currentIndex)
		{
			var posX = 0.0;
			var posY = 0.0;

			for (var index = 0; index <= currentIndex; index++)
			{
				if (elements[index] is TextObjectElementRelative relativeElement)
				{
					posX += relativeElement.ShiftPosX;
					posY += relativeElement.ShiftPosY;
				}
			}

			return new Point(posX, posY);
		}

		private static double GetPosition(COperator @operator, int index)
		{
			return @operator.Operands[index] is CInteger
				? ((CInteger)@operator.Operands[index]).Value
				: ((CReal)@operator.Operands[index]).Value
				;
		}
	}
}