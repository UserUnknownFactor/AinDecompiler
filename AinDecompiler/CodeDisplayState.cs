using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using ScintillaNET;

namespace AinDecompiler
{
    public class ExplorerHistoryItem
    {
        public string FunctionName;
        public string FileNameToDisassemble;
        public string ClassName;
        public string FileNameToEdit;
        public int StartAddress = -1;
        public int EndAddress = -1;
        public ScintillaState ScintillaState;
        public ScintillaState ScintillaState2;
        public ScintillaState ScintillaState3;
        public Document document;

        public bool NavigationLocked = false;

        public override string ToString()
        {
            return FunctionName ?? FileNameToDisassemble ?? ClassName ?? "";
        }
    }

    public class CodeDisplayState
    {
        public AinFile ainFile;
        public Function Function = null;
        public string FunctionName = null;
        public int FunctionAddress = -1;
        public int EndAddress = -1;
        public string DecompiledCodeText;
        public string DisassembledCodeText;
        public string CombinedViewCodeText;
        public Expression FunctionExpression;
        public ExpressionMap ExpressionMap;
        public CodeDisplayOptions CodeDisplayOptions;
        public bool UseTry;
        public bool Revisiting;
        public string FileNameToDisassemble;
        public string ClassName;
        public string FileNameToEdit;
        public Document Document;
    }

    public class CodeDisplayOptions
    {
        //options for display
        public bool DeclareVariablesAtBeginning = false;
        public bool RemoveReturns = true;
        public bool ShowCasting = false;
        public bool ShowGlobalPrefix = true;
        public bool ShowThis = true;
        public bool Verbose = false;
        public bool DisplayDefaultValuesForMethods = true;
        public bool UseEnumerations = true;

        //options for exporting
        public bool ForceOriginalStringOrder = false;
        public bool MergeDuplicateStrings = false;
        public bool MergeDuplicateMessages = false;
        public bool RemoveOrphanFunctions = false;
        public bool AnnotateWithDecompiledCode = false;
        public bool AddEndfunc = false;

        //options for binary disassembly
        public bool DisassembleDisplayBytes = false;
    }
}
