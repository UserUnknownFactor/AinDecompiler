using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.ComponentModel;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AinDecompiler
{
#if false
    Dungeons and Dolls:
    枠(1, 0, 0); - normal dialog
    
	PUSH 1
	PUSH 0
	PUSH 0
	CALLFUNC 枠
    40 characters wide
    3 lines tall

    枠(2, 0, 0); - manual text

	PUSH 2
	PUSH 0
	PUSH 0
	CALLFUNC 枠

    54/55 characters wide
    28 lines tall


    Diabanchou:
    functions must exist:
    void Ｅ枠(int nWin, int nEffect, int nWait)
    void 枠(int nWin)

    PUSH 1 or PUSH 2 or PUSH 3 or PUSH 4
	CALLFUNC 枠

    38 characters wide
    3 lines high

	PUSH 5 or PUSH 6
	CALLFUNC 枠	//Frame	nWin

    46 characters wide
    3 lines high

    Sengoku Rance:
    
    void 字枠(int nNo) - used for game clear report or looking at enemy commanders - disable word wrap
    void 全画面枠２(int nBlendRate, int nEffectNo, int nSpeed) - used by game intro, disable word wrap

    void 台詞枠(int nNameNo, int nEffectNo, int nSpeed) - normal text
    void 思考枠(int nNameNo, int nEffectNo, int nSpeed)
    void ト書き枠(int nEffectNo, int nSpeed)

    43/44? characters, 3 lines

    void 文字サイズ(int nSize) - Mouri's weird text - word wrapping for this or not?


    Rance 6:
    No variation at all
    44 text width
    3 lines

    Tsumashibori:

    57 width
    3 height

    reduced: 
    void 顔(int ＣＧ番号, int n名札)
    CALLFUNC 顔

    43 width
    3 height


    Hoken:

    57/59 width
    3 height

    reduced:

    41/43 width
    3 height


    Rance02:
    normal:
    functions: ??? (no way to pick without actually analyzing code)

    54 width
    5 height

    expanded:
    70 width
    3 height

    Widenyo:

    Haruurare:




    Daiteikoku:

    Rance Quest:

    Oyako Rankan:

    Drapeko:

    Rance 01:


#endif

    [Serializable]
    public class WordWrapOptionsOld
    {
        [Bindable(true), Browsable(true), Description("Name of the New Line function, so it can detect new lines.  Usually R.")]
        public string NextLineFunctionName { get; set; }
        [Bindable(true), Browsable(true), Description("Name of the Next Message function, so it knows when the message ends.  Usually A.")]
        public string NextMessageFunctionName { get; set; }
        [Bindable(true), Browsable(true), Description("The set of 'Face Display' functions that makes the margins narrower." + "\r\n" +
            "If the game doesn't make text narrower when a face is displayed, use the same margin width for both cases.")]
        public string ReduceMarginFunctionName { get; set; }
        [Bindable(true), Browsable(true), Description("The ASM code that will be output when the word wrapper needs to split a line of text." + "\r\n" +
            "Usually CALLFUNC R, but sometimes it might need more arguments.")]
        public string NextLineFunctionCode { get; set; }
        [Bindable(true), Browsable(true), Description("The ASM code that will be output when the word wrapper needs to split a series of lines across multiple messages." + "\r\n" +
            "Usually CALLFUNC A, but sometimes it might need more arguments.")]
        public string NextMessageFunctionCode { get; set; }
        [Bindable(true), Browsable(true), Description("The width of a line of text in halfwidth characters.  Lines longer than this are split." + "\r\n" +
            "Fullwidth characters count as two halfwidth characters.")]
        public int MaxCharactersPerLineNormal { get; set; }
        [Bindable(true), Browsable(true), Description("The narrower width used when the game displays a face in the left side of the text box." + "\r\n" +
            "If the game doesn't do this, use the same value as the regular margin.")]
        public int MaxCharactersPerLineReduced { get; set; }
        [Bindable(true), Browsable(true), Description("The height of the text box in lines.  If more rows are displayed, it splits it into multiple messages.")]
        public int MaxLinesPerMessage { get; set; }
        [Bindable(true), Browsable(true), Description("Whether or not we are using a variable-width font (not a fixed width font)." + "\r\n" +
            "Number of characters per line will be measured in units of half a kanji character.")]
        public bool UseVariableWidthFont { get; set; }
        [Bindable(true), Browsable(true), Description("If the line wraps to a new line, remove the line break for that line." + "\r\n" +
            "This prevents short fragments of messages from taking up their own lines.")]
        public bool RemoveLineBreaksIfWordWrapping { get; set; }
        [Bindable(true), Browsable(true), Description("If the line begins with a '「' or fullwidth space, add fullwidth spaces to the next line if the line wraps.")]
        public bool MaintainIndentation { get; set; }
        [Bindable(true), Browsable(true), Description("Name of the font to use as an example of variable-width text")]
        public string TemplateFontName { get; set; }
        [Bindable(true), Browsable(true), Description("Size of the font to use as an example of variable-width text")]
        public float TemplateFontSize { get; set; }
        [Bindable(true), Browsable(true), Description("Weight of the font to use as an example of variable-width text")]
        public bool TemplateFontBold { get; set; }
        [Bindable(true), Browsable(true), Description("Width of a Kanji character for determining margin size in pixels")]
        public float TemplateKanjiWidth { get; set; }

        public WordWrapOptionsOld()
        {
            this.NextLineFunctionName = "R";
            this.NextLineFunctionCode = " CALLFUNC R ";
            this.ReduceMarginFunctionName = "顔";
            this.NextMessageFunctionName = "A";
            this.NextMessageFunctionCode = " CALLFUNC A ";
            this.MaxCharactersPerLineNormal = 57;
            this.MaxCharactersPerLineReduced = 42;
            this.MaxLinesPerMessage = 3;
            this.UseVariableWidthFont = false;
            this.RemoveLineBreaksIfWordWrapping = true;
            this.MaintainIndentation = true;
            this.TemplateFontName = "Arial Unicode MS";
            this.TemplateFontSize = 22.0f;
            this.TemplateFontBold = false;
            this.TemplateKanjiWidth = 28;
        }

        public void AssignFrom(WordWrapOptionsOld assignFromThis)
        {
            ReflectionUtil.MemberwiseAssign(this, assignFromThis);
        }

        public void SaveToRegistry(AinFile ainFile)
        {
            string baseName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename);
            string pathName = "WordWrapOptions\\" + baseName;
            SaveToRegistry(pathName);
        }

        public void SaveToRegistry(string registryPathName)
        {
            RegistryUtility.SaveObject(registryPathName, this);
        }

        public static WordWrapOptionsOld GetWordWrapOptions(AinFile ainFile)
        {
            var wordWrapOptions = new WordWrapOptionsOld();
            string baseName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename);
            string pathName = "WordWrapOptions\\" + baseName;
            if (RegistryUtility.PathExists(pathName))
            {
                RegistryUtility.GetObject(pathName, wordWrapOptions);
            }
            else
            {
                //generate default code for next line and next message commands
                wordWrapOptions.NextLineFunctionCode = GetGeneratedCode(wordWrapOptions.NextLineFunctionName, ainFile);
                wordWrapOptions.NextMessageFunctionCode = GetGeneratedCode(wordWrapOptions.NextMessageFunctionName, ainFile);

                //Face portrait detection for a few games

                //hoken
                MatchFunction(ainFile, wordWrapOptions, 57, 41, "顔表示", DataType.Int);

                //tsumashibori
                MatchFunction(ainFile, wordWrapOptions, 57, 42, "顔", DataType.Int, DataType.Int);

                //rancequest
                if (MatchFunction(ainFile, wordWrapOptions, 57, 42, "セリフ２", DataType.Int, DataType.String) &&
                    MatchFunction(ainFile, wordWrapOptions, 57, 42, "セリフ", DataType.Int, DataType.String) &&
                    MatchFunction(ainFile, wordWrapOptions, 57, 42, "ト書き") &&
                    MatchFunction(ainFile, wordWrapOptions, 57, 42, "思考", DataType.Int, DataType.String))
                {
                    wordWrapOptions.ReduceMarginFunctionName = new string[] { "セリフ", "セリフ２", "ト書き", "思考" }.Join(Environment.NewLine);
                    wordWrapOptions.UseVariableWidthFont = true;
                }

                //oyakoranken
                if (MatchFunction(ainFile, wordWrapOptions, -1, -1, "◎台詞", DataType.String, DataType.String))
                {
                    wordWrapOptions.UseVariableWidthFont = true;
                }
            }
            return wordWrapOptions;
        }

        private static bool MatchFunction(AinFile ainFile, WordWrapOptionsOld wordWrapOptions, int maxCharactersPerLineNormal, int maxCharactersPerLineReduced, string functionName, params DataType[] dataTypes)
        {
            var function = ainFile.GetFunction(functionName);
            if (function != null && function.ParameterCount == dataTypes.Length && function.Parameters.Take(function.ParameterCount).Select(a => a.DataType).SequenceEqual(dataTypes))
            {
                //if we specified values for the max lengths, set them
                if (maxCharactersPerLineNormal > 0)
                {
                    wordWrapOptions.MaxCharactersPerLineNormal = maxCharactersPerLineNormal;
                }
                if (maxCharactersPerLineReduced > 0)
                {
                    wordWrapOptions.MaxCharactersPerLineReduced = maxCharactersPerLineReduced;
                }

                //set the function name
                wordWrapOptions.ReduceMarginFunctionName = functionName;

                //indicate success
                return true;
            }
            return false;
        }

        private static string GetGeneratedCode(string functionName, AinFile ainFile)
        {
            StringBuilder sb = new StringBuilder();
            if (ainFile.FunctionNameToIndex.ContainsKey(functionName))
            {
                Function function = ainFile.Functions[ainFile.FunctionNameToIndex[functionName]];
                if (function.ParameterCount > 0)
                {
                    for (int i = 0; i < function.ParameterCount; i++)
                    {
                        var arg = function.Parameters[i];
                        if (arg.DataType == DataType.Int)
                        {
                            sb.AppendLine("\t" + "PUSH 0");
                        }
                        else if (arg.DataType == DataType.String)
                        {
                            sb.AppendLine("\t" + "S_PUSH \"\"");
                        }
                        else if (arg.DataType == DataType.Float)
                        {
                            sb.AppendLine("\t" + "F_PUSH 0.0");
                        }
                        else
                        {
                            sb.AppendLine("\t" + "PUSH 0");
                        }
                    }
                }
            }
            sb.Append("\t" + "CALLFUNC " + functionName);
            return sb.ToString();
        }
    }

    [Serializable]
    public class WordWrapOptionsProfile : ICloneable
    {
        [Bindable(true), Browsable(true), Description("Name of the profile.")]
        public string ProfileName { get; set; }

        [Bindable(true), Browsable(true), Description("The width of a line of text in halfwidth characters.  Lines longer than this are split." + "\r\n" +
            "Fullwidth characters count as two halfwidth characters.")]
        public int MaxCharactersPerLine { get; set; }
        [Bindable(true), Browsable(true), Description("The height of the text box in lines.  If more rows are displayed, it splits it into multiple messages.")]
        public int MaxLinesPerMessage { get; set; }

        [Bindable(true), Browsable(true), Description("The code that would trigger these word wrap settings.")]
        public string[] TriggerCodes
        {
            get
            {
                _dirty = true;
                return _triggerCodes;
            }
            set
            {
                _dirty = true;
                _triggerCodes = value;
            }
        }
        bool _dirty = true;
        string[] _triggerCodes;

        [Bindable(true), Browsable(true), Description("The binary form of the code that would trigger these word wrap settings.")]
        private byte[][] TriggerBinaries;

        public byte[][] GetTriggerBinaries(AinFile ainFile)
        {
            if (!_dirty)
            {
                return TriggerBinaries;
            }
            SetTriggerCodes(GetTriggerCodes(), ainFile);
            _dirty = false;
            return this.TriggerBinaries;
        }

        public WordWrapOptionsProfile()
        {
            this.MaxCharactersPerLine = 65535;
            this.MaxLinesPerMessage = 65535;

            TriggerCodes = new string[0];
            TriggerBinaries = new byte[0][];
        }

        public string GetTriggerCodes()
        {
            if (this.TriggerCodes == null) return "";
            return TriggerCodes.Join(Environment.NewLine + Environment.NewLine);
        }

        public bool SetTriggerCodes(AinFile ainFile, params string[] triggerCodes)
        {
            return SetTriggerCodes(triggerCodes.Join(Environment.NewLine + Environment.NewLine), ainFile);
        }

        public bool SetTriggerCodes(string triggerCodeText, AinFile ainFile)
        {
            string[] incomingTriggerCodes = triggerCodeText.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            //New content is same as old content?
            if (this.TriggerCodes != null && incomingTriggerCodes.SequenceEqual(this.TriggerCodes))
            {
                if (TriggerBinaries.Length != 0)
                {
                    return true;
                }
            }

            for (int i = 0; i < incomingTriggerCodes.Length; i++)
            {
                string triggerCode = incomingTriggerCodes[i].Trim();
                if (!triggerCode.EndsWith(";"))
                {
                    byte[] code = AssemblerProjectReader.CompileCode(triggerCode, ainFile);
                    if (code == null)
                    {
                        if (triggerCode.Split('\n').Length == 1)
                        {
                            triggerCode += ";";
                            incomingTriggerCodes[i] = triggerCode;
                        }
                    }
                }
            }

            //New content is blank?
            if (incomingTriggerCodes.Length == 0)
            {
                this.TriggerCodes = new string[0];
                this.TriggerBinaries = new byte[0][];
                return true;
            }

            //New content is same as old content?
            if (this.TriggerCodes != null && incomingTriggerCodes.SequenceEqual(this.TriggerCodes))
            {
                if (TriggerBinaries.Length != 0)
                {
                    return true;
                }
            }

            List<string> newTriggerCodes = new List<string>();
            List<byte[]> newTriggerBinaries = new List<byte[]>();
            var ainCopy = ainFile.Clone();

            for (int i = 0; i < incomingTriggerCodes.Length; i++)
            {
                string triggerCode = incomingTriggerCodes[i];

                byte[] binary;
                string disassembledCodeText;

                binary = AssemblerProjectReader.CompileCode(triggerCode, ainFile);
                if (binary == null)
                {
                    var compiler = new Compiler.Compiler(ainCopy, 0);
                    compiler.CompileCode(triggerCode, out binary, out disassembledCodeText, false, null);
                }
                if (binary != null)
                {
                    var binary2 = FilterCode(binary);
                    if (binary2.Length > 0)
                    {
                        newTriggerCodes.Add(triggerCode);
                        newTriggerBinaries.Add(binary2);
                    }
                    else
                    {

                    }
                }
                else
                {
                    return false;
                }
            }

            this.TriggerCodes = newTriggerCodes.ToArray();
            this.TriggerBinaries = newTriggerBinaries.ToArray();
            return true;
        }

        private static byte[] FilterCode(byte[] binary)
        {
            //removes FUNC and ENDFUNC, and RETURN before ENDFUNC instructions
            MemoryStream binary2stream = new MemoryStream(binary.Length);
            int address = 0;
            while (address < binary.Length)
            {
                var ins = Decompiler.Peek(binary, address);
                if (ins.instruction != Instruction.FUNC && ins.instruction != Instruction.ENDFUNC)
                {
                    bool filtered = false;
                    if (ins.instruction == Instruction.RETURN)
                    {
                        var nextIns = Decompiler.Peek(binary, ins.nextAddress);
                        if (nextIns.instruction == Instruction.ENDFUNC)
                        {
                            filtered = true;
                        }
                    }
                    if (!filtered)
                    {
                        int args = ins.totalArguments;
                        binary2stream.Write(binary, address, 2 + args * 4);
                    }
                }
                address = ins.nextAddress;
            }
            var binary2 = binary2stream.ToArray();
            return binary2;
        }

        public WordWrapOptionsProfile Clone()
        {
            var clone = this.MemberwiseClone() as WordWrapOptionsProfile;
            if (clone.TriggerCodes != null)
            {
                clone.TriggerCodes = clone.TriggerCodes.Clone() as string[];
            }
            if (clone.TriggerBinaries != null)
            {
                clone.TriggerBinaries = clone.TriggerBinaries.Select(a => (a != null ? a.Clone() : null) as byte[]).ToArray();
            }
            return clone;
        }

        #region ICloneable Members

        object ICloneable.Clone()
        {
            return this.Clone();
        }

        #endregion
    }

    [Serializable]
    public class WordWrapOptions : ICloneable
    {
        /// <summary>
        /// Whether or not we are using a variable-width font (not a fixed width font).
        /// <br/> Number of characters per line will be measured in units of half a kanji character.
        /// </summary>
        [Bindable(true), Browsable(true), Description("Whether or not we are using a variable-width font (not a fixed width font)." + "\r\n" +
            "Number of characters per line will be measured in units of half a kanji character.")]
        public bool UseVariableWidthFont { get; set; }
        /// <summary>
        /// If the line wraps to a new line, remove the line break for that line.
        /// <br/>This prevents short fragments of messages from taking up their own lines.
        /// </summary>
        [Bindable(true), Browsable(true), Description("If the line wraps to a new line, remove the line break for that line." + "\r\n" +
            "This prevents short fragments of messages from taking up their own lines.")]
        public bool RemoveLineBreaksIfWordWrapping { get; set; }
        /// <summary>
        /// If the line begins with a '「' or fullwidth space, add fullwidth spaces to the next line if the line wraps.
        /// </summary>
        [Bindable(true), Browsable(true), Description("If the line begins with a '「' or fullwidth space, add fullwidth spaces to the next line if the line wraps.")]
        public bool MaintainIndentation { get; set; }
        /// <summary>
        /// Name of the font to use as an example of variable-width text
        /// </summary>
        [Bindable(true), Browsable(true), Description("Name of the font to use as an example of variable-width text")]
        public string TemplateFontName { get; set; }
        /// <summary>
        /// Size of the font to use as an example of variable-width text
        /// </summary>
        [Bindable(true), Browsable(true), Description("Size of the font to use as an example of variable-width text")]
        public float TemplateFontSize { get; set; }
        /// <summary>
        /// Weight of the font to use as an example of variable-width text
        /// </summary>
        [Bindable(true), Browsable(true), Description("Weight of the font to use as an example of variable-width text")]
        public bool TemplateFontBold { get; set; }
        /// <summary>
        /// Width of a Kanji character for determining margin size in pixels
        /// </summary>
        [Bindable(true), Browsable(true), Description("Width of a Kanji character for determining margin size in pixels")]
        public float TemplateKanjiWidth { get; set; }
        /// <summary>
        /// If true, completely disable word wrapping for this game.
        /// </summary>
        [Bindable(true), Browsable(true), Description("If true, completely disable word wrapping for this game.")]
        public bool Disabled { get; set; }
        /// <summary>
        /// If true, Angle braces are used for formatting text and should be ignored for word wrapping.
        /// </summary>
        [Bindable(true), Browsable(true), Description("If true, Angle braces are used for formatting text and should be ignored for word wrapping.")]
        public bool IgnoreAngleBraces { get; set; }

        public List<WordWrapOptionsProfile> WordWrapOptionsProfiles { get; set; }

        public WordWrapOptions()
        {
            this.WordWrapOptionsProfiles = new List<WordWrapOptionsProfile>();
            this.WordWrapOptionsProfiles.Add(new WordWrapOptionsProfile() { ProfileName = "Default", MaxCharactersPerLine = 57, MaxLinesPerMessage = 3 });
            this.WordWrapOptionsProfiles.Add(new WordWrapOptionsProfile() { ProfileName = "Normal", MaxCharactersPerLine = 57, MaxLinesPerMessage = 3, TriggerCodes = new string[] { "CALLFUNC A" } });
            this.WordWrapOptionsProfiles.Add(new WordWrapOptionsProfile() { ProfileName = "Reduced", MaxCharactersPerLine = 42, MaxLinesPerMessage = 3 });
            this.WordWrapOptionsProfiles.Add(new WordWrapOptionsProfile() { ProfileName = "Disable Wrapping", MaxCharactersPerLine = 65535, MaxLinesPerMessage = 65535 });

            this.UseVariableWidthFont = false;
            this.RemoveLineBreaksIfWordWrapping = true;
            this.MaintainIndentation = true;
            this.TemplateFontName = "Arial Unicode MS";
            this.TemplateFontSize = 22.0f;
            this.TemplateFontBold = false;
            this.TemplateKanjiWidth = 28;
            this.IgnoreAngleBraces = false;
        }

        public void AssignFrom(WordWrapOptions assignFromThis)
        {
            ReflectionUtil.MemberwiseAssign(this, assignFromThis);
            this.WordWrapOptionsProfiles = this.WordWrapOptionsProfiles.Select(p => p.Clone()).ToList();
        }

        public void SaveToRegistry(AinFile ainFile)
        {
            string baseName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename);
            string pathName = "WordWrapOptions2\\" + baseName;
            SaveToRegistry(pathName);
        }

        public void SaveToRegistry(string registryPathName)
        {
            RegistryUtility.SaveObject(registryPathName, this);
            RegistryUtility.DeletePath(registryPathName + "\\profiles");

            for (int i = 0; i < this.WordWrapOptionsProfiles.Count; i++)
            {
                string profilePath = registryPathName + "\\profiles\\" + i.ToString();
                var profile = this.WordWrapOptionsProfiles[i];
                RegistryUtility.SaveObject(profilePath, profile);
            }
        }

        public static WordWrapOptions LoadWordWrapOptions(string registryPathName)
        {
            if (RegistryUtility.PathExists(registryPathName))
            {
                var wordWrapOptions = new WordWrapOptions();
                RegistryUtility.GetObject(registryPathName, wordWrapOptions);
                int i = 0;
                if (wordWrapOptions.WordWrapOptionsProfiles == null)
                {
                    wordWrapOptions.WordWrapOptionsProfiles = new List<WordWrapOptionsProfile>();
                }
                wordWrapOptions.WordWrapOptionsProfiles.Clear();
                while (true)
                {
                    string profilePath = registryPathName + "\\profiles\\" + i.ToString();
                    if (RegistryUtility.PathExists(profilePath))
                    {
                        var profile = new WordWrapOptionsProfile();
                        RegistryUtility.GetObject(profilePath, profile);
                        wordWrapOptions.WordWrapOptionsProfiles.Add(profile);
                    }
                    else
                    {
                        break;
                    }
                }
                return wordWrapOptions;
            }
            return null;
        }

        public static WordWrapOptions LoadWordWrapOptions(AinFile ainFile)
        {
            string baseName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename);
            string registryPathName = "WordWrapOptions2\\" + baseName;
            return LoadWordWrapOptions(registryPathName);
        }

        public WordWrapOptions Clone()
        {
            var clone = new WordWrapOptions();
            clone.AssignFrom(this);
            return clone;
        }

        #region ICloneable Members

        object ICloneable.Clone()
        {
            return Clone();
        }

        #endregion

        /// <summary>
        /// Loads either word wrap options saved in the registry, or options hardcoded into the program for a limited list of games, if neither are available, returns default settings with word wrapping disabled.
        /// </summary>
        /// <param name="ainFile"></param>
        /// <returns></returns>
        public static WordWrapOptions GetWordWrapOptions(AinFile ainFile)
        {
            var wordWrapOptions = WordWrapOptions.LoadWordWrapOptions(ainFile);
            if (wordWrapOptions != null)
            {
                return wordWrapOptions;
            }
            wordWrapOptions = DefaultWordWrapOptions.DetectGame(ainFile);
            if (wordWrapOptions != null)
            {
                return wordWrapOptions;
            }
            wordWrapOptions = new WordWrapOptions();
            wordWrapOptions.Disabled = true;
            return wordWrapOptions;
        }
    }

}
