using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using Eshava.Report.Pdf.Core;
using Eshava.Report.Pdf.Core.Extensions;
using Eshava.Report.Pdf.Core.Interfaces;
using Eshava.Report.Pdf.Core.Models;
using Eshava.Report.Pdf.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf.IO;

namespace Eshava.Report.Pdf
{
	public class PdfPrinter : AbstractPdfPrinter<PdfDocument, PdfPage>
	{
		private readonly MemoryCache _itemCache;

		/// <summary>
		/// Images that have already been converted for PdfSharp, per document.
		/// A Graphics instance exists per page, so the cache has to live here to be shared
		/// by all pages of one document.
		/// </summary>
		private readonly ConcurrentDictionary<string, Dictionary<string, XImage>> _loadedImages;

		public PdfPrinter()
		{
			_itemCache = MemoryCache.Default;
			_loadedImages = new ConcurrentDictionary<string, Dictionary<string, XImage>>();
		}

		public PdfSharpCore.Pdf.PdfDocument CreatePDF(string xml, CacheItem<SixLabors.ImageSharp.Image> cacheItem = null)
		{
			if (cacheItem == null)
			{
				cacheItem = new CacheItem<SixLabors.ImageSharp.Image>();
			}

			var cacheItemPolicy = new CacheItemPolicy();
			var internalDocumentId = Guid.NewGuid().ToString();
			while (_itemCache.Contains(internalDocumentId))
			{
				internalDocumentId = Guid.NewGuid().ToString();
			}

			_itemCache.Set(internalDocumentId, cacheItem, cacheItemPolicy);
			_loadedImages.TryAdd(internalDocumentId, new Dictionary<string, XImage>());

			try
			{
				var pdfDocument = base.CreatePDF(internalDocumentId, xml);

				return pdfDocument?.Pdf;
			}
			finally
			{
				_itemCache.Remove(internalDocumentId);

				// the XImage instances are not disposed here, because the returned document is
				// saved by the caller afterwards; they are released together with the document
				_loadedImages.TryRemove(internalDocumentId, out _);
			}
		}


		protected override IGraphics GetGraphicsFromPdfPage(PdfPage pdfPage)
		{
			var xGraphics = PdfSharpCore.Drawing.XGraphics.FromPdfPage(pdfPage.Page);
			var cacheItem = _itemCache.Get(pdfPage.InternalDocumentId) as CacheItem<SixLabors.ImageSharp.Image>;
			var loadedImages = _loadedImages.GetOrAdd(pdfPage.InternalDocumentId, _ => new Dictionary<string, XImage>());

			return new Graphics(xGraphics, cacheItem.Images, loadedImages);
		}

		protected override PdfDocument GetPdfDocumentInstance(string internalDocumentId)
		{
			return new PdfDocument(internalDocumentId);
		}

		protected override byte[] GetStationary(string internalDocumentId, string stationaryName)
		{
			var cacheItem = _itemCache.Get(internalDocumentId) as CacheItem<SixLabors.ImageSharp.Image>;

			return cacheItem.Stationaries.CheckDictionary(stationaryName);
		}

		protected override void PrependPdfs(PdfDocument document)
		{
			var cacheItem = _itemCache.Get(document.InternalId) as CacheItem<SixLabors.ImageSharp.Image>;

			foreach (var pdfDocument in cacheItem.PrependPdfs.OrderBy(t => t.SequenceNumber))
			{
				AddPdfToDocument(document, pdfDocument.Pdf);
			}
		}
		protected override void AppendPdfs(PdfDocument document)
		{
			var cacheItem = _itemCache.Get(document.InternalId) as CacheItem<SixLabors.ImageSharp.Image>;

			foreach (var pdfDocument in cacheItem.AppendPdfs.OrderBy(t => t.SequenceNumber))
			{
				AddPdfToDocument(document, pdfDocument.Pdf);
			}
		}

		private void AddPdfToDocument(PdfDocument document, byte[] pdf)
		{
			var pdfStream = new MemoryStream(pdf)
			{
				Position = 0
			};

			var appendPdf = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
			foreach (var page in appendPdf.Pages)
			{
				document.AddPage(page);
			}
		}
	}
}