using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Eshava.Report.Pdf.Core.Enums;
using Eshava.Report.Pdf.Core.Extensions;
using Eshava.Report.Pdf.Core.Interfaces;

namespace Eshava.Report.Pdf.Core.Models
{
	public class ElementHtml : ElementBase
	{
		private Font _font;
		private IEnumerable<TextSegment> _textSegments;

		[XmlAttribute]
		public string Fontfamily { get; set; }

		[XmlAttribute]
		public double Size { get; set; }

		[XmlAttribute]
		public string Color { get; set; }

		[XmlAttribute]
		public string BackgroundColor { get; set; }

		[XmlAttribute]
		public int BackgroundSpacing { get; set; }

		[XmlAttribute]
		public bool Bold { get; set; }

		[XmlAttribute]
		public bool Italic { get; set; }

		[XmlAttribute]
		public bool Underline { get; set; }

		[XmlAttribute]
		public bool Strikeout { get; set; }

		[XmlAttribute]
		public Alignment Alignment { get; set; }

		[XmlAttribute]
		public bool ExpandAndShift { get; set; }

		[XmlAttribute]
		public double ShiftUpHeight { get; set; }

		[XmlIgnore]
		public double HeightDifference { get; private set; }

		[XmlAttribute]
		public bool NoShift { get; set; }

		[XmlAttribute]
		public bool ReduceLineBreaksForLists { get; set; }

		[XmlIgnore]
		public IEnumerable<TextSegment> TextSegments
		{
			get
			{
				if (_textSegments == default)
				{
					var interpreter = new HtmlInterpreter();
					_textSegments = interpreter.AnalyzeText(GetFont(), Content, ReduceLineBreaksForLists);
				}

				return _textSegments;
			}
			set
			{
				_textSegments = SummerizeTextSegments(value);
			}
		}

		public void ConvertContentToHtml()
		{
			var content = EncodeCharacters(Content);
			var interpreter = new HtmlInterpreter();

			Content = interpreter.ConvertToHtml(content);
		}

		public Size GetTextSize(IGraphics graphics, IEnumerable<TextSegment> textSegments)
		{
			return graphics.GetTextSize(SummerizeTextSegments(textSegments), Width, Alignment);
		}

		/// <summary>
		/// Calculates the text size considering the field size
		/// </summary>
		/// <param name="graphics">Graphics element</param>
		/// <returns>Field size</returns>
		public override Size GetSize(IGraphics graphics)
		{
			var textSize = GetSizeWithAdjustments(graphics);

			return textSize.Adjusted;
		}

		public override void Draw(IGraphics graphics, Point topLeftPage, Size sizePage)
		{
			var textSize = GetSizeWithAdjustments(graphics);

			if (!BackgroundColor.IsNullOrEmpty())
			{
				var spaceWidth = (BackgroundSpacing * textSize.SpaceWidth) / 2;
				Point topLeft;
				Point bottomRight;
				switch (Alignment)
				{
					case Alignment.Center:
						var remainingSpace = (Width - textSize.Real.Width) / 2;
						topLeft = new Point(topLeftPage.X + PosX + remainingSpace - spaceWidth, topLeftPage.Y + PosY);
						bottomRight = new Point(textSize.Real.Width + (2 * spaceWidth), textSize.Real.Height);
						break;
					case Alignment.Right:
						topLeft = new Point(topLeftPage.X + PosX + (Width - textSize.Real.Width) - spaceWidth, topLeftPage.Y + PosY);
						bottomRight = new Point(textSize.Real.Width + (2 * spaceWidth), textSize.Real.Height);

						break;
					default:
						topLeft = new Point(topLeftPage.X + PosX - spaceWidth, topLeftPage.Y + PosY);
						bottomRight = new Point(textSize.Real.Width + (2 * spaceWidth), textSize.Real.Height);

						break;
				}

				graphics.DrawRectangle(BackgroundColor, 1, DashStyle.Solid, topLeft, bottomRight, true);
			}

			graphics.DrawText(TextSegments, Alignment, topLeftPage, sizePage, new Point(PosX, PosY), textSize.Adjusted);
		}

		public List<TextSegment> SplitBySpaces()
		{
			var textSegments = new List<TextSegment>();
			var text = new StringBuilder();

			if (!TextSegments.Any() || TextSegments.All(ts => ts.Text.IsNullOrEmpty()))
			{
				return textSegments;
			}

			foreach (var textSegment in TextSegments)
			{
				if (textSegment.Text == Environment.NewLine)
				{
					textSegments.Add(textSegment.Clone());

					continue;
				}

				var words = textSegment.Text.Trim().Split(' ');
				for (var index = 0; index < words.Length; index++)
				{
					var word = words[index];
					if (index > 0 || textSegment.Text.StartsWith(" "))
					{
						word = " " + word;
					}

					var textSegmentWord = textSegment.Clone();
					textSegmentWord.Text = word;

					textSegments.Add(textSegmentWord);
				}
			}

			return textSegments;
		}

