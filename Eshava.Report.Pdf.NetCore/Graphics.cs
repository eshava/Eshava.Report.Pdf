using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eshava.Report.Pdf.Core.Enums;
using Eshava.Report.Pdf.Core.Extensions;
using Eshava.Report.Pdf.Core.Interfaces;
using Eshava.Report.Pdf.Core.Models;
using Eshava.Report.Pdf.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp.Formats.Png;

namespace Eshava.Report.Pdf
{
	public class Graphics : IGraphics
	{
		private XGraphics _xGraphics;
		private readonly Dictionary<string, SixLabors.ImageSharp.Image> _pictures;
		private readonly Dictionary<string, XImage> _loadedImages;
		private bool _isDisposed = false;

		public Graphics(XGraphics xGraphics, Dictionary<string, SixLabors.ImageSharp.Image> pictures)
			: this(xGraphics, pictures, new Dictionary<string, XImage>())
		{
		}

		/// <summary>
		/// The dictionary of already loaded images is meant to be shared by all pages of one
		/// document. Every image element asks for its image several times (size calculation,
		/// position calculation, drawing), and each of those calls used to re-encode the image
		/// and to add another copy of it to the pdf.
		/// </summary>
		public Graphics(XGraphics xGraphics, Dictionary<string, SixLabors.ImageSharp.Image> pictures, Dictionary<string, XImage> loadedImages)
		{
			_xGraphics = xGraphics;
			_pictures = pictures;
			_loadedImages = loadedImages;
		}

		public void Dispose()
		{
			if (!_isDisposed)
			{
				_xGraphics.Dispose();
				_xGraphics = null;
				_isDisposed = true;
			}
		}

		public double GetTextWidth(Font font, string text)
		{
			var options = new XPdfFontOptions(PdfFontEncoding.Unicode);
			var xFont = new XFont(font.Fontfamily, font.Size, GetXFontstyle(font.Bold, font.Italic, font.Underline, font.Strikeout), options);
			var format = XStringFormats.Default;
			format.Alignment = XStringAlignment.Near;
			format.LineAlignment = XLineAlignment.Near;

			return _xGraphics.MeasureString(text, xFont, format).Width;
		}

		public Size GetTextSize(Font font, double elementWidth, string text, Alignment alignment)
		{
			var textSegment = new Core.Models.TextSegment
			{
				Text = text,
				Font = font
			};

			return GetTextSize(new[] { textSegment }, elementWidth, alignment);
		}

		public Size GetTextSize(IEnumerable<Core.Models.TextSegment> textSegments, double width, Alignment alignment)
		{
			var pdfSharpTextSegments = ConvertTextSegemnts(textSegments);
			var format = XStringFormats.Default;
			format.Alignment = XStringAlignment.Near;
			format.LineAlignment = XLineAlignment.Near;

			var tf = new XTextSegmentFormatter(_xGraphics)
			{
				Alignment = alignment == Alignment.Justify ? XParagraphAlignment.Justify : XParagraphAlignment.Left
			};
			var size = tf.CalculateTextSize(pdfSharpTextSegments, width, format);

			return new Size(size.Width, size.Height);
		}

		public IImage LoadImage(string imageName)
		{
			if (imageName.IsNullOrEmpty())
			{
				return null;
			}

			if (_loadedImages.TryGetValue(imageName, out var loadedImage))
			{
				return loadedImage == null ? null : new Image(loadedImage);
			}

			XImage image = null;
			try
			{

				if (_pictures != null && _pictures.ContainsKey(imageName))
				{
					var localImage = _pictures[imageName];
					if (localImage != null)
					{
						var imageStream = new MemoryStream();
						localImage.Save(imageStream, new PngEncoder { CompressionLevel = PngCompressionLevel.BestSpeed, ColorType = PngColorType.RgbWithAlpha });
						imageStream.Position = 0;
						image = XImage.FromStream(() => imageStream);
					}
				}
				else if (File.Exists(imageName))
				{
					image = XImage.FromFile(imageName);
				}
			}
			catch
			{
				image = null;
			}

			// a failed image is cached as null on purpose, so that it is not retried on every access
			_loadedImages[imageName] = image;

			return image == null ? null : new Image(image);
		}

		public IGraphics DrawImage(IImage image, Point location, Size size)
		{
			if (image == null || location == null || size == null)
			{
				return this;
			}

			_xGraphics.DrawImage((image as Image).XImage, new XRect(new XPoint(location.X, location.Y), new XSize(size.Width, size.Height)));

			return this;
		}

		public IGraphics DrawLine(string color, double lineWidth, DashStyle dashStyle, Point topLeft, Point bottomRight)
		{
			var pen = new XPen(TranslateColor(color), lineWidth) { DashStyle = (XDashStyle)dashStyle };
			_xGraphics.DrawLine(pen, topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);

			return this;
		}

