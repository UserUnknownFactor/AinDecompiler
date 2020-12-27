using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Drawing;

namespace AinDecompiler
{
    public static class ScintillaExtensions
    {
        public static void UpdateFonts(this ScintillaNET.Scintilla scintilla)
        {
            Font font = scintilla.Font;
            for (int i = 0; i < scintilla.Styles.Max.Index; i++)
            {
                var style = scintilla.Styles[i];
                style.FontName = font.Name;
                style.Size = font.Size;
                //style.Font = font;
            }
        }
    }
}