		public static T Clone<T>(T element) where T : ElementHtml, new()
		{
			var clone = new T();
			foreach (var propertyInfo in element.GetType().GetProperties())
			{
				if (propertyInfo.CanWrite)
				{
					propertyInfo.SetValue(clone, propertyInfo.GetValue(element, null));
				}
			}
			return clone;
		}

		private Font GetFont()
		{
			if (_font == null)
			{
				_font = new Font
				{
					Fontfamily = Fontfamily,
					Size = Size,
					Bold = Bold,
					Italic = Italic,
					Underline = Underline,
					Strikeout = Strikeout,
					Color = Color.ConvertHexColorToDecimalColor()
				};
			}

			return _font;
		}

		private IEnumerable<TextSegment> SummerizeTextSegments(IEnumerable<TextSegment> textSegments)
		{
			var summerizedTextSegments = new List<TextSegment>();

			var lastTextSegment = default(TextSegment);
			foreach (var textSegment in textSegments)
			{
				if (lastTextSegment != default
					&& (textSegment.Text != Environment.NewLine && lastTextSegment.Text != Environment.NewLine)
					&& lastTextSegment.Font.GetHashCode() == textSegment.Font.GetHashCode()
					&& lastTextSegment.LineIndent == textSegment.LineIndent)
				{
					lastTextSegment.Text += textSegment.Text;
				}
				else
				{
					lastTextSegment = default(TextSegment);
				}

				if (lastTextSegment == default)
				{
					lastTextSegment = textSegment.Clone();
					summerizedTextSegments.Add(lastTextSegment);
				}
			}

			return summerizedTextSegments;
		}

		private (Size Real, Size Adjusted, double SpaceWidth) GetSizeWithAdjustments(IGraphics graphics)
		{
			if (!TextSegments.Any() || TextSegments.All(ts => ts.Text.IsNullOrEmpty()))
			{
				return (new Size(0, 0), new Size(0, 0), 0);
			}

			// calculate line indent reduction
			var lineIndentReductions = TextSegments.Where(ts => ts.ReduceLineIndent).ToList();
			foreach (var textSegment in lineIndentReductions)
			{
				var textWidth = graphics.GetTextWidth(GetFont(), textSegment.ReduceLineIndentByText);
				if (textSegment.LineIndent > textWidth)
				{
					textSegment.LineIndent -= textWidth;
				}

				textSegment.ReduceLineIndent = false;
				textSegment.ReduceLineIndentByText = null;
			}

			var textSize = graphics.GetTextSize(TextSegments, Width, Alignment);
			var referenceSize = graphics.GetTextSize(TextSegments.OrderByDescending(ts => ts.Font.Size).First().Font, Width, "a", Alignment.Left);
			var referenceSize2 = graphics.GetTextSize(TextSegments.OrderByDescending(ts => ts.Font.Size).First().Font, Width, "a a", Alignment.Left);

			double height;
			HeightDifference = 0;

			// Height == 0: Can have an infinite height
			if (Math.Abs(textSize.Height) < 0.001)
			{
				height = 0.0;
			}
			else if (ExpandAndShift && textSize.Height > Height)
			{

				height = textSize.Height;
				HeightDifference = height - referenceSize.Height;
			}
			else if (Math.Abs(Height) < 0.001 || (Height > textSize.Height && !ExpandAndShift))
			{
				height = textSize.Height;
			}
			else
			{
				height = Height;
			}

			return (new Size(textSize.Width, textSize.Height), new Size(Width, height), referenceSize2.Width - (2 * referenceSize.Width));
		}

		private string EncodeCharacters(string text)
		{
			text = EncodeCharacters(text, "&", "&amp;");
			text = EncodeCharacters(text, "<", "&lt;");
			text = EncodeCharacters(text, ">", "&gt;");

			return text;
		}

		private string EncodeCharacters(string text, string characterToReplace, string replacement)
		{
			var startIndex = 0;
			while (true)
			{
				var indexOf = text.IndexOf(characterToReplace, startIndex);
				if (indexOf < 0)
				{
					break;
				}

				startIndex = indexOf + 1;

				var indexOfEnd = indexOf + 4;
				if (indexOfEnd < text.Length)
				{
					if (text[indexOfEnd] != ';')
					{
						var before = text.Substring(0, indexOf);
						var after = text.Substring(indexOf + 1);

						text = before + replacement + after;
					}
				}
				else
				{
					var before = text.Substring(0, indexOf);
					var after = text.Substring(indexOf + 1);

					text = before + replacement + after;
				}
			}

			return text;
		}
	}
}