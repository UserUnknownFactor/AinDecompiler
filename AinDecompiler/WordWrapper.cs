using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    class WordWrapperOld
    {
        AinFile ainFile;

        public WordWrapOptionsOld wordWrapOptions;
        string _reduceMarginFunctionName = null;

        public string NextLineFunctionName { get { return wordWrapOptions.NextLineFunctionName; } set { wordWrapOptions.NextLineFunctionName = value; } }
        public string NextMessageFunctionName { get { return wordWrapOptions.NextMessageFunctionName; } set { wordWrapOptions.NextMessageFunctionName = value; } }
        public string ReduceMarginFunctionName { get { return wordWrapOptions.ReduceMarginFunctionName; } set { wordWrapOptions.ReduceMarginFunctionName = value; } }
        public string NextLineFunctionCode { get { return wordWrapOptions.NextLineFunctionCode; } set { wordWrapOptions.NextLineFunctionCode = value; } }
        public string NextMessageFunctionCode { get { return wordWrapOptions.NextMessageFunctionCode; } set { wordWrapOptions.NextMessageFunctionCode = value; } }
        public int MaxCharactersPerLineNormal { get { return wordWrapOptions.MaxCharactersPerLineNormal; } set { wordWrapOptions.MaxCharactersPerLineNormal = value; } }
        public int MaxCharactersPerLineReduced { get { return wordWrapOptions.MaxCharactersPerLineReduced; } set { wordWrapOptions.MaxCharactersPerLineReduced = value; } }
        public int MaxLinesPerMessage { get { return wordWrapOptions.MaxLinesPerMessage; } set { wordWrapOptions.MaxLinesPerMessage = value; } }
        public bool UseVariableWidthFont { get { return wordWrapOptions.UseVariableWidthFont; } set { wordWrapOptions.UseVariableWidthFont = value; } }
        public bool MaintainIndentation { get { return wordWrapOptions.MaintainIndentation; } set { wordWrapOptions.MaintainIndentation = value; } }
        public bool RemoveLineBreaksIfWordWrapping { get { return wordWrapOptions.RemoveLineBreaksIfWordWrapping; } set { wordWrapOptions.RemoveLineBreaksIfWordWrapping = value; } }
        HashSet<string> _reduceMarginFunctionNames = new HashSet<string>();

        public bool HasRemainingText
        {
            get
            {
                return remainingText != null && remainingText.Length > 0;
            }
        }

        public HashSet<string> ReduceMarginFunctionNames
        {
            get
            {
                if (_reduceMarginFunctionName != this.ReduceMarginFunctionName && this.ReduceMarginFunctionName != null)
                {
                    _reduceMarginFunctionNames = new HashSet<string>(this.ReduceMarginFunctionName.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
                }
                return _reduceMarginFunctionNames;
            }
            set
            {
                this.ReduceMarginFunctionName = value.ToArray().Join(Environment.NewLine);
            }
        }

        //public string nextLineFunctionName, nextMessageFunctionName, reduceMarginFunctionName,
        //    nextLineFunctionCode, nextMessageFunctionCode;
        //public int maxCharactersPerLineNormal, maxCharactersPerLineReduced, maxLinesPerMessage;

        public WordWrapperOld(AinFile ainFile)
            : this(ainFile, new WordWrapOptionsOld())
        {

        }

        public WordWrapperOld(AinFile ainFile, WordWrapOptionsOld wordWrapOptions)
        {
            this.wordWrapOptions = wordWrapOptions;
            characterWidthCache = new CharacterWidthCache(wordWrapOptions);
            this.ainFile = ainFile;

            //this.NextLineFunctionName = "R";
            //this.NextLineFunctionCode = " CALLFUNC R ";
            //this.ReduceMarginFunctionName = "顔";
            //this.NextMessageFunctionName = "A";
            //this.NextMessageFunctionCode = " S_PUSH \"\"" + Environment.NewLine +
            //    "\t" + " CALLFUNC A ";
            //this.MaxCharactersPerLineNormal = 56 + 1; //one more
            //this.MaxCharactersPerLineReduced = 41 + 1; //one more
            //this.MaxLinesPerMessage = 3;

            this.maxCharactersPerLine = this.MaxCharactersPerLineNormal;
        }

        public void WordWrap(string outputAinFilename)
        {
            var options = new CodeDisplayOptions();
            var projectWriter = new AssemblerProjectWriter(ainFile, options);
            projectWriter.BeforeWritingInstruction += new EventHandler<InstructionInfoEventArgs>(projectWriter_BeforeWritingInstruction);


            string tempFile = Path.GetTempFileName();
            try
            {
                var saveProjectInBackground = new ExplorerForm.SaveProjectInBackground();
                saveProjectInBackground.SaveAsProject(projectWriter, tempFile, true);

                var buildProjectInBackground = new ExplorerForm.BuildProjectInBackground();
                buildProjectInBackground.Run(tempFile, outputAinFilename, true);

                //projectWriter.SaveAsProject(tempFile, true);
                //var projectReader = new AssemblerProjectReader();
                //projectReader.LoadProject(tempFile);
                //var outputAinFile = projectReader.MakeAinFile();
                //outputAinFile.WriteAndEncryptAinFile(outputAinFilename);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        int maxCharactersPerLine;
        int currentTextLine;

        /// <summary>
        /// When it sees one of these characters, it indents one more fullwidth space.
        /// </summary>
        static HashSet<char> indentCharacters = new HashSet<char>("　‘“（〔［｛〈《「『【".ToCharArray());

        /// <summary>
        /// When it sees one of these characters, it treats them like ASCII characters for the purpose of wrapping lines.
        /// </summary>
        static HashSet<char> terminatingCharacters = new HashSet<char>("”’゛）〕］｝〉》」』】。、，．：？！".ToCharArray());

        /// <summary>
        /// Count the number of fullwidth spaces that must be added to maintain indentation.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        static int GetIndentSize(string text)
        {
            int indentSize = 0;
            int i = 0;
            int textLength = text.Length;
            while (i < textLength && indentCharacters.Contains(text[i]))
            {
                i++;
            }
            indentSize = i;
            return indentSize;
        }


        string remainingText = "";

        public void projectWriter_BeforeWritingInstruction(object sender, InstructionInfoEventArgs e)
        {
            var instructionInfo = e.InstructionInfo;

            if (instructionInfo.instruction == Instruction.MSG)
            {
                string messageText = e.Text;
                //string messageText = ainFile.Messages[instructionInfo.word1];
                messageText = CombineRemainingText(messageText);
                e.Text = messageText;
                HandleMessage(e, messageText);
            }
            else if (instructionInfo.instruction == Instruction.CALLFUNC)
            {
                var function = ainFile.GetFunction(instructionInfo.word1);
                string functionName = null;
                if (function != null)
                {
                    functionName = function.Name;
                }
                if (functionName == NextLineFunctionName)
                {
                    if (remainingText != "")
                    {
                        e.Handled = true;
                    }
                    else
                    {
                        NextLine(e);
                    }
                }
                else if (functionName == NextMessageFunctionName)
                {
                    if (remainingText != "")
                    {
                        while (remainingText != "")
                        {
                            e.Handled = true;
                            HandleMessage(e, remainingText);
                        }

                        GenerateNextMessage(e);
                    }
                    else
                    {
                        NextMessage();
                    }
                }
                else if (ReduceMarginFunctionNames.Contains(functionName))
                {
                    this.maxCharactersPerLine = this.MaxCharactersPerLineReduced;
                }
            }
        }

        private string CombineRemainingText(string messageText)
        {
            if (String.IsNullOrEmpty(messageText))
            {
                messageText = remainingText;
                remainingText = "";
                return messageText;
            }

            int indentSize = GetIndentSize(messageText);
            if (remainingText != "")
            {
                if (indentSize > 0)
                {
                    //remove initial indent
                    messageText = messageText.Substring(indentSize);
                }
                messageText = remainingText + " " + messageText;
                remainingText = "";
            }
            return messageText;
        }

        private void HandleMessage(InstructionInfoEventArgs e, string messageText)
        {
            int indentSize = GetIndentSize(messageText);
            int realSplitPosition = FindCharacterIndexOfRightMargin(messageText, maxCharactersPerLine);

            if (realSplitPosition < messageText.Length || e.Handled == true)
            {
                e.Handled = true;
                while (realSplitPosition < messageText.Length)
                {
                    int position = FindSplitPoint(messageText, realSplitPosition);
                    if (position <= indentSize)
                    {
                        //for really long lines - split them at the wrap position instead of an infinite loop
                        position = realSplitPosition;
                    }

                    string line = messageText.Substring(0, position);

                    e.WriteLine(" " + AssemblerProjectWriter.EscapeAndQuoteMessage(line) + " ");
                    GenerateNextLine(e);
                    e.WriteLine("");

                    messageText = messageText.Substring(position);

                    //eat spaces
                    position = 0;
                    while (position < messageText.Length && messageText[position] == ' ')
                    {
                        position++;
                    }
                    if (position > 0)
                    {
                        messageText = messageText.Substring(position);
                    }

                    //add indent if enabled
                    if (MaintainIndentation)
                    {
                        if (indentSize > 0)
                        {
                            messageText = "".PadLeft(indentSize, '　') + messageText;
                        }
                    }

                    //find next split
                    realSplitPosition = FindCharacterIndexOfRightMargin(messageText, maxCharactersPerLine);
                }

                //remaining text is set to something if this is a "next message" call, so output any leftover text at that time.
                //Otherwise, if we have the leftover text feature disabled, output it now.
                if (!this.RemoveLineBreaksIfWordWrapping || this.remainingText != "")
                {
                    e.WriteText(" " + AssemblerProjectWriter.EscapeAndQuoteMessage(messageText) + " ");
                    this.remainingText = "";
                }
                else
                {
                    this.remainingText = messageText;
                }
            }
        }

        void GenerateNextLine(InstructionInfoEventArgs e)
        {
            if (this.currentTextLine < MaxLinesPerMessage)
            {
                if (this.currentTextLine == MaxLinesPerMessage - 1)
                {
                    e.Handled = true;
                }
                else
                {
                    e.WriteText(NextLineFunctionCode);
                    e.Handled = true;
                }
            }
            NextLine(e);
        }

        private void NextLine(InstructionInfoEventArgs e)
        {
            this.currentTextLine++;
            if (this.currentTextLine >= MaxLinesPerMessage)
            {
                GenerateNextMessage(e);
            }
        }

        private void GenerateNextMessage(InstructionInfoEventArgs e)
        {
            e.WriteText(NextMessageFunctionCode);
            e.Handled = true;
            NextMessage();
        }

        private void NextMessage()
        {
            this.maxCharactersPerLine = this.MaxCharactersPerLineNormal;
            this.currentTextLine = 0;
        }

        private static int FindSplitPoint(string line, int position)
        {
            int initialPosition = position;
            bool foundSpace = false;
            while (position >= 0)
            {
                //find a space, then find a non-space
                char c = line[position];

                if (c >= 0x80 && !terminatingCharacters.Contains(c))
                {
                    break;
                }
                if (c == ' ')
                {
                    if (!foundSpace)
                    {
                        foundSpace = true;
                    }
                }
                else
                {
                    if (foundSpace)
                    {
                        break;
                    }
                }
                position--;
            }
            position++;

            if (position <= 0 || position > initialPosition)
            {
                position = initialPosition;
            }
            return position;
        }

        CharacterWidthCache characterWidthCache = null;

        /// <summary>
        /// Returns the character index where the text hits the margin, using either fixed-width or variable-width rules.
        /// </summary>
        /// <param name="messageText">The text to look inside</param>
        /// <param name="position"></param>
        /// <returns></returns>
        private int FindCharacterIndexOfRightMargin(string messageText, int position)
        {
            if (!this.UseVariableWidthFont)
            {
                int stringLength = messageText.Length;
                int x = 0;
                int targetX = position;
                int i = 0;
                while (x < targetX && i < stringLength)
                {
                    char c = messageText[i];
                    if (c >= 0x80 && !(c >= 0xFF61 && c <= 0xFF9F))
                    {
                        x += 2;
                    }
                    else
                    {
                        x++;
                    }
                    i++;
                }
                return i;
            }
            else
            {
                int stringLength = messageText.Length;
                float targetX = position + 0.25f;
                float x = 0;
                int i = 0;
                while (x < targetX && i < stringLength)
                {
                    x += characterWidthCache.GetRelativeWidth(messageText[i]);
                    if (x < targetX)
                    {
                        i++;
                    }
                }
                return i;
            }
        }

    }

    class WordWrapperSimple
    {
        public WordWrapperSimple(WordWrapOptions wordWrapOptions)
        {
            this.WordWrapOptions = wordWrapOptions;
            if (this.WordWrapOptions != null)
            {
                this.CurrentProfile = this.WordWrapOptions.WordWrapOptionsProfiles.FirstOrDefault();
                if (this.WordWrapOptions.UseVariableWidthFont)
                {
                    this.characterWidthCache = new CharacterWidthCache(wordWrapOptions);
                }
            }
        }

        int maxLinesPerMessage;
        int maxCharactersPerLine;

        bool doNotWrapThis = false;

        public WordWrapOptions WordWrapOptions = null;
        public WordWrapOptionsProfile CurrentProfile = null;

        public bool SwitchToNormalProfileOnA;
        public WordWrapOptionsProfile NormalProfile = null;

        public string[] WordWrapLines(IEnumerable<string> lines)
        {
            if (WordWrapOptions == null || CurrentProfile == null || WordWrapOptions.Disabled)
            {
                return lines.ToArray();
            }

            this.maxLinesPerMessage = CurrentProfile.MaxLinesPerMessage;
            this.maxCharactersPerLine = CurrentProfile.MaxCharactersPerLine;
            //scale = 1.0;

            //bool sizeTerminatedSameLine = false;
            //bool isFirstLine = true;
            doNotWrapThis = false;

            List<string> outputLines = new List<string>();
            string remainingCharacters = "";

            foreach (var line in lines)
            {
                //NEW: ignore fake lines that are actually code patches
                if (line.StartsWith("\0@CODE"))
                {
                    WordWrapLine(null, ref remainingCharacters, outputLines);
                    outputLines.Add(line);
                    continue;
                }

                WordWrapLine(line, ref remainingCharacters, outputLines);
                if (line == "@" && SwitchToNormalProfileOnA)
                {
                    CurrentProfile = NormalProfile;
                    this.maxLinesPerMessage = CurrentProfile.MaxLinesPerMessage;
                    this.maxCharactersPerLine = CurrentProfile.MaxCharactersPerLine;
                }

                //if (sizeTerminatedSameLine && isFirstLine)
                //{
                //    WordWrapLine(null, ref remainingCharacters, outputLines);
                //    this.maxCharactersPerLine = CurrentProfile.MaxCharactersPerLine;
                //    this.maxLinesPerMessage = CurrentProfile.MaxLinesPerMessage;
                //}
                //isFirstLine = false;
            }
            WordWrapLine(null, ref remainingCharacters, outputLines);

            if (!doNotWrapThis)
            {
                //next count lines and add page breaks as needed
                int i = 0;
                int currentLineCount = 0;
                while (i < outputLines.Count)
                {
                    string line = outputLines[i];
                    if (line == "@")
                    {
                        currentLineCount = 0;
                    }
                    else if (line.StartsWith("\0@CODE"))
                    {
                        //NEW: ignore fake lines that are actually code patches
                    }
                    else
                    {
                        currentLineCount++;
                        if (currentLineCount > maxLinesPerMessage)
                        {
                            outputLines.Insert(i, "@");
                            currentLineCount = 1;
                        }
                    }
                    i++;
                }
            }
            return outputLines.ToArray();
        }

        public void WordWrapLine(string line, ref string remainingCharacters, List<string> outputLines)
        {
            if (!String.IsNullOrEmpty(remainingCharacters))
            {
                if (String.IsNullOrEmpty(line) || line == "@" || line == "@''")
                {
                    outputLines.Add(remainingCharacters);
                    if (line == "@")
                    {
                        outputLines.Add(line);
                    }
                    remainingCharacters = "";
                    return;
                }
                //add line to remainingCharacters
                int i = 0;
                while (i < line.Length && char.IsWhiteSpace(line, i))
                {
                    i++;
                }
                line = remainingCharacters + " " + line.Substring(i);
                remainingCharacters = "";
            }

            if (line == null && String.IsNullOrEmpty(remainingCharacters))
            {
                return;
            }

            if (CurrentProfile == null)
            {
                outputLines.Add(line);
                return;
            }

            int rightMarginPoint = FindCharacterIndexOfRightMargin(line, maxCharactersPerLine);
            if (WordWrapOptions.IgnoreAngleBraces && line.Contains("<S"))
            {
                doNotWrapThis = true;
            }
            if (doNotWrapThis)
            {
                rightMarginPoint = line.Length;
            }

            if (rightMarginPoint == line.Length)
            {
                //nothing needed to do
                outputLines.Add(line);
                return;
            }
            bool indentIsHalfwidth = false;
            int indentSize = GetIndentSize(line, out indentIsHalfwidth);

            while (rightMarginPoint < line.Length)
            {
                int position = FindSplitPoint(line, rightMarginPoint);
                if (position <= indentSize)
                {
                    position = rightMarginPoint;
                }

                string piece = line.Substring(0, position);
                outputLines.Add(piece);
                line = line.Substring(position);

                //eat spaces
                position = 0;
                while (position < line.Length && Char.IsWhiteSpace(line, position))
                {
                    position++;
                }
                line = line.Substring(position);
                if (indentSize > 0)
                {
                    if (indentIsHalfwidth)
                    {
                        //halfwidth space
                        line = "".PadLeft(indentSize, ' ') + line;
                    }
                    else
                    {
                        //fullwidth space
                        line = "".PadLeft(indentSize, '　') + line;
                    }
                }

                rightMarginPoint = FindCharacterIndexOfRightMargin(line, maxCharactersPerLine);
            }
            remainingCharacters = line;
            if (!this.WordWrapOptions.RemoveLineBreaksIfWordWrapping)
            {
                outputLines.Add(remainingCharacters);
                remainingCharacters = "";
            }
        }

        private int GetIndentSize(string text, out bool indentIsHalfwidth)
        {
            indentIsHalfwidth = false;
            if (!this.WordWrapOptions.MaintainIndentation)
            {
                return 0;
            }

            int indentSize = 0;
            int i = 0;
            int textLength = text.Length;
            while (i < textLength && indentCharacters.Contains(text[i]))
            {
                i++;
            }
            indentSize = i;

            if (indentSize == 0)
            {
                i = 0;
                //first do spaces
                while (i < textLength && text[i] == ' ')
                {
                    i++;
                }
                //then try quotes
                if (i < textLength && text[i] == '"')
                {
                    //count quotes
                    int quoteCount = text.ToCharArray().Count('"');

                    //if it ends with a quotation mark, and has an even number of quotation marks, do not count the last one
                    if (0 == (quoteCount & 1))
                    {
                        if (text.TrimEnd().EndsWith("\""))
                        {
                            quoteCount--;
                        }
                    }

                    //if there's an odd number of quotes, indent them like spaces
                    if (0 != (quoteCount & 1))
                    {
                        while (i < textLength && text[i] == '"')
                        {
                            i++;
                        }
                    }
                }
                //do spaces again?
                while (i < textLength && text[i] == ' ')
                {
                    i++;
                }
                if (i > 0)
                {
                    indentSize = i;
                    indentIsHalfwidth = true;
                }
            }

            return indentSize;
        }

        /// <summary>
        /// When it sees one of these characters, it indents one more fullwidth space.
        /// </summary>
        static HashSet<char> indentCharacters = new HashSet<char>("　‘“（〔［｛〈《「『【".ToCharArray());

        /// <summary>
        /// When it sees one of these characters, it treats them like ASCII characters for the purpose of wrapping lines.
        /// </summary>
        static HashSet<char> terminatingCharacters = new HashSet<char>("”’゛）〕］｝〉》」』】。、，．：？！.,".ToCharArray());

        private int FindSplitPoint(string line, int position)
        {
            int initialPosition = position;
            bool foundSpace = false;
            while (position >= 0)
            {
                //find a space, then find a non-space
                char c = line[position];

                if (c >= 0x80 && !terminatingCharacters.Contains(c))
                {
                    break;
                }
                if (c == ' ')
                {
                    if (!foundSpace)
                    {
                        foundSpace = true;
                    }
                }
                else
                {
                    if (foundSpace)
                    {
                        break;
                    }
                }
                position--;
            }
            position++;

            if (position <= 0 || position > initialPosition)
            {
                position = initialPosition;
            }
            return position;
        }

        /// <summary>
        /// Returns the character index where the text hits the margin, using either fixed-width or variable-width rules.
        /// </summary>
        /// <param name="messageText">The text to look inside</param>
        /// <param name="position"></param>
        /// <returns></returns>
        private int FindCharacterIndexOfRightMargin(string messageText, int position)
        {
            bool insideTag = false;

            if (!this.WordWrapOptions.UseVariableWidthFont)
            {
                int stringLength = messageText.Length;
                int x = 0;
                int targetX = position;
                int i = 0;
                while (x <= targetX && i < stringLength)
                {
                    char c = messageText[i];
                    if (c == '<' && !insideTag && this.WordWrapOptions.IgnoreAngleBraces)
                    {
                        insideTag = true;
                    }
                    else if (c == '>' && insideTag)
                    {
                        insideTag = false;
                    }

                    if (!insideTag)
                    {
                        if (c >= 0x1000)
                        {
                            if (c >= 0xFF61 && c <= 0xFF9F)
                            {
                                x++;
                            }
                            else
                            {
                                x += 2;
                            }
                        }
                        else if (c < 0x80)
                        {
                            x++;
                        }
                        else if ((c >= 0xBF && c <= 0xFF && c != '×' && c != '÷') || c == '¡')
                        {
                            x++;
                        }
                        else
                        {
                            x += 2;
                        }
                    }
                    if (x > targetX)
                    {
                        //allow one terminating character to exceed the margin
                        if (terminatingCharacters.Contains(c))
                        {
                            if (i < stringLength)
                            {
                                i++;
                            }
                            else
                            {

                            }
                        }
                        break;
                    }
                    i++;
                }
                return i;
            }
            else
            {
                int stringLength = messageText.Length;
                float targetX = position + 0.25f;
                float x = 0;
                int i = 0;
                while (x < targetX && i < stringLength)
                {
                    char c = messageText[i];
                    if (c == '<' && !insideTag && this.WordWrapOptions.IgnoreAngleBraces)
                    {
                        insideTag = true;
                    }
                    else if (c == '>' && insideTag)
                    {
                        insideTag = false;
                    }

                    if (!insideTag)
                    {
                        x += characterWidthCache.GetRelativeWidth(messageText[i]);
                        if (x < targetX)
                        {
                            i++;
                        }
                    }
                    else
                    {
                        i++;
                    }
                }
                return i;
            }
        }
        CharacterWidthCache characterWidthCache = null;

    }
}