		public IGraphics DrawRectangle(string color, double lineWidth, DashStyle dashStyle, Point topLeft, Point bottomRight, bool fill)
		{
			if (fill)
			{
				XBrush brush = new XSolidBrush(TranslateColor(color));
				_xGraphics.DrawRectangle(brush, topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
			}
			else
			{
				var pen = new XPen(TranslateColor(color), lineWidth) { DashStyle = (XDashStyle)dashStyle };
				_xGraphics.DrawRectangle(pen, topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
			}

			return this;
		}

		public IGraphics DrawText(Font font, string text, Alignment alignment, Point topLeftPage, Size sizePage, Point topLeftText, Size textSize)
		{
			if (text.IsNullOrEmpty())
			{
				return this;
			}

			var textSegment = new Core.Models.TextSegment
			{
				Text = text,
				Font = font
			};

			return DrawText(new List<Core.Models.TextSegment> { textSegment }, alignment, topLeftPage, sizePage, topLeftText, textSize);
		}

		public IGraphics DrawText(IEnumerable<Core.Models.TextSegment> textSegments, Alignment alignment, Point topLeftPage, Size sizePage, Point topLeftText, Size textSize)
		{
			if (textSegments.All(ts => ts.Text.IsNullOrEmpty()))
			{
				return this;
			}

			var pdfSharpTextSegments = ConvertTextSegemnts(textSegments);
			var format = XStringFormats.Default;
			var xSize = new XSize(textSize.Width, textSize.Height);
			var topLeftTotal = new XPoint(topLeftPage.X + topLeftText.X, topLeftPage.Y + topLeftText.Y);
			var tf = new XTextSegmentFormatter(_xGraphics);

			if (topLeftText.X < sizePage.Width && topLeftText.Y < sizePage.Height)
			{
				if (topLeftText.X + xSize.Width > sizePage.Width)
				{
					xSize = new XSize(sizePage.Width - topLeftText.X, xSize.Height);
				}

				if (topLeftText.Y + xSize.Height > sizePage.Height)
				{
					xSize = new XSize(xSize.Width, sizePage.Height - topLeftText.Y);
				}

				format.Alignment = XStringAlignment.Near;
				format.LineAlignment = XLineAlignment.Near;
				tf.Alignment = (XParagraphAlignment)alignment;
				tf.DrawString(pdfSharpTextSegments, new XRect(topLeftTotal, xSize), format);
			}

			return this;
		}

		private IEnumerable<PdfSharpCore.Drawing.Layout.TextSegment> ConvertTextSegemnts(IEnumerable<Core.Models.TextSegment> textSegments)
		{
			var options = new XPdfFontOptions(PdfFontEncoding.Unicode);

			return textSegments
				.Select(ts => new PdfSharpCore.Drawing.Layout.TextSegment
				{
					Text = ts.Text,
					Font = new XFont(ts.Font.Fontfamily, ts.Font.Size, GetXFontstyle(ts.Font.Bold, ts.Font.Italic, ts.Font.Underline, ts.Font.Strikeout), options),
					Brush = new XSolidBrush(TranslateColor(ts.Font.Color)),
					LineIndent = ts.LineIndent,
					SkipParagraphAlignment = ts.SkipParagraphAlignment
				})
				.ToList();
		}

		public IGraphics SetStationery(byte[] stationery)
		{
			if (stationery == null)
			{
				return this;
			}

			var pdf = XPdfForm.FromStream(new MemoryStream(stationery), PdfSharpCore.Pdf.IO.enums.PdfReadAccuracy.Moderate);
			var point = new XPoint(0, 0);

			_xGraphics.DrawImage(pdf, point);

			return this;
		}

		public XFontStyle GetXFontstyle(bool bold, bool italic, bool underline, bool strikeout)
		{
			var style = XFontStyle.Regular;
			if (bold)
			{
				style |= XFontStyle.Bold;
			}
			if (italic)
			{
				style |= XFontStyle.Italic;
			}
			if (underline)
			{
				style |= XFontStyle.Underline;
			}
			if (strikeout)
			{
				style |= XFontStyle.Strikeout;
			}

			return style;
		}

		private XColor TranslateColor(string color)
		{
			if (color.IsNullOrEmpty())
			{
				return XColor.FromArgb(0, 0, 0);
			}

			try
			{
				var parts = color.Split(' ');

				return XColor.FromArgb(Convert.ToInt32(parts[0]), Convert.ToInt32(parts[1]), Convert.ToInt32(parts[2]), Convert.ToInt32(parts[3]));
			}
			catch
			{
				return XColor.FromArgb(0, 0, 0);
			}
		}
	}
}