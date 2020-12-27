using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace AinDecompiler
{
    public class MessageGroup
    {
        //CALL TO FUNCTION R or MESSAGE begins a message group
        //A message group is terminated by either:
        //  CALL TO A without a message after it
        //  Anything not a message, Call to R, or Call to A
        //  Second consecutive message

        public List<string> MessageText = new List<string>();
        public List<int> MessageNumbers = new List<int>();
        public List<int> MessageIndexNumbers = new List<int>();
        public int StartAddress;
        public int EndAddress;

        public int FirstMessageNumber;
        public int LastMessageNumber;

        public int MessageGroupNumber;

        public bool EndsWithNonA = false;
        public bool EndsWithNewline = false;
    }

    public class MessageGroupFinder
    {
        AinFile ainFile;
        public int MessageGroupNumber;
        public int MessageNumber;

        Function currentFunction;
        Dictionary<int, string> _labels;
        Dictionary<string, int> _labelsInverse;

        Dictionary<int, string> labels
        {
            get
            {
                if (_labels == null)
                {
                    FindLabels();
                }
                return _labels;
            }
        }

        Dictionary<string, int> labelsInverse
        {
            get
            {
                if (_labelsInverse == null)
                {
                    FindLabels();
                }
                return _labelsInverse;
            }
        }

        private void FindLabels()
        {
            this._labels = new Dictionary<int, string>();
            this._labelsInverse = new Dictionary<string, int>();
            var labelFinder = new AssemblerProjectWriter.LabelFinder(_labels, _labelsInverse, ainFile);
            labelFinder.FindLabels(currentFunction.Address, currentFunction.GetEndAddress());
        }

        private byte[] aFunctionCode;
        private byte[] rFunctionCode;
        int aFunctionIndex;
        int rFunctionIndex;
        int cFunctionIndex;
        bool rFunctionIsNormal;
        bool aFunctionIsNormal;

        public void SetFunction(Function function)
        {
            this.currentFunction = function;
            this._labels = null;
            this._labelsInverse = null;
        }

        public MessageGroupFinder(AinFile ainFile)
        {
            this.ainFile = ainFile;
            aFunctionCode = DefaultWordWrapOptions.GetGeneratedCodeBytes(ainFile, "A");
            rFunctionCode = DefaultWordWrapOptions.GetGeneratedCodeBytes(ainFile, "R");
            aFunctionIndex = ainFile.FunctionNameToIndex.GetOrNull("A");
            rFunctionIndex = ainFile.FunctionNameToIndex.GetOrNull("R");
            cFunctionIndex = ainFile.FunctionNameToIndex.GetOrNull("C");
            aFunctionIsNormal = aFunctionCode.Length == 6;
            rFunctionIsNormal = rFunctionCode.Length == 6;
        }

        private void Peek(int address, out bool isMessage, out bool isA, out bool isR, out int word1, out int nextAddress)
        {
            isMessage = false;
            isA = false;
            isR = false;

            var ins = ainFile.Peek(address);
            nextAddress = ins.nextAddress;
            word1 = ins.word1;
            if (ins.instruction == Instruction.MSG)
            {
                isMessage = true;
                return;
            }
            else if (ins.instruction == Instruction.CALLFUNC && ins.word1 == aFunctionIndex)
            {
                isA = true;
                return;
            }
            else if (ins.instruction == Instruction.CALLFUNC && ins.word1 == rFunctionIndex)
            {
                isR = true;
                return;
            }
            if (TextImportExport.CompareBytes(ainFile.Code, address, this.aFunctionCode))
            {
                isA = true;
                nextAddress = address + this.aFunctionCode.Length;
            }
            if (TextImportExport.CompareBytes(ainFile.Code, address, this.rFunctionCode))
            {
                isR = true;
                nextAddress = address + this.aFunctionCode.Length;
            }
        }

        public bool IsMessageOrR(int address)
        {
            var ins = ainFile.Peek(address);
            if (ins.instruction == Instruction.MSG)
            {
                return true;
            }
            else if (ins.instruction == Instruction.CALLFUNC)
            {
                if (ins.word1 == rFunctionIndex)
                {
                    if (rFunctionIsNormal)
                    {
                        var previousIns = ainFile.Peek(address - 6);
                        if (previousIns.instruction == Instruction.CALLFUNC && previousIns.word1 == cFunctionIndex)
                        {
                            return false;
                        }

                        return true;
                    }
                    else
                    {
                        if (TextImportExport.CompareBytes(ainFile.Code, ins.nextAddress - rFunctionCode.Length, rFunctionCode))
                        {
                            var previousIns = ainFile.Peek(ins.nextAddress - rFunctionCode.Length - 6);
                            if (previousIns.instruction == Instruction.CALLFUNC && previousIns.word1 == cFunctionIndex)
                            {
                                return false;
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public MessageGroup GetMessageGroup(int address)
        {
            MessageGroup messageGroup = new MessageGroup();
            messageGroup.StartAddress = address;
            messageGroup.FirstMessageNumber = this.MessageNumber;

            bool currentIsMsg = false;
            bool currentIsR = false;
            bool currentIsA = false;
            bool previousWasMsg = false;
            bool previousWasR = false;

            int startAddress = address;

            while (address < ainFile.Code.Length)
            {
                if (labels.ContainsKey(address) && address != startAddress)
                {
                    break;
                }
                int messageIndex;
                int nextAddress;
                Peek(address, out currentIsMsg, out currentIsA, out currentIsR, out messageIndex, out nextAddress);

                bool nextIsMsg, nextIsA, nextIsR;
                int dummyIndex, dummyAddress;
                Peek(nextAddress, out nextIsMsg, out nextIsA, out nextIsR, out dummyIndex, out dummyAddress);

                if (!currentIsMsg && !currentIsR && !currentIsA)
                {
                    break;
                }
                if (currentIsMsg)
                {
                    if (previousWasMsg)
                    {
                        break;
                    }

                    messageGroup.MessageIndexNumbers.Add(messageIndex);
                    messageGroup.MessageNumbers.Add(this.MessageNumber);
                    messageGroup.MessageText.Add(ainFile.GetMessage(messageIndex));
                    this.MessageNumber++;
                }
                else if (currentIsR)
                {
                    if (previousWasMsg)
                    {
                        if (nextIsA || nextIsR || nextIsMsg)
                        {

                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        messageGroup.MessageIndexNumbers.Add(-1);
                        messageGroup.MessageNumbers.Add(-1);
                        messageGroup.MessageText.Add("");
                    }
                }
                else if (currentIsA)
                {
                    if (nextIsR || nextIsMsg)
                    {
                        messageGroup.MessageIndexNumbers.Add(-2);
                        messageGroup.MessageNumbers.Add(-2);
                        messageGroup.MessageText.Add("@");
                    }
                    else
                    {
                        break;
                    }
                }

                previousWasR = currentIsR;
                previousWasMsg = currentIsMsg;
                address = nextAddress;
            }
            messageGroup.LastMessageNumber = this.MessageNumber - 1;
            messageGroup.EndAddress = address;
            messageGroup.MessageGroupNumber = MessageGroupNumber;

            messageGroup.EndsWithNonA = !currentIsA;
            messageGroup.EndsWithNewline = previousWasR;
            MessageGroupNumber++;

            return messageGroup;
        }
    }


    public class TextImportExport
    {
        AinFile ainFile;
        protected EnumerationType AnnotateEnumerationType
        {
            get
            {
                return _annotateEnumerationType;
            }
            set
            {
                _annotateEnumerationType = value;
                GetVariablesUsingEnumeartionType();
            }
        }

        protected void SetAnnotationFunctionParameters(IEnumerable<Variable> variables)
        {
            if (variables == null)
            {
                return;
            }
            foreach (var variable in variables)
            {
                MarkVariableForAnnotation(variable);
            }
        }

        IDictionary<string, string> replacementStringsForAnnotations = new Dictionary<string, string>();

        internal void AnnotateParameterWithStrings(IDictionary<string, string> replacementStrings, params Variable[] variables)
        {
            this.replacementStringsForAnnotations = replacementStrings ?? new Dictionary<string, string>();
            foreach (var variable in variables)
            {
                MarkVariableForAnnotation(variable);
            }
        }

        private void MarkVariableForAnnotation(Variable variable)
        {
            if (variable == null)
            {
                return;
            }
            if (_annotateEnumerationType == null)
            {
                _annotateEnumerationType = EnumerationType.Dummy;
            }

            VariablesUsingEnumerationType.Add(variable);
            var function = variable.Parent as Function;
            if (function != null)
            {
                VariablesUsingEnumerationType.Add(function);
            }
        }

        private void GetVariablesUsingEnumeartionType()
        {
            VariablesUsingEnumerationType.Clear();
            if (_annotateEnumerationType == null)
            {
                return;
            }
            foreach (var pair in ainFile.MetadataFile.Metadata)
            {
                var variable = pair.Key;
                var metaData = pair.Value;
                if (metaData.EnumerationType == _annotateEnumerationType)
                {
                    VariablesUsingEnumerationType.Add(variable);
                    var function = variable.Parent as Function;
                    if (function != null)
                    {
                        VariablesUsingEnumerationType.Add(function);
                    }
                }
            }

        }

        EnumerationType _annotateEnumerationType = null;
        HashSet<IVariable> VariablesUsingEnumerationType = new HashSet<IVariable>();

        public bool IncludeMessages = true;
        public bool CombineMessages = true;
        public const bool EmitPageBreaks = true;
        public bool IncludeStrings = true;

        StringExportImport stringExportImport = null;
        public WordWrapOptionsOld wordWrapOptionsOld;
        public WordWrapOptions wordWrapOptions;
        public HashSet<string> StringsToMatch = new HashSet<string>();
        bool useStringsToMatch = false;

        public TextImportExport(AinFile ainFile)
        {
            this.ainFile = ainFile;
            this.stringExportImport = new StringExportImport(ainFile);
            this.stringExportImport.GetExclusionList();

            this.messageGroupFinder = new MessageGroupFinder(ainFile);
        }

        public void SaveText(string fileName, Encoding encoding)
        {
            using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                using (var streamWriter = new StreamWriter(fileStream, encoding))
                {
                    SaveText(streamWriter);
                    streamWriter.Flush();
                    fileStream.Flush();
                    streamWriter.Close();
                    fileStream.Close();
                }
            }
        }

        public void SaveText(string fileName)
        {
            SaveText(fileName, Encoding.UTF8);  //utf8 with BOM
        }

        public void SaveText(TextWriter tw)
        {
            //iterate through the functions
            //enumerate strings and messages used
            //skip anything that uses no strings, no messages, or only empty strings
            //display "FUNCTION x functionName"
            //display "#x strings, x messages

            foreach (var function in ainFile.Functions)
            {
                var functionLines = GetTextFromFunction(function);
                if (functionLines.Count > 2)
                {
                    foreach (var line in functionLines)
                    {
                        tw.WriteLine(line);
                    }
                }
            }
        }

        MessageGroupFinder messageGroupFinder = null;

        private List<string> GetTextFromFunction(Function function)
        {
            messageGroupFinder.MessageGroupNumber = 0;
            messageGroupFinder.MessageNumber = 0;
            messageGroupFinder.SetFunction(function);

            int numberOfNonCommentedLines = 0;
            int numberOfStrings = 0;
            int numberOfMessages = 0;

            useStringsToMatch = StringsToMatch != null && StringsToMatch.Count > 0;
            List<string> functionLines = new List<string>();
            string functionLineString = "FUNCTION " + /*function.Index.ToString() + " " + */ AssemblerProjectWriter.SanitizeVariableName(function.Name);

            functionLines.Add(functionLineString);
            functionLines.Add("#x strings, x messages");  //this line gets changed later (it's index 1)
            int address = function.Address;
            string lastSeenName = null;

            while (address < ainFile.Code.Length)
            {
                var instructionInfo = ainFile.Peek(address);
                int nextAddress = instructionInfo.nextAddress;
                if (instructionInfo.instruction == Instruction.ENDFUNC || instructionInfo.instruction == Instruction.FUNC)
                {
                    break;
                }
                if (this.AnnotateEnumerationType != null && instructionInfo.instruction == Instruction.CALLFUNC && this.IncludeMessages)
                {
                    CheckFunctionInstructionForCharacterName(functionLines, ref lastSeenName, instructionInfo);
                }

                bool isMsgOrR = messageGroupFinder.IsMessageOrR(address);
                MessageGroup messageGroup = null;
                if (isMsgOrR)
                {
                    messageGroup = messageGroupFinder.GetMessageGroup(address);
                    if (messageGroup.LastMessageNumber >= messageGroup.FirstMessageNumber)
                    {
                        nextAddress = messageGroup.EndAddress;

                        if (this.IncludeMessages)
                        {
                            if (this.CombineMessages)
                            {
                                OutputMessageGroup(functionLines, messageGroup);
                            }
                            else
                            {
                                OutputMessagesOld(functionLines, messageGroup);
                            }
                        }
                        int messagesInBlock = (messageGroup.LastMessageNumber - messageGroup.FirstMessageNumber) + 1;
                        numberOfMessages += messagesInBlock;
                        numberOfNonCommentedLines += messagesInBlock;
                    }
                }
                else
                {
                    if (instructionInfo.instruction == Instruction.STRSWITCH)
                    {
                        int switchBlockNumber = instructionInfo.word1;
                        var switchBlock = ainFile.Switches.GetOrNull(switchBlockNumber);
                        if (switchBlock != null)
                        {
                            foreach (var switchCase in switchBlock.SwitchCases)
                            {
                                int stringNumber = switchCase.Value;
                                string str = ainFile.GetString(stringNumber);
                                if (str != null)
                                {
                                    AddString(ref numberOfNonCommentedLines, ref numberOfStrings, functionLines, stringNumber, str);
                                }
                            }
                        }
                    }
                    else
                    {
                        int indexOfStringArgument = instructionInfo.instruction.IndexOfStringArgument();
                        if (indexOfStringArgument != -1)
                        {
                            int stringNumber = instructionInfo.words[indexOfStringArgument];
                            string str = ainFile.GetString(stringNumber);
                            if (str != null)
                            {
                                AddString(ref numberOfNonCommentedLines, ref numberOfStrings, functionLines, stringNumber, str);
                            }
                        }
                    }
                }
                address = nextAddress;
            }

            functionLines[1] = "#" + numberOfStrings.ToString() + " strings, " + numberOfMessages.ToString() + " messages";
            if (numberOfNonCommentedLines == 0)
            {
                functionLines.Clear();
            }
            return functionLines;
        }

        private static void OutputMessagesOld(List<string> functionLines, MessageGroup messageGroup)
        {
            for (int i = 0; i < messageGroup.MessageText.Count; i++)
            {
                string messageText = messageGroup.MessageText[i];
                int messageNumber = messageGroup.MessageNumbers[i];
                if (messageNumber >= 0)
                {
                    string outputLine = "m " + messageNumber.ToString("000") + " " + StringExportImport.EscapeText(messageText);
                    functionLines.Add(outputLine);
                }
            }
        }

        private static void OutputMessageGroup(List<string> functionLines, MessageGroup messageGroup)
        {
            string prefix = "";

            //find a @@ line that isn't commented out
            int i = functionLines.Count - 1;
            int indexOfDummyLine = -1;
            while (i >= 0)
            {
                if (functionLines[i].StartsWith("#"))
                {
                    //proceed
                }
                else if (functionLines[i] == "@@")
                {
                    indexOfDummyLine = i;
                    break;
                }
                else
                {
                    break;
                }
                i--;
            }

            if (indexOfDummyLine != -1)
            {
                prefix = "@";
            }

            string messageLine = prefix + "mg " + messageGroup.MessageGroupNumber.ToString("000") + " " +
                messageGroup.FirstMessageNumber.ToString("000") + "-" + messageGroup.LastMessageNumber.ToString("000") + ":";

            if (indexOfDummyLine != -1)
            {
                functionLines[indexOfDummyLine] = messageLine;
            }
            else
            {
                functionLines.Add(messageLine);
            }

            for (i = 0; i < messageGroup.MessageText.Count; i++)
            {
                if (messageGroup.MessageNumbers[i] == -2)
                {
                    functionLines.Add("@");
                }
                else if (messageGroup.MessageNumbers[i] == -1)
                {
                    functionLines.Add("");
                }
                else
                {
                    string messageText = messageGroup.MessageText[i];
                    if (messageText == "")
                    {
                        functionLines.Add("@''");
                    }
                    else
                    {
                        functionLines.Add(StringExportImport.EscapeText(messageText));
                    }
                }
            }
            functionLines.Add("@@");
        }

        private string GetLastFunctionLine(List<string> functionLines, out int i)
        {
            i = functionLines.Count - 1;
            while (i > 0 && functionLines[i].StartsWith("#") && !functionLines[i].StartsWith("#x strings,"))
            {
                i--;
            }
            return functionLines[i];
        }

        private void CheckFunctionInstructionForCharacterName(List<string> outputLines, ref string lastSeenName, InstructionInfo instructionInfo)
        {
            var func = ainFile.GetFunction(instructionInfo.word1);
            if (VariablesUsingEnumerationType.Contains(func))
            {
                var parameters = GetParametersThatUsesEnumerationType(func);
                if (parameters.FirstOrDefault() != null)
                {
                    string characterName = "";
                    foreach (var parameter in parameters)
                    {
                        int i = parameter.Index;
                        int addr = instructionInfo.CurrentAddress - (func.ParameterCount) * 6 + i * 6;
                        var ins2 = ainFile.Peek(addr);
                        if (ins2.instruction == Instruction.PUSH)
                        {
                            string enumerationValue = this.AnnotateEnumerationType.GetOrDefault(ins2.word1, "");
                            if (!String.IsNullOrEmpty(enumerationValue))
                            {
                                characterName = enumerationValue;
                                //outputLines.Add("#" + enumerationValue);
                            }
                            else
                            {

                            }
                        }
                        else if (ins2.instruction == Instruction.S_PUSH)
                        {
                            string str = ainFile.GetString(ins2.word1);
                            if (!String.IsNullOrEmpty(str))
                            {
                                if (this.replacementStringsForAnnotations != null && this.replacementStringsForAnnotations.ContainsKey(str))
                                {
                                    string nextStr = this.replacementStringsForAnnotations[str];
                                    if (!nextStr.StartsWith("*"))
                                    {
                                        str = nextStr;
                                    }
                                    else
                                    {
                                        str = lastSeenName;
                                    }
                                }
                                else
                                {

                                }
                                characterName = str;
                            }
                        }
                        else if (ins2.instruction == Instruction.SH_GLOBALREF)  //name of global variable - for Beat Blades Haruka
                        {
                            var global = ainFile.GetGlobal(ins2.word1);
                            if (global != null)
                            {
                                string str = global.Name;
                                if (this.replacementStringsForAnnotations != null && this.replacementStringsForAnnotations.ContainsKey(str))
                                {
                                    string nextStr = this.replacementStringsForAnnotations[str];
                                    if (!nextStr.StartsWith("*"))
                                    {
                                        str = nextStr;
                                    }
                                    else
                                    {
                                        str = lastSeenName;
                                    }
                                    characterName = str;
                                }
                                else
                                {

                                }
                            }
                        }
                    }
                    if (lastSeenName != characterName)
                    {
                        lastSeenName = characterName;
                        outputLines.Add("#" + characterName);
                    }
                }
            }
        }

        private IEnumerable<Variable> GetParametersThatUsesEnumerationType(Function func)
        {
            var parameters = func.Parameters.Where(v => VariablesUsingEnumerationType.Contains(v));
            return parameters;
        }

        private void AddString(ref int numberOfNonCommentedLines, ref int numberOfStrings, List<string> functionLines, int stringNumber, string str)
        {
            bool stringIsBlank = str == "";
            bool hasAnyText = functionLines.Count > 2;

            if (!(stringIsBlank && !hasAnyText))
            {
                string stringLine = "s " + numberOfStrings.ToString("000") + " " + StringExportImport.EscapeText(str);
                StringExclusionReason exclude = stringExportImport.stringsToExclude.GetOrDefault(stringNumber, StringExclusionReason.None);
                if (exclude != StringExclusionReason.None)
                {
                    stringLine = "#" + stringLine + "#\t\t" + exclude.GetText();
                }
                else
                {
                    if (str != "")
                    {
                        numberOfNonCommentedLines++;
                    }
                }

                if (useStringsToMatch == false || StringsToMatch.Contains(str))
                {
                    if (this.IncludeStrings == true)
                    {
                        functionLines.Add(stringLine);
                    }
                }
            }
            numberOfStrings++;
        }

        public bool ReplaceText(string textFileName, string outputFileName)
        {
            Encoding encoding = EncodingDetector.DetectEncoding(textFileName);
            using (var fileStream = new FileStream(textFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var streamReader = new StreamReader(fileStream, encoding))
                {
                    return ReplaceText(streamReader, textFileName, outputFileName);
                }
            }
        }

        public bool ReplaceText(StreamReader sr, string textFileName, string outputFileName)
        {
            var replacer = new Replacer(ainFile, outputFileName, wordWrapOptions);
            return replacer.ReplaceText(new TextReaderWrapper(sr), textFileName);
        }

        class Replacer
        {
            bool InsideFunction = false;
            bool hasOutputFunction = false;
            bool _wordWrap;

            public bool WordWrap
            {
                get
                {
                    return _wordWrap;
                }
                set
                {
                    _wordWrap = value;
                }
            }

            AinFile ainFile;
            AinFile originalAinFile;
            string outputFileName;
            WordWrapOptions wordWrapOptions = null;
            MessageGroupFinder messageGroupFinder = null;

            string aFunctionText;
            string rFunctionText;

            public bool DoNotWrapIfMatchesOriginal = false;

            public Replacer(AinFile ainFile, string outputFileName, WordWrapOptions wordWrapOptions)
            {
                this.ainFile = ainFile;
                this.originalAinFile = ainFile;
                this.outputFileName = outputFileName;
                this.wordWrapOptions = wordWrapOptions;
                if (this.wordWrapOptions != null)
                {
                    this.WordWrap = true;
                    this.wordWrapper = new WordWrapperSimple(wordWrapOptions);
                }

                this.aFunctionText = DefaultWordWrapOptions.GetGeneratedCodeText(ainFile, "A");
                this.rFunctionText = DefaultWordWrapOptions.GetGeneratedCodeText(ainFile, "R");

                this.messageGroupFinder = new MessageGroupFinder(ainFile);

                LoadWordWrapProfile();
            }

            private void LoadWordWrapProfile()
            {
                if (this.wordWrapOptions != null)
                {
                    foreach (var profile in this.wordWrapOptions.WordWrapOptionsProfiles)
                    {
                        var triggerBinaries = profile.GetTriggerBinaries(ainFile);
                        foreach (var triggerBinary in triggerBinaries)
                        {
                            var ins = Decompiler.Peek(triggerBinary, triggerBinary.Length - 6);
                            int functionIndex = -1;
                            if (ins.instruction == Instruction.CALLFUNC)
                            {
                                functionIndex = ins.word1;
                            }
                            else
                            {
                                //ins = Decompiler.Peek(triggerBinary, triggerBinary.Length - 2);
                                //if (ins.instruction == Instruction.POP || ins.instruction == Instruction.S_POP || ins.instruction == Instruction.SR_POP)
                                //{
                                //    ins = Decompiler.Peek(triggerBinary, triggerBinary.Length - 8);
                                //    if (ins.instruction == Instruction.CALLFUNC)
                                //    {
                                //        functionIndex = ins.word1;
                                //    }
                                //}
                            }
                            if (functionIndex != -1)
                            {
                                this.FunctionIndexToProfiles.GetOrAddNew(functionIndex).Add(profile);
                            }
                            else
                            {

                            }
                        }
                    }
                }

                int aFunctionIndex = ainFile.FunctionNameToIndex.GetOrNull("A");

                if (this.FunctionIndexToProfiles.ContainsKey(aFunctionIndex))
                {
                    var profile = this.FunctionIndexToProfiles[aFunctionIndex].FirstOrDefault();
                    this.wordWrapper.SwitchToNormalProfileOnA = true;
                    this.wordWrapper.NormalProfile = profile;
                }
            }
            WordWrapOptionsProfile currentWordWrapProfile = null;
            WordWrapOptionsProfile previousWordWrapProfile = null;
            Dictionary<int, HashSet<WordWrapOptionsProfile>> FunctionIndexToProfiles = new Dictionary<int, HashSet<WordWrapOptionsProfile>>();

            WordWrapperSimple wordWrapper;

            Dictionary<string, Dictionary<int, string>> stringEntries = new Dictionary<string, Dictionary<int, string>>();
            Dictionary<string, Dictionary<int, string>> messageEntries = new Dictionary<string, Dictionary<int, string>>();
            Dictionary<string, Dictionary<int, List<string>>> messageGroupEntries = new Dictionary<string, Dictionary<int, List<string>>>();

            Dictionary<string, string> CodePatches = new Dictionary<string, string>();
            HashSet<string> IncludedFiles = new HashSet<string>();
            StringBuilder CodePatches2 = new StringBuilder();

            string currentFunctionName;
            Dictionary<int, string> stringDictionary;
            Dictionary<int, string> messageDictionary;
            Dictionary<int, string> numberedStrings = new Dictionary<int, string>();
            Dictionary<int, List<string>> messageGroupDictionary;

            bool insideMessageGroup = false;
            List<string> currentMessageGroup = null;

            int firstStringNumberMsg = -1;

            public bool ReplaceText(TextReaderWrapper tr, string textFileName)
            {
                stringEntries.Clear();
                messageEntries.Clear();
                numberedStrings.Clear();
                CodePatches.Clear();
                CodePatches2.Length = 0;

                var dummy = new StringExportImport(ainFile);
                this.firstStringNumberMsg = dummy.GetFirstMessageIdNumber();

                currentFunctionName = "";
                stringDictionary = stringEntries.GetOrAddNew(currentFunctionName);
                messageDictionary = messageEntries.GetOrAddNew(currentFunctionName);
                messageGroupDictionary = messageGroupEntries.GetOrAddNew(currentFunctionName);

                try
                {
                    ReadReplacementFile(tr, textFileName);
                }
                catch (FileNotFoundException ex)
                {
                    string prompt = "File not found: " + ex.FileName + Environment.NewLine + "Check the filenames in your build file.";

                    if (Program.ConsoleMode)
                    {
                        Console.Error.WriteLine(prompt);
                    }
                    else
                    {
                        MessageBox.Show(prompt, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    }
                    return false;
                }

                if (ExportAndMerge())
                {
                    return true;
                }
                return false;
            }

            private void ReadReplacementFile(TextReaderWrapper tr, string textFileName)
            {
                SetCurrentFunction(null);

                textFileName = Path.GetFullPath(textFileName);
                if (IncludedFiles.Contains(textFileName.ToUpperInvariant()))
                {
                    return;
                }
                IncludedFiles.Set(textFileName.ToUpperInvariant());

                insideMessageGroup = false;

                string line;
                while (true)
                {
                    line = tr.ReadLine();
                    string originalLine = line;
                    if (line == null)
                    {
                        break;
                    }

                    //remove initial whitespace
                    line = line.TrimStart();

                    //check for "#include"
                    if (line.StartsWith("#include "))
                    {
                        string filenameToInclude = line.Substring("#include ".Length);
                        //check for quotes?
                        if (filenameToInclude.StartsWith("\"") && filenameToInclude.EndsWith("\""))
                        {
                            filenameToInclude = filenameToInclude.Substring(1, filenameToInclude.Length - 2);
                        }
                        string basePath = tr.DirectoryName;
                        filenameToInclude = Path.Combine(basePath, filenameToInclude);
                        if (!File.Exists(filenameToInclude))
                        {
                            throw new FileNotFoundException("Cannot find file: " + filenameToInclude, filenameToInclude);
                        }

                        filenameToInclude = Path.GetFullPath(filenameToInclude);

                        if (File.Exists(filenameToInclude) && !IncludedFiles.Contains(filenameToInclude.ToUpperInvariant()))
                        {
                            IncludedFiles.Add(filenameToInclude.ToUpperInvariant());
                            var encoding = EncodingDetector.DetectEncoding(filenameToInclude);

                            string ext = Path.GetExtension(filenameToInclude).ToLowerInvariant();
                            if (ext == ".jaf" || ext == ".hll")
                            {
                                string codeText = "#include " + AssemblerProjectWriter.EscapeAndQuoteString(filenameToInclude);
                                CodePatches2.AppendLine(codeText);
                            }
                            else if (ext == ".jam")
                            {
                                IncludeJamFile(filenameToInclude, encoding);
                            }
                            else
                            {
                                tr.IncludeTextReader(new StreamReader(filenameToInclude, encoding));
                            }
                        }
                        continue;
                    }


                    //remove commented text
                    int indexOfComment = line.IndexOf('#');
                    if (indexOfComment >= 0)
                    {
                        line = line.Substring(0, indexOfComment);
                        //explicitly ignore lines contining only comments at the start of the line
                        if (line.Length == 0)
                        {
                            continue;
                        }
                    }

                    //check for terminating a message group (@ at beginning of line)
                    if (line.StartsWith("@") && line.Length != 1 && line != "@''")
                    {
                        insideMessageGroup = false;
                        line = line.Substring(1);
                    }

                    //reading one of these lines:
                    //CODE
                    //function x functionName  (or func, f)
                    //string x text (or str, s)
                    //message x text (or msg, m)
                    //id x text (or i)
                    //x text (same as id x text)
                    //@''  (explicit null message)

                    string lineTrim = line.Trim();

                    if (!insideMessageGroup && (lineTrim.Equals("CODE", StringComparison.OrdinalIgnoreCase) || lineTrim.Equals("CODE2", StringComparison.OrdinalIgnoreCase)))
                    {
                        bool isCode2 = lineTrim.Equals("CODE2", StringComparison.OrdinalIgnoreCase);
                        StringBuilder codeText = new StringBuilder();
                        while (true)
                        {
                            line = tr.ReadLine();
                            lineTrim = line.Trim();
                            if (lineTrim.StartsWith("#include"))
                            {
                                string filenameToInclude = lineTrim.Substring("#include ".Length);
                                //check for quotes?
                                if (filenameToInclude.StartsWith("\"") && filenameToInclude.EndsWith("\""))
                                {
                                    filenameToInclude = filenameToInclude.Substring(1, filenameToInclude.Length - 2);
                                }
                                string basePath = tr.DirectoryName;
                                if (!Path.IsPathRooted(filenameToInclude))
                                {
                                    filenameToInclude = Path.Combine(basePath, filenameToInclude);
                                }
                                filenameToInclude = Path.GetFullPath(filenameToInclude);

                                if (!File.Exists(filenameToInclude))
                                {
                                    throw new FileNotFoundException("Cannot find file: " + filenameToInclude, filenameToInclude);
                                }

                                if (File.Exists(filenameToInclude) && !IncludedFiles.Contains(filenameToInclude.ToUpperInvariant()))
                                {
                                    IncludedFiles.Add(filenameToInclude.ToUpperInvariant());

                                    if (isCode2)
                                    {
                                        //replace with #include <fullpath>, let the compiler handle the actual include
                                        codeText.AppendLine("#include " + AssemblerProjectWriter.EscapeAndQuoteString(filenameToInclude));
                                    }
                                    else
                                    {
                                        var encoding = EncodingDetector.DetectEncoding(filenameToInclude);
                                        tr.IncludeTextReader(new StreamReader(filenameToInclude, encoding));
                                    }
                                }
                                continue;
                            }

                            if (lineTrim.ToUpperInvariant() == "ENDCODE")
                            {
                                if (isCode2)
                                {
                                    CodePatches2.AppendLine(codeText.ToString());
                                }
                                else
                                {
                                    if (currentMessageGroup != null && originalLine.Trim().Equals("@CODE", StringComparison.OrdinalIgnoreCase))
                                    {
                                        //new: code patch anchored to message group
                                        insideMessageGroup = true;
                                        currentMessageGroup.Add("\0@CODE " + codeText.ToString());
                                    }
                                    else
                                    {
                                        CodePatches.Set(currentFunctionName, codeText.ToString());
                                    }
                                }
                                break;
                            }
                            else
                            {
                                codeText.AppendLine(line);
                            }
                        }
                        continue;
                    }

                    //find first space
                    int spaceIndex = line.IndexOf(' ');
                    string tagName;
                    if (spaceIndex == -1)
                    {
                        spaceIndex = line.Length;
                        if (!insideMessageGroup)
                        {
                            continue;
                        }
                        tagName = line;
                        line = "";
                    }
                    else
                    {
                        tagName = line.Substring(0, spaceIndex);
                        line = line.Substring(spaceIndex + 1);
                    }
                    int number = -1;
                    //if it starts with a number, it's a legacy text replacement

                    if (!insideMessageGroup && IntUtil.TryParse(tagName, out number) == true)
                    {
                        tagName = "id";
                    }
                    else
                    {
                        number = -1;
                        line = line.TrimStart();
                        spaceIndex = line.IndexOf(' ');
                        if (spaceIndex != -1)
                        {
                            string numberString = line.Substring(0, spaceIndex);
                            if (IntUtil.TryParse(numberString, out number))
                            {
                                line = line.Substring(spaceIndex + 1);
                            }
                            else
                            {
                                number = -1;
                            }
                        }
                    }

                    line = StringExportImport.UnescapeText(line);

                    bool handled = true;

                    if (!insideMessageGroup)
                    {
                        string tagNameLower = tagName.ToLowerInvariant();
                        switch (tagNameLower)
                        {
                            case "f":
                            case "func":
                            case "function":
                                string nextFunctionName = line.Trim();
                                Function function = null;
                                function = ainFile.GetFunction(number);
                                if (function == null)
                                {
                                    function = ainFile.GetFunction(nextFunctionName);
                                }

                                SetCurrentFunction(function);
                                if (function == null)
                                {
                                    //ERROR: Missing function!
                                    if (Debugger.IsAttached)
                                    {
                                        Debugger.Break();
                                    }
                                    handled = false;
                                }
                                break;
                            case "string":
                            case "str":
                            case "s":
                                if (number != -1)
                                {
                                    stringDictionary.Set(number, line);
                                    insideMessageGroup = false;
                                }
                                else
                                {
                                    handled = false;
                                }
                                break;
                            case "msg":
                            case "message":
                            case "m":
                                if (number != -1)
                                {
                                    messageDictionary.Set(number, line);
                                    insideMessageGroup = false;
                                }
                                else
                                {
                                    handled = false;
                                }
                                break;
                            case "mg":
                            case "messagegroup":
                            case "msggroup":
                                if (number != -1)
                                {
                                    insideMessageGroup = true;
                                    currentMessageGroup = messageGroupDictionary.GetOrAddNew(number);
                                    if (currentMessageGroup.Count != 0)
                                    {
                                        if (Debugger.IsAttached)
                                        {
                                            Debugger.Break();
                                        }
                                    }
                                }
                                else
                                {
                                    handled = false;
                                }
                                break;

                            case "i":
                            case "id":
                                if (!insideMessageGroup && number != -1)
                                {
                                    numberedStrings.Set(number, line);
                                }
                                else
                                {
                                    handled = false;
                                }
                                break;
                            default:
                                handled = false;
                                break;
                        }
                    }
                    else
                    {
                        handled = false;
                    }
                    if (handled && !insideMessageGroup)
                    {
                        currentMessageGroup = null;
                    }
                    if (!handled && insideMessageGroup && currentMessageGroup != null)
                    {
                        line = StringExportImport.UnescapeText(originalLine);
                        currentMessageGroup.Add(line);
                    }
                }
            }

            private void SetCurrentFunction(Function function)
            {
                if (function != null)
                {
                    currentFunctionName = function.Name;
                    stringDictionary = stringEntries.GetOrAddNew(currentFunctionName);
                    messageDictionary = messageEntries.GetOrAddNew(currentFunctionName);
                    messageGroupDictionary = messageGroupEntries.GetOrAddNew(currentFunctionName);
                    insideMessageGroup = false;
                    currentMessageGroup = null;
                }
                else
                {
                    currentFunctionName = "";
                    stringDictionary = null;
                    messageDictionary = null;
                    messageGroupDictionary = null;
                    insideMessageGroup = false;
                    currentMessageGroup = null;
                }
            }

            private void IncludeJamFile(string filenameToInclude, Encoding encoding)
            {
                var textReader = new StreamReader(filenameToInclude, encoding);
                MyTextReader tr = new MyTextReader(textReader, filenameToInclude);

                StringBuilder sb = new StringBuilder();
                StringWriter sw = new StringWriter(sb);

                while (true)
                {
                    var token = tr.ReadToken();
                    if (token == null)
                    {
                        break;
                    }
                    if (token.Value == "#include")
                    {
                        token = tr.ReadToken();
                        if (token == null)
                        {
                            break;
                        }
                        string fileName = token.Value;
                        if (!Path.IsPathRooted(fileName))
                        {
                            fileName = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(tr.FileName), fileName));
                        }
                        var newTextReader = new StreamReader(fileName, EncodingDetector.DetectEncoding(fileName));
                        tr.IncludeTextReader(newTextReader, fileName);
                    }
                    else if (token.Value == "FUNC")
                    {
                        token = tr.ReadToken();
                        if (token == null)
                        {
                            break;
                        }
                        var func = ainFile.GetFunction(token.Value);
                        if (func != null)
                        {
                            if (currentFunctionName != func.Name)
                            {
                                string codeText = sb.ToString();
                                if (codeText != "")
                                {
                                    if (CodePatches.ContainsKey(currentFunctionName))
                                    {
                                        CodePatches[currentFunctionName] += Environment.NewLine + codeText;
                                    }
                                    else
                                    {
                                        CodePatches[currentFunctionName] = codeText;
                                    }
                                }
                                SetCurrentFunction(func);
                                sb.Length = 0;
                            }
                            else
                            {

                            }
                        }
                        else
                        {
                            //ERROR!  Missing function!
                        }
                    }
                    else
                    {
                        sw.WriteLine(token.ToString());
                    }
                }

                {
                    string codeText = sb.ToString();
                    sb.Length = 0;
                    if (codeText != "")
                    {
                        var func = ainFile.GetFunction(currentFunctionName);
                        if (func == null)
                        {
                            func = ainFile.GetFunction("NULL");
                        }
                        if (func != null)
                        {
                            if (CodePatches.ContainsKey(func.Name))
                            {
                                CodePatches[func.Name] += Environment.NewLine + codeText;
                            }
                            else
                            {
                                CodePatches[func.Name] = codeText;
                            }
                        }
                    }
                }
            }

            private bool ExportAndMerge()
            {
                string newDisassembledCode = null;

                if ((this.CodePatches2 != null && this.CodePatches2.Length > 0) || numberedStrings.Count > 0)
                {
                    this.ainFile = ainFile.Clone();
                }

                if (this.CodePatches2 != null && this.CodePatches2.Length > 0)
                {
                    //this.ainFile = ainFile.Clone();
                    var compiler = new Compiler.Compiler(ainFile);
                    if (Program.ConsoleMode)
                    {
                        compiler.Error += new EventHandler<ErrorEventArgs>(compiler_Error);
                    }

                    byte[] codeBytes;
                    compiler.CompileCode(this.CodePatches2.ToString(), out codeBytes, out newDisassembledCode, true, this.CodePatches);
                    if (newDisassembledCode == null || compiler.Errors.Count > 0)
                    {
                        if (Program.ConsoleMode)
                        {

                        }
                        else
                        {
                            var errorsListForm = new ErrorsListForm();
                            errorsListForm.SetErrorList(compiler.Errors);
                            errorsListForm.Show();
                        }
                        return false;
                    }

                    //foreach (var func in functionsToOutput)
                    //{
                    //    this.CodePatches[func.Name] = "";
                    //}
                }

                currentFunctionName = "";
                stringDictionary = null;
                messageDictionary = null;
                stringNumber = 0;
                this.messageGroupFinder.MessageNumber = 0;
                this.messageGroupFinder.MessageGroupNumber = 0;

                //var ainFile = this.ainFile;
                if (numberedStrings.Count > 0)
                {
                    //this.ainFile = ainFile.Clone();
                    var stringExportImport = new StringExportImport(ainFile);
                    int firstMessage = stringExportImport.GetFirstMessageIdNumber();
                    int lastMessage = firstMessage + ainFile.Messages.Count;
                    int firstString = stringExportImport.GetFirstStringIdNumber();
                    int lastString = firstString + ainFile.Strings.Count;

                    foreach (var pair in numberedStrings)
                    {
                        int number = pair.Key;
                        string message = pair.Value;
                        if (number >= firstMessage && number < lastMessage)
                        {
                            ainFile.Messages[number - firstMessage] = message;
                        }
                        else if (number >= firstString && number < lastString)
                        {
                            ainFile.Strings[number - firstString] = message;
                        }
                    }
                }

                using (TemporaryFile tempFile = new TemporaryFile("jam", true))
                {
                    var saver = new ExplorerForm.SaveProjectInBackground();
                    var options = new CodeDisplayOptions();
                    options.AnnotateWithDecompiledCode = false;
                    options.MergeDuplicateStrings = true;
                    options.MergeDuplicateMessages = true;
                    var writer = new AssemblerProjectWriter(ainFile, options);
                    writer.BeforeWritingInstruction += new EventHandler<InstructionInfoEventArgs>(writer_BeforeWritingInstruction);
                    writer.BeforeWritingString += new EventHandler<InstructionInfoEventArgs>(writer_BeforeWritingString);
                    saver.SaveAsProject(writer, tempFile.FileName, true);
                    if (newDisassembledCode != null)
                    {
                        using (var fs = new FileStream(tempFile.FileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        {
                            using (var sw = new StreamWriter(fs, Extensions.TextEncoding))
                            {
                                sw.WriteLine();
                                sw.WriteLine(newDisassembledCode);
                                sw.Flush();
                                fs.Flush();
                                sw.Close();
                                fs.Close();
                            }
                        }
                    }

                    var builder = new ExplorerForm.BuildProjectInBackground();
                    builder.ForceUniqueMessages = true;
                    builder.Run(tempFile.FileName, this.outputFileName, true);
                }
                return true;
            }

            void compiler_Error(object sender, ErrorEventArgs e)
            {
                Console.Error.WriteLine(e.GetException().Message);
                Environment.ExitCode = 1;
            }

            int stringNumber = 0;

            void writer_BeforeWritingInstruction(object sender, InstructionInfoEventArgs e)
            {
                var instructionInfo = e.InstructionInfo;
                int currentAddress = instructionInfo.CurrentAddress;
                var instruction = instructionInfo.instruction;
                int word1 = instructionInfo.word1;
                if (InsideFunction)
                {
                    if (!hasOutputFunction)
                    {
                        if (this.CodePatches.ContainsKey(currentFunctionName))
                        {
                            var codePatch = this.CodePatches[currentFunctionName];
                            e.WriteLine(codePatch);
                        }
                        hasOutputFunction = true;
                    }

                    if (instruction == Instruction.FUNC || instruction == Instruction.ENDFUNC || instruction == Instruction.EOF)
                    {
                        //will emit the ENDFUNC or next FUNC instruction
                        InsideFunction = false;
                        e.StopEmittingCode = false;
                    }
                    else
                    {
                        e.Handled = true;
                        e.StopEmittingCode = true;
                        return;
                    }
                }

                if (instruction == Instruction.FUNC)
                {
                    int functionNumber = instructionInfo.word1;
                    if (this.wordWrapOptions != null)
                    {
                        currentWordWrapProfile = this.wordWrapOptions.WordWrapOptionsProfiles[0];
                        previousWordWrapProfile = currentWordWrapProfile;
                        this.wordWrapper.CurrentProfile = currentWordWrapProfile;
                    }

                    var function = ainFile.GetFunction(functionNumber);
                    if (function != null)
                    {
                        stringNumber = 0;
                        messageGroupFinder.MessageNumber = 0;
                        messageGroupFinder.MessageGroupNumber = 0;
                        currentFunctionName = function.Name;

                        stringDictionary = stringEntries.GetOrNull(currentFunctionName);
                        messageDictionary = messageEntries.GetOrNull(currentFunctionName);
                        messageGroupDictionary = messageGroupEntries.GetOrNull(currentFunctionName);
                        messageGroupFinder.SetFunction(ainFile.GetFunction(currentFunctionName));

                        if (messageGroupDictionary != null && messageGroupDictionary.Count > 1)
                        {
                            function.Crc = 0;
                        }

                        if (this.CodePatches.ContainsKey(currentFunctionName))
                        {
                            //var codePatch = this.CodePatches[currentFunctionName];
                            //e.WriteLine(codePatch);
                            e.Handled = false;
                            e.StopEmittingCode = false;
                            //will emit the FUNC instruction, but then not the rest
                            this.InsideFunction = true;
                            this.hasOutputFunction = false;
                            function.Crc = 0;
                        }
                    }
                }
                else
                {
                    if ((instruction == Instruction.MSG || instruction == Instruction.CALLFUNC) && messageGroupFinder.IsMessageOrR(currentAddress))
                    {
                        var messageGroup = messageGroupFinder.GetMessageGroup(currentAddress);
                        if (messageGroup.LastMessageNumber >= messageGroup.FirstMessageNumber)
                        {
                            List<string> newMessageGroup = null;

                            if (messageGroupDictionary != null)
                            {
                                newMessageGroup = messageGroupDictionary.GetOrNull(messageGroup.MessageGroupNumber);
                            }

                            if (newMessageGroup == null)
                            {
                                newMessageGroup = new List<string>();
                                for (int i = 0; i < messageGroup.MessageNumbers.Count; i++)
                                {
                                    int msgNumber = messageGroup.MessageNumbers[i];
                                    int messageIndexNumber = messageGroup.MessageIndexNumbers[i];
                                    string message = null;
                                    if (msgNumber == -2)
                                    {
                                        message = "@";
                                    }
                                    else if (msgNumber == -1)
                                    {
                                        message = "";
                                    }
                                    else
                                    {
                                        message = null;
                                        if (this.messageDictionary != null)
                                        {
                                            message = this.messageDictionary.GetOrNull(msgNumber);
                                        }
                                        if (message == null && this.numberedStrings.ContainsKey(messageIndexNumber + firstStringNumberMsg))
                                        {
                                            message = this.numberedStrings[messageIndexNumber + firstStringNumberMsg];
                                        }
                                        if (message == null)
                                        {
                                            message = messageGroup.MessageText[i] ?? "";
                                        }
                                        if (message == "")
                                        {
                                            message = "@''";
                                        }
                                    }
                                    newMessageGroup.Add(message);
                                }
                            }

                            if (this.WordWrap)
                            {
                                if (this.DoNotWrapIfMatchesOriginal && newMessageGroup.SequenceEqual(messageGroup.MessageText))
                                {

                                }
                                else
                                {
                                    newMessageGroup = wordWrapper.WordWrapLines(newMessageGroup).ToList();
                                }
                            }

                            for (int i = 0; i < newMessageGroup.Count; i++)
                            {
                                string newMessage = newMessageGroup[i];
                                if (newMessage.StartsWith("\0@CODE"))
                                {
                                    //NEW: fake message that is actually a code patch
                                    e.WriteLine(newMessage.Substring(6));
                                }
                                else if (newMessage == "@")
                                {
                                    e.WriteLine(" " + aFunctionText);
                                }
                                else
                                {
                                    if (newMessage != "")
                                    {
                                        if (newMessage != "@''")
                                        {
                                            e.WriteLine("\tMSG " + AssemblerProjectWriter.EscapeAndQuoteMessage(newMessage));
                                        }
                                        else
                                        {
                                            e.WriteLine("\tMSG ''");
                                        }
                                    }
                                    if (i + 1 >= newMessageGroup.Count)
                                    {
                                        if (messageGroup.EndsWithNonA && messageGroup.EndsWithNewline)
                                        {
                                            e.WriteLine(" " + rFunctionText);
                                        }
                                    }
                                    else
                                    {
                                        if (newMessageGroup[i + 1] != "@")
                                        {
                                            e.WriteLine(" " + rFunctionText);
                                        }
                                    }
                                }
                            }
                            e.NextAddress = messageGroup.EndAddress;
                            e.Handled = true;
                        }
                    }
                    else if (instruction == Instruction.CALLFUNC)
                    {
                        if (this.FunctionIndexToProfiles.ContainsKey(instructionInfo.word1))
                        {
                            CheckForWordWrapProfile(instructionInfo);
                        }
                    }
                }
                //string instructions are handled by writer_BeforeWritingString
            }

            private void CheckForWordWrapProfile(InstructionInfo ins)
            {
                int functionIndex = ins.word1;
                int nextAddress = ins.nextAddress;
                var profiles = this.FunctionIndexToProfiles.GetOrNull(functionIndex);
                if (profiles != null)
                {
                    foreach (var profile in profiles)
                    {
                        foreach (var binary in profile.GetTriggerBinaries(ainFile))
                        {
                            int startAddress = nextAddress - binary.Length;
                            bool match = CompareBinary(ainFile.Code, startAddress, binary);
                            if (match)
                            {
                                if (profile.ProfileName.Equals("PREVIOUS PROFILE", StringComparison.OrdinalIgnoreCase))
                                {
                                    currentWordWrapProfile = previousWordWrapProfile;
                                    this.wordWrapper.CurrentProfile = currentWordWrapProfile;
                                }
                                else
                                {
                                    currentWordWrapProfile = profile;
                                    this.wordWrapper.CurrentProfile = profile;
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            Dictionary<byte[], byte[]> BinaryToMask = new Dictionary<byte[], byte[]>();

            private bool CompareBinary(byte[] code, int startAddress, byte[] binary)
            {
                if (startAddress < 0)
                {
                    return false;
                }
                if (startAddress + binary.Length >= code.Length)
                {
                    return false;
                }

                byte[] mask = GetMask(binary);
                for (int i = 0; i < binary.Length; i++)
                {
                    if (mask[i] != 0)
                    {
                        if (code[i + startAddress] != binary[i])
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            private byte[] GetMask(byte[] binary)
            {
                var mask = BinaryToMask.GetOrNull(binary);
                if (mask == null)
                {
                    mask = new byte[binary.Length];
                    for (int i = 0; i < mask.Length; i++)
                    {
                        mask[i] = 0xFF;
                    }
                    int deadbeef;
                    unchecked
                    {
                        deadbeef = (int)0xDEADBEEF;
                    }
                    for (int i = 0; i <= binary.Length - 4; i++)
                    {
                        int word = BitConverter.ToInt32(binary, i);
                        if (word == deadbeef)
                        {
                            mask[i] = 0;
                            mask[i + 1] = 0;
                            mask[i + 2] = 0;
                            mask[i + 3] = 0;
                        }
                    }
                    BinaryToMask[binary] = mask;
                }
                return mask;
            }

            void writer_BeforeWritingString(object sender, InstructionInfoEventArgs e)
            {
                var instructionInfo = e.InstructionInfo;
                var instruction = instructionInfo.instruction;
                int indexOfStringArgument = instruction.IndexOfStringArgument();
                if (indexOfStringArgument >= 0)
                {
                    string originalString = ainFile.GetString(instructionInfo.words[indexOfStringArgument]);
                    string replacementString = null;
                    if (stringDictionary != null)
                    {
                        replacementString = stringDictionary.GetOrNull(stringNumber);
                    }
                    if (replacementString != null && originalString != replacementString)
                    {
                        e.Text = replacementString;
                    }
                    stringNumber++;
                }
                else if (indexOfStringArgument == -1 && instruction == Instruction.STRSWITCH)
                {
                    string originalString = e.Text;
                    string replacementString = null;
                    if (stringDictionary != null)
                    {
                        replacementString = stringDictionary.GetOrNull(stringNumber);
                    }
                    if (replacementString != null && originalString != replacementString)
                    {
                        e.Text = replacementString;
                    }
                    stringNumber++;
                }
            }
        }

        public void SaveTextToMultipleFiles(string outputFileName, Encoding encoding)
        {
            this.IncludeMessages = true;
            this.IncludeStrings = false;
            string baseDirectoryName = Path.GetDirectoryName(outputFileName);
            string textDirectory = Path.Combine(baseDirectoryName, "text");
            string rootFileName = Path.Combine(textDirectory, "root.txt");
            Directory.CreateDirectory(textDirectory);
            using (var rootFileStream = new FileStream(rootFileName, FileMode.Create, FileAccess.Write))
            {
                var rootTextWriter = new StreamWriter(rootFileStream, encoding);

                var enumerator = new FunctionEnumerator(ainFile);
                var results = enumerator.GetFilesAndFunctions();
                foreach (var result1 in results)
                {
                    string jafFileName = result1.name;
                    foreach (var functionNode in result1.children)
                    {
                        var function = ainFile.GetFunction(functionNode.name);
                        var text = GetTextFromFunction(function);

                        if (text.Count > 2)
                        {
                            string functionName = AssemblerProjectWriter.SanitizeVariableName(function.Name);
                            if (functionName.StartsWith("\""))
                            {

                            }
                            else
                            {
                                string includeFileName = Path.Combine(jafFileName, functionName + ".txt");
                                string fileName = Path.Combine(textDirectory, includeFileName);
                                string outputDirectory = Path.GetDirectoryName(fileName);
                                Directory.CreateDirectory(outputDirectory);
                                File.WriteAllLines(fileName, text.ToArray(), encoding);
                                rootTextWriter.WriteLine("#include \"" + includeFileName + "\"");
                            }
                        }
                    }
                }
                rootTextWriter.Flush();
                rootFileStream.Flush();
                rootTextWriter.Close();
                rootFileStream.Close();
            }

            using (var textWriter2 = new StreamWriter(outputFileName, false, encoding))
            {
                textWriter2.WriteLine("#include \"text/root.txt\"");
                textWriter2.Flush();
                textWriter2.Close();
            }
        }

        public void SetSubstitutionTable(Dictionary<string, string> dictionary)
        {
            this.replacementStringsForAnnotations = dictionary;
        }

        public static bool CompareBytes(byte[] code, int startAddress, byte[] binary)
        {
            if (startAddress < 0)
            {
                return false;
            }
            if (startAddress + binary.Length >= code.Length)
            {
                return false;
            }

            for (int i = 0; i < binary.Length; i++)
            {
                if (code[i + startAddress] != binary[i])
                {
                    return false;
                }
            }
            return true;
        }

        public static Dictionary<string, List<string>> GetExportedTextForFunctions(AinFile ainFile)
        {
            Dictionary<string, List<string>> output = new Dictionary<string, List<string>>();

            var exporter = new TextImportExport(ainFile);
            exporter.IncludeStrings = false;
            exporter.CombineMessages = true;
            exporter.IncludeMessages = true;
            var annotations = AnnotationSettings.GetAnnotationSettings(ainFile);
            exporter.SetAnnotations(annotations);
            foreach (var function in ainFile.Functions)
            {
                if (ainFile.CodeSearchCache.MessagesCache.GetUses(function).Count > 0)
                {
                    var list = exporter.GetTextFromFunction(function);
                    output[function.Name] = list;
                }
            }
            return output;
        }

        public static Dictionary<string, List<MessageGroup>> FindMessageGroups(AinFile ainFile)
        {
            Dictionary<string, List<MessageGroup>> messageGroupsDictionary = new Dictionary<string, List<MessageGroup>>();
            MessageGroupFinder messageGroupFinder = new MessageGroupFinder(ainFile);
            foreach (var function in ainFile.Functions)
            {
                messageGroupFinder.MessageGroupNumber = 0;
                messageGroupFinder.MessageNumber = 0;
                messageGroupFinder.SetFunction(function);

                int address = function.Address;
                int endAddress = function.GetEndAddress();
                while (address < endAddress)
                {
                    var ins = ainFile.Peek(address);
                    int nextAddress = ins.nextAddress;
                    if (messageGroupFinder.IsMessageOrR(address))
                    {
                        var messageGroup = messageGroupFinder.GetMessageGroup(address);
                        if (messageGroup.LastMessageNumber >= messageGroup.FirstMessageNumber)
                        {
                            nextAddress = messageGroup.EndAddress;
                        }
                        int messageGroupNumber = messageGroup.MessageGroupNumber;

                        //List<string> output = new List<string>();
                        //for (int i = 0; i < messageGroup.MessageText.Count; i++)
                        //{
                        //    if (messageGroup.MessageNumbers[i] == -2)
                        //    {
                        //        output.Add("@");
                        //    }
                        //    else if (messageGroup.MessageNumbers[i] == -1)
                        //    {
                        //        output.Add("");
                        //    }
                        //    else
                        //    {
                        //        string messageText = messageGroup.MessageText[i];
                        //        if (messageText == "")
                        //        {
                        //            messageText = "@''";
                        //        }
                        //        output.Add(messageText);
                        //    }
                        //}
                        var list = messageGroupsDictionary.GetOrAddNew(function.Name);
                        list.Add(messageGroup);
                    }
                    address = nextAddress;
                }
            }
            return messageGroupsDictionary;
        }

        public void SetAnnotations(AnnotationSettings annotationSettings)
        {
            if (annotationSettings.UseEnumerationType)
            {
                this.AnnotateEnumerationType = annotationSettings.GetEnumerationType(ainFile);
            }
            if (annotationSettings.UseVariables)
            {
                this.SetAnnotationFunctionParameters(annotationSettings.GetVariables(ainFile));
            }
            this.SetSubstitutionTable(annotationSettings.GetSubstitutionTable());
        }

        private static void IncludeScriptFile(string fileName, Dictionary<string, List<string>> fileContents)
        {
            Encoding encoding = EncodingDetector.DetectEncoding(fileName);
            List<string> lines = File.ReadAllLines(fileName, encoding).ToList();

            bool sawFunction = false;
            foreach (var line in lines)
            {
                string lineTrim = line.Trim();
                if (lineTrim.StartsWith("FUNCTION ", StringComparison.OrdinalIgnoreCase))
                {
                    if (!sawFunction)
                    {
                        sawFunction = true;
                        string functionName = lineTrim.Substring("FUNCTION ".Length);
                        fileContents[functionName] = lines;
                    }
                }
                else if (lineTrim.StartsWith("#include ", StringComparison.OrdinalIgnoreCase))
                {
                    string includedFileName = lineTrim.Substring("#include".Length);
                    if (includedFileName.StartsWith("\"") && includedFileName.EndsWith("\""))
                    {
                        includedFileName = includedFileName.Substring(1, includedFileName.Length - 2);
                    }

                    IncludeScriptFile(includedFileName, fileContents);
                }
            }
        }

        public static Dictionary<string, List<string>> ReadScriptFiles(string rootPath)
        {
            Dictionary<string, List<string>> fileContents = new Dictionary<string, List<string>>();
            IncludeScriptFile(rootPath, fileContents);
            return fileContents;
        }
    }
}
