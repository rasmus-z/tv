using System;
using System.Composition;
using System.Drawing;
using SharpDX;
using SharpDX.DirectWrite;
using TerminalVelocity.Preferences;

namespace TerminalVelocity.Direct2D
{
    [Shared]
    public class FontProvider
    {
        public const string TerminalTextContract = TerminalTheme.FontContract;

        [Export(TerminalTextContract)]
        public Configurable<TextFormat> TerminalText { get; }

        private Factory _factory;

        [ImportingConstructor]
        public FontProvider(
            [Import] Factory factory,
            [Import(TerminalTheme.FontContract)] Configurable<string> terminalTextFamily,
            [Import(TerminalTheme.FontContract)] Configurable<int> terminalTextSize
        )
        {
            _factory = factory;
            TerminalText = terminalTextFamily.Join(terminalTextSize, TextFormat);
        }

        private TextFormat TextFormat(string font, Size fontSize) => TextFormat(font, fontSize.Height);

        private TextFormat TextFormat(string font, int fontSize) =>
            new TextFormat(_factory, font, fontSize);
    }
}