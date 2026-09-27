# Eshava.Report.Pdf — Repository Notes

Library that generates PDF documents from an XML description, built on PdfSharp. Published as
two NuGet packages, **`Eshava.Report.Pdf.NetCore`** and **`Eshava.Report.Pdf.NetFramework`**.

**Conventions:** documentation, code and commit messages are written in English. Line endings are
pinned through `.gitattributes` — anything that may run on Linux must be checked out with LF.

**Releases are recorded in `CHANGELOG.md`, not in the project files.** The two packages are
versioned separately, so every entry names its package and version; the version itself is set
when packing.

## Layout

| Project | Target | Content |
|---|---|---|
| `Eshava.Report.Pdf.Core` | — | Platform-independent calculation and document generation. **Not published as its own package.** |
| `Eshava.Report.Pdf.NetCore` | `net8.0;net9.0;net10.0` | PdfSharpCore, SixLabors.Fonts, SixLabors.ImageSharp. |
| `Eshava.Report.Pdf.NetFramework` | `net48` | PdfSharp 1.32.3057, `System.Drawing.Image`. |

Test projects are named `Eshava.Test.Report.Pdf.<Variant>` and use MSTest with FluentAssertions.
`Input/` in the repository root holds sample data.

**Only `Eshava.Test.Report.Pdf.NetCore` runs under `dotnet test`.** It carries the test adapter and
`Microsoft.NET.Test.Sdk`, with `GenerateProgramFile` off because its `Program.cs` keeps a `Main` for
running the document tests by hand. `Eshava.Test.Report.Pdf.Core` references the test framework
alone, so its tests compile and are never executed.

## Non-Obvious Mechanisms

**`Eshava.Report.Pdf.Core` is embedded into both packages instead of being referenced.** The
project reference carries `<PrivateAssets>all</PrivateAssets>`, and a custom
`IncludeP2PAssets` target adds `Eshava.Report.Pdf.Core.dll` to `BuildOutputInPackage`. So the
Core DLL ships inside each package. Two consequences: there is no separate Core version to keep
in sync, and touching this wiring — or the extra `IncludeP2PAssets` build configuration next to
Debug and Release — silently produces a package that is missing its Core assembly. Verify the
package contents after any change to it.

**`net48` on the NetFramework variant is deliberate, not neglect.** PdfSharp is pinned to
1.32.3057 because that version still measures strings through GDI. Later versions no longer do,
and the pull request adding multiline string measurement upstream is still open. Do not "modernise"
this dependency.

The `System.Runtime.Caching` version is conditioned per target framework — a new target needs a
matching `ItemGroup`.

**Text extraction reads the font from the text state, not from the text object.** `ExtractText`
decodes every string through the `/ToUnicode` map of the font currently set. That font belongs to
the graphics state: it outlives `ET`, may be set by a `Tf` outside a text object, and is saved and
restored by `q` and `Q` — PdfSharpCore itself switches fonts inside one text object and writes
text objects without a `Tf` of their own. Decoding with any other font yields wrong characters, or
none. A code without a mapping is left out of the text, since its value names a glyph rather than
a character.

The extraction code (`PdfDocumentExtensions`, `PdfFonts/`) was taken over from two open source
samples, named in the class comments, and is tested with PDFs built in the test itself from font
dictionaries that carry nothing but a `/ToUnicode` map — independent of any installed font.

## Dependencies

Consumes `Eshava.Core` as a NuGet package.
