using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Eshava.Report.Pdf.Core.Extensions;
using Eshava.Report.Pdf.Core.Models;

namespace Eshava.Report.Pdf.Core
{
	public class HtmlInterpreter
	{
		private const double CENTIMETERTOPOINTFACTOR = 28.3465;
		private const string P_TAG = "p";
		private const string UL_TAG = "ul";
		private const string OL_TAG = "ol";
		private const string LI_TAG = "li";
		private const string BR_TAG = "br";

		public string ConvertToHtml(string text)
		{
			var htmlContent = new StringBuilder();
			var contentParts = text.Replace("\r", "").Split('\n');

			var hasOpenListing = false;
			for (var index = 0; index < contentParts.Length; index++)
			{
				if (contentParts[index].StartsWith("-"))
				{
					if (!hasOpenListing)
					{
						hasOpenListing = true;
						htmlContent.Append("<ul>");
					}

					htmlContent.Append("<li>");
					htmlContent.Append(contentParts[index].Substring(1).Trim());
					htmlContent.Append("</li>");
				}
				else if (hasOpenListing)
				{
					hasOpenListing = false;
					htmlContent.Append("</ul>");
					if (contentParts[index].IsNullOrEmpty())
					{
						if (index < contentParts.Length - 1 && !contentParts[index + 1].StartsWith("- "))
						{
							htmlContent.Append("<br>");
						}
					}
					else
					{
						htmlContent.Append("<p>");
						htmlContent.Append(contentParts[index]);
						htmlContent.Append("</p>");
					}
				}
				else if (contentParts[index].IsNullOrEmpty())
				{
					htmlContent.Append("<br>");
				}
				else
				{
					htmlContent.Append("<p>");
					htmlContent.Append(contentParts[index]);
					htmlContent.Append("</p>");
				}
			}

			if (hasOpenListing)
			{
				htmlContent.Append("</ul>");
			}

			return htmlContent.ToString();
		}

		public IEnumerable<TextSegment> AnalyzeText(Font font, string text, bool reduceLineBreaksForLists)
		{
			font.Color = font.Color?.Trim().ConvertHexColorToDecimalColor();
			font.Color = font.Color.ConvertRGBFunctionToDecimalColor();

			var textSegments = new List<TextSegment>();

			if (text.IsNullOrEmpty())
			{
				textSegments.Add(new TextSegment
				{
					Font = font,
					Text = ""
				});

				return textSegments;
			}
			try
			{
				// remove hr tags
				text = Regex.Replace(text, @"<hr.*?>{1}", " ");

				var html = $"<div>{text.Replace("<br>", "<br/>")}</div>";
				var xmlDocument = new XmlDocument();
				// When loading the html as xml, spaces between tags are sometimes removed
				xmlDocument.LoadXml(html.Replace("> <", "><span>&amp;nbsp;</span><"));

				var segment = new TextSegmentExtended
				{
					Font = font
				};

				AnalyzeNode(xmlDocument.FirstChild, segment, reduceLineBreaksForLists);

				FlatSegmentTree(segment, textSegments);

				if (textSegments.Last().Text == Environment.NewLine)
				{
					textSegments.RemoveAt(textSegments.Count - 1);
				}

				// Hack: Separate the content of an list entry from the leading "bullet points"
				for (var index = 0; index < textSegments.Count - 1; index++)
				{
					if (textSegments[index].ReduceLineIndent)
					{
						textSegments[index + 1].Text = " " + textSegments[index + 1].Text;
					}
				}

				// Remove empty text segments
				textSegments = textSegments.Where(ts => ts.Text != "").ToList();

				if (textSegments.Count > 0 && textSegments[0].Text == Environment.NewLine)
				{
					textSegments.RemoveAt(0);
				}

				return textSegments;
			}
			catch
			{
				textSegments.Add(new TextSegment
				{
					Font = font,
					Text = text
				});

				return textSegments;
			}
		}

