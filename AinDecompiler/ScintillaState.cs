using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using ScintillaNET;

namespace AinDecompiler
{
    public class ScintillaState
    {
        public int SelectionStart;
        public int SelectionEnd;
        public int CursorPosition;
        public int ScrollXPosition;
        public int ScrollPosition;
        public bool Modified;
        //public Document document;

        public int PositionToLineNumber(Scintilla textBox, int position, out int positionWithinLine)
        {
            positionWithinLine = textBox.GetColumn(position);
            using (var range = textBox.GetRange(position))
            {
                return range.StartingLine.Number;
            }
        }

        public void ReadFromScintilla(Scintilla textBox)
        {
            this.ScrollPosition = textBox.PositionFromPoint(0, 0);
            this.ScrollXPosition = textBox.Scrolling.XOffset;
            this.CursorPosition = textBox.CurrentPos;
            this.SelectionStart = textBox.Selection.Start;
            this.SelectionEnd = textBox.Selection.End;
            this.Modified = textBox.Modified;
            //this.document = textBox.DocumentHandler.Current;
        }

        public void WriteToScintilla(Scintilla textBox)
        {
            textBox.CurrentPos = textBox.GetRange().End;
            textBox.Scrolling.ScrollToCaret();
            textBox.CurrentPos = ScrollPosition;
            textBox.Scrolling.ScrollToCaret();
            textBox.Scrolling.XOffset = this.ScrollXPosition;
            textBox.CurrentPos = this.CursorPosition;
            textBox.Selection.Start = this.SelectionStart;
            textBox.Selection.End = this.SelectionEnd;
            if (textBox.Modified != this.Modified)
            {
                textBox.Modified = this.Modified;
            }
            //textBox.DocumentHandler.Current = this.document;
        }

    }

}
