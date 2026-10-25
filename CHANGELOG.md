# Changelog

Notable changes per released version, newest first. The two packages are versioned separately, so
every entry names the package it belongs to. Versions before NetCore 1.3.14 are not documented
here — the Git history is the source for those.

## Eshava.Report.Pdf.NetCore 1.3.15

### Fixed

* **`ExtractText` dropped every string shown after a font switch inside a text object.** A string
  was appended to the text object's last element, and a `Tf` adds an element of its own — the one
  that is skipped when the text is put together. So in `(Tino Kr)Tj /F1 10 Tf (…)Tj /F0 10 Tf
  (ger)Tj` everything after the first switch was lost, and the result read `Tino Kr`. Some PDF
  writers switch to a small font of their own for every umlaut, which cut the text at each one. A
  font switch does not move the text position, so a string now continues the last positioned
  element; without any position it starts at the origin of the text object. The tests of 1.3.14
  did not show it because a `Td` followed every `Tf` there.

* **A `bfrange` with an array of destinations was not read.** The `/ToUnicode` map may give one
  destination per code — `<0000> <0003> [<fffd> <00fc> <00f6> <00e4>]` — instead of a start value
  that is incremented. Only the second form was parsed, so every code of such a range stayed
  unmapped and was left out of the text. Both forms are read now, and the ranges are taken from all
  matches of the block rather than by cutting the string after each one, which went wrong whenever
  a match did not start at the beginning.

* **A simple font with `/Encoding /StandardEncoding` and no `/ToUnicode` map was read as Latin-1.**
  The bytes went into the text unchanged, which is right for most of the range but not for the codes
  where Adobe's standard encoding differs: `ß` (0xFB) came out as `û`, `·` (0xB4) as `´`, the
  typographic quotes, dashes and ligatures as unrelated Latin-1 characters. Such a font is now read
  through the standard encoding (PDF 32000-1:2008, Annex D.2). The other named encodings —
  `WinAnsiEncoding`, `MacRomanEncoding` — are still read as Latin-1.

## Eshava.Report.Pdf.NetCore 1.3.14

### Fixed

* **`ExtractText` decoded a string with the wrong font as soon as the font changed inside a text
  object.** The font was looked up among the text object's own `Tf` operators, and the lookup
  ended at the *first* of them instead of the last one before the string. PdfSharpCore switches
  between the regular and the bold face inside one text object, so every bold string that followed
  a regular one went through the regular face's `/ToUnicode` map. Where the bold glyph ids happened
  to exist there, the text came out as the wrong characters; where they did not, `CMap.Encode`
  ran past the end of the string (next entry) and the whole extraction failed with an
  `IndexOutOfRangeException`.

* **A text object without a `Tf` of its own came out as raw glyph ids.** The font is part of the
  graphics state, not of the text object: it outlives `ET`, and PdfSharpCore writes text objects
  that rely on the font set before them. The extraction found no font there and returned the
  undecoded bytes. It now keeps the font as text state for the page — set by `Tf` wherever it
  appears, saved and restored by `q` and `Q`, reset at every page.

* **A `Tf` outside a text object threw a `NullReferenceException`.** The operator is allowed
  there; it now sets the text state like any other.

* **`CMap.Encode` read past the end of the string.** When a code had no one-byte mapping, the
  two-, three- and four-byte attempts indexed the following characters without checking that the
  string still held them, so an unmapped code at the end of a string threw. Longer codes are now
  only tried while enough bytes are left. A code without a mapping is left out of the text, which
  is what the previous code did too — its fallback appended to a discarded result — and now says so.

* **Three- and four-byte codes never matched.** Both branches compared the mapping against a
  source length of two, and the four-byte code was assembled with `<< 32`, which shifts an `int` by
  nothing. Codes are now assembled byte by byte and matched against their own length.

* **Code space ranges were never ordered.** The `OrderBy` result was discarded; the list is now
  replaced by the ordered one, so the lookup tries ranges of fewer bytes first as intended.

* **A `/ToUnicode` map holding only `bfchar` blocks was parsed repeatedly.** An assignment in place
  of an addition (`beginbfcharIdx = 11 + …`) cut the remaining map at the wrong offset, so the
  same block was read again until the offset happened to pass it. The mappings came out the same,
  the work did not.

### Changed

* **`Eshava.Test.Report.Pdf.NetCore` runs under `dotnet test`.** It had no test adapter and no
  `Microsoft.NET.Test.Sdk`, so none of its tests were ever executed. The fixes above are covered by
  tests that build their PDFs from font dictionaries carrying nothing but a `/ToUnicode` map, so
  they depend on no installed font.