		private void FlatSegmentTree(TextSegmentExtended segment, IList<TextSegment> textSegments)
		{
			if (segment.Text == Environment.NewLine)
			{
				textSegments.Add(new TextSegment
				{
					Font = segment.Font,
					Text = segment.Text
				});


				return;
			}

			if (!segment.Text.IsNullOrEmpty())
			{
				var lastSegment = textSegments.LastOrDefault();
				if (lastSegment == default
					|| lastSegment.Text == Environment.NewLine
					|| lastSegment.Font.GetHashCode() != segment.Font.GetHashCode()
					|| lastSegment.LineIndent != segment.LineIndent
					|| lastSegment.ReduceLineIndentByText != segment.ReduceLineIndentByText
					)
				{
					if (lastSegment != default)
					{
						if (lastSegment.Text.EndsWith(" ") && segment.Text.StartsWith(" "))
						{
							// Reduce number of spaces between text segments to one
							lastSegment.Text = lastSegment.Text.TrimEnd();
						}
						else if (lastSegment.Text.EndsWith(" ") && !segment.Text.StartsWith(" "))
						{
							// PdfSharp behaviour: Trailing space is always removed or ignored
							// so move trailing space to the next text segment
							lastSegment.Text = lastSegment.Text.TrimEnd();
							segment.Text = " " + segment.Text;
						}
					}

					textSegments.Add(segment.Clone());
				}
				else
				{
					if (lastSegment.Text.EndsWith(" ") || segment.Text.StartsWith(" "))
					{
						lastSegment.Text += segment.Text;
					}
					else
					{
						lastSegment.Text += " " + segment.Text;
					}
				}
			}

			if (segment.Children.Count == 0)
			{
				return;
			}

			foreach (var childSegment in segment.Children)
			{
				if (segment.LineIndent > 0)
				{
					childSegment.LineIndent += segment.LineIndent;
				}

				FlatSegmentTree(childSegment, textSegments);
			}
		}

		private void AnalyzeNode(XmlNode node, TextSegmentExtended parentSegment, bool reduceLineBreaksForLists)
		{
			if (node.NodeType == XmlNodeType.Text)
			{
				var text = node.Value?
								.Replace("\r", "")
								.Replace("\n", "")
								.Replace("  ", " ")
								.Replace("&nbsp;", " ")
								?? "";

				// Ignore leading spaces after a line break
				if (node.PreviousSibling?.Name.ToLower() == BR_TAG)
				{
					text = text.TrimStart();
				}

				parentSegment.Children.Add(new TextSegmentExtended
				{
					Font = new Font
					{
						Fontfamily = parentSegment.Font.Fontfamily,
						Size = parentSegment.Font.Size,
						Bold = parentSegment.Font.Bold,
						Italic = parentSegment.Font.Italic,
						Underline = parentSegment.Font.Underline,
						Strikeout = parentSegment.Font.Strikeout,
						Color = parentSegment.Font.Color,
					},
					Text = text
				});

				return;
			}

			if (node.NodeType != XmlNodeType.Element)
			{
				return;
			}

			var xmlElement = node as XmlElement;
			var segment = new TextSegmentExtended
			{
				Font = new Font
				{
					Fontfamily = parentSegment.Font.Fontfamily,
					Size = parentSegment.Font.Size,
					Bold = parentSegment.Font.Bold,
					Italic = parentSegment.Font.Italic,
					Underline = parentSegment.Font.Underline,
					Strikeout = parentSegment.Font.Strikeout,
					Color = parentSegment.Font.Color,
				}
			};

			if (xmlElement.Name.ToLower() == BR_TAG)
			{
				segment.Text = Environment.NewLine;
				parentSegment.Children.Add(segment);

				return;
			}

			if (xmlElement.Name.ToLower() == UL_TAG || xmlElement.Name.ToLower() == OL_TAG)
			{
				if (reduceLineBreaksForLists)
				{
					if (!CheckForTagInPreviousElements(xmlElement, P_TAG, UL_TAG, OL_TAG))
					{
						// Add only a new line if the previous node is no list tag (<br> is be ignored)
						AddNewLineSegement(parentSegment, segment.Font);
					}
				}
				else
				{
					AddNewLineSegement(parentSegment, segment.Font);
				}

				// HACK
				if (xmlElement.Name.ToLower() == UL_TAG)
				{
					segment.LineIndent = 2.0;
				}
				else if (xmlElement.Name.ToLower() == OL_TAG)
				{
					segment.LineIndent = 1.5;
				}
			}
			else if (xmlElement.Name.ToLower() == P_TAG && (!reduceLineBreaksForLists || !CheckForTagInPreviousElements(xmlElement, P_TAG, UL_TAG, OL_TAG)))
			{
				AddNewLineSegement(parentSegment, segment.Font);
			}
			else if (xmlElement.Name.ToLower() == "b" || xmlElement.Name.ToLower() == "strong")
			{
				segment.Font.Bold = true;
			}
			else if (xmlElement.Name.ToLower() == "i" || xmlElement.Name.ToLower() == "em")
			{
				segment.Font.Italic = true;
			}
			else if (xmlElement.Name.ToLower() == "u")
			{
				segment.Font.Underline = true;
			}
			else if (xmlElement.Name.ToLower() == "s" || xmlElement.Name.ToLower() == "del" || xmlElement.Name.ToLower() == "strike")
			{
				segment.Font.Strikeout = true;
			}

			CheckAttributes(xmlElement, segment);

			parentSegment.Children.Add(segment);

			foreach (XmlNode item in node.ChildNodes)
			{
				AnalyzeNode(item, segment, reduceLineBreaksForLists);
			}

			var tagsForNextElementsCheck = reduceLineBreaksForLists
				? new[] { P_TAG, UL_TAG, OL_TAG }
				: new[] { P_TAG };

			if (xmlElement.Name.ToLower() == P_TAG && !CheckForTagInNextElements(xmlElement, !reduceLineBreaksForLists, tagsForNextElementsCheck))
			{
				AddNewLineSegement(parentSegment, segment.Font);
			}
			else if (xmlElement.Name.ToLower() == LI_TAG)
			{
				AddNewLineSegement(parentSegment, segment.Font);
			}

			if (xmlElement.Name.ToLower() == UL_TAG)
			{
				segment.LineIndent = 0.0;
				foreach (var childSegment in segment.Children.Where(s => s.Text != Environment.NewLine))
				{
					childSegment.Text = "-";
					childSegment.ReduceLineIndent = true;
					childSegment.ReduceLineIndentByText = $"{childSegment.Text} ";
					childSegment.SkipParagraphAlignment = true;
				}
			}
			else if (xmlElement.Name.ToLower() == OL_TAG)
			{
				segment.LineIndent = 0.0;

				var counter = 1;
				foreach (var childSegment in segment.Children.Where(s => s.Text != Environment.NewLine))
				{
					childSegment.Text = $"{counter}.";
					childSegment.ReduceLineIndent = true;
					childSegment.ReduceLineIndentByText = $"{childSegment.Text} ";
					childSegment.SkipParagraphAlignment = true;

					counter++;
				}
			}
			else if (xmlElement.Name.ToLower() == LI_TAG)
			{
				if (segment.LineIndent == 0.0 && parentSegment.LineIndent > 0.0)
				{
					segment.LineIndent = CENTIMETERTOPOINTFACTOR / parentSegment.LineIndent;
				}
			}
		}

		private void AddNewLineSegement(TextSegmentExtended segment, Font font)
		{
			segment.Children.Add(new TextSegmentExtended
			{
				Font = font,
				Text = Environment.NewLine
			});
		}

		private bool CheckForTagInPreviousElements(XmlNode xmlElement, string cancelAtTag, params string[] tags)
		{
			while (xmlElement?.PreviousSibling != null)
			{
				if (!cancelAtTag.IsNullOrEmpty() && xmlElement.PreviousSibling?.Name.ToLower() == cancelAtTag)
				{
					return false;
				}

				if (tags.Contains(xmlElement.PreviousSibling?.Name.ToLower()))
				{
					return true;
				}

				xmlElement = xmlElement.PreviousSibling;
			}

			return false;
		}

		private bool CheckForTagInNextElements(XmlNode xmlElement, bool onlyNextSibling, params string[] tags)
		{
			while (xmlElement?.NextSibling != null)
			{
				if (tags.Contains(xmlElement.NextSibling?.Name.ToLower()))
				{
					return true;
				}

				xmlElement = xmlElement.NextSibling;

				if (onlyNextSibling)
				{
					break;
				}
			}

			return false;
		}

		private void CheckAttributes(XmlElement xmlElement, TextSegment textSegment)
		{
			if (!xmlElement.HasAttributes)
			{
				return;
			}

			foreach (XmlAttribute attribute in xmlElement.Attributes)
			{
				if (attribute.Name.ToLower() == "style" && !attribute.Value.IsNullOrEmpty())
				{
					var styles = attribute.Value.Split(';');
					foreach (var style in styles)
					{
						var tuple = style.Split(':');
						if (tuple.Length != 2)
						{
							continue;
						}

						switch (tuple[0].Trim().ToLower())
						{
							case "font-family":
								textSegment.Font.Fontfamily = tuple[1].Split(',')[0].Trim();

								break;
							case "font-size":
								if (Double.TryParse(tuple[1].Trim(), out var size))
								{
									textSegment.Font.Size = size;
								}

								break;
							case "color":
								textSegment.Font.Color = tuple[1].Trim().ConvertHexColorToDecimalColor();
								textSegment.Font.Color = textSegment.Font.Color.ConvertRGBFunctionToDecimalColor();

								break;
							case "margin-left" when xmlElement.Name.ToLower() == "li":
								if (Double.TryParse(tuple[1].Trim(), out var indent))
								{
									textSegment.LineIndent = indent;
								}

								break;
						}
					}

					break;
				}
			}
		}

		private class TextSegmentExtended : TextSegment
		{
			public TextSegmentExtended()
			{
				Children = new List<TextSegmentExtended>();
			}

			public IList<TextSegmentExtended> Children { get; set; }
		}
	}
}