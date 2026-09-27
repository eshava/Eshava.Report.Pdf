using System.Collections.Generic;

namespace Eshava.Report.Pdf.Models.Internal
{
	/// <summary>
	/// The font of the text state while a content stream is read.
	/// It belongs to the graphics state, not to a text object: it outlives <see cref="PdfSharpCore.Pdf.Content.Objects.OpCodeName.ET"/>,
	/// may be set outside a text object and is saved and restored by
	/// <see cref="PdfSharpCore.Pdf.Content.Objects.OpCodeName.q"/> and <see cref="PdfSharpCore.Pdf.Content.Objects.OpCodeName.Q"/>.
	/// </summary>
	internal class TextState
	{
		private readonly Stack<string> _savedFontNames = new Stack<string>();

		public string FontName { get; set; }

		public void Save()
		{
			_savedFontNames.Push(FontName);
		}

		public void Restore()
		{
			if (_savedFontNames.Count > 0)
			{
				FontName = _savedFontNames.Pop();
			}
		}
	}
}