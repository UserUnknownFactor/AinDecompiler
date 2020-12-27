using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace AinDecompiler.Compiler
{
    public partial class Compiler
    {
        //TODO:
        //add filenames to output file
        //add filenames to error messages
        //cache syntax trees
        //move global array initializer function 0 to the correct location  (before class array initializers)
        //fix bugs

        public AinFile ainFile;

        internal ListAndDictionary<Function> Functions;
        internal ListAndDictionary<Global> Globals;
        internal StringListAndDictionary GlobalGroupNames;
        internal ListAndDictionary<Variable> Constants;
        internal ListAndDictionary<Struct> Structs;
        internal ListAndDictionary<FunctionType> Functypes;
        internal ListAndDictionary<FunctionType> Delegates;
        internal ListAndDictionary<HllLibrary> Libraries;
        internal StringListAndDictionary Strings;
        internal StringListAndDictionary Messages;

        internal Dictionary<string, IVariable> Symbols = new Dictionary<string, IVariable>();
        internal Dictionary<IVariable, InitialValue> InitialValues = new Dictionary<IVariable, InitialValue>();
        internal Dictionary<Function, TokenExpression> FunctionDefinitionTokens = new Dictionary<Function, TokenExpression>();
        internal Dictionary<Function, TokenExpression> FunctionDeclarationTokens = new Dictionary<Function, TokenExpression>();
        internal Dictionary<Function, Expression> FunctionDefinitions = new Dictionary<Function, Expression>();

        internal Dictionary<IVariable, TokenExpression> ArraySizeExpressions = new Dictionary<IVariable, TokenExpression>();

        /// <summary>
        /// Creates a blank compiler, ready to build a project file
        /// </summary>
        public Compiler()
            : this(new AinFile(), -1)
        {
            this.Functions.Get("NULL");
            this.Strings.Add("");
            this.Messages.Add("");

            this.AinVersion = 5;
            this.IsAi2File = false;
            this.UsesMsg1 = false;
            this.KeyCode = 0;
            this.gameVersion = 0;
            this.UseSimplifiedCode = false;
            this.TargetVMVersion = 227;
            this.ainFile.TargetVMVersion = 227;
        }

        /// <summary>
        /// Creates a compiler attached to a specified AinFile, this will modify the ainfile as the compiler is run.
        /// </summary>
        /// <param name="ainFile">The ain file to modify</param>
        /// <param name="dummy">not used, just to distinguish this overload from another</param>
        public Compiler(AinFile ainFile, int dummy)
        {
            this.ainFile = ainFile;
            this.Functions = new ListAndDictionary<Function>(Symbols, ainFile, ainFile.Functions, ainFile.FunctionNameToIndex);
            this.Globals = new ListAndDictionary<Global>(Symbols, ainFile, ainFile.Globals, ainFile.GlobalNameToIndex);
            this.GlobalGroupNames = new StringListAndDictionary(ainFile.GlobalGroupNames);
            this.Constants = new ListAndDictionary<Variable>(Symbols, ainFile);
            this.Structs = new ListAndDictionary<Struct>(Symbols, ainFile, ainFile.Structs, ainFile.StructNameToIndex);
            this.Functypes = new ListAndDictionary<FunctionType>(Symbols, ainFile, ainFile.FunctionTypes, ainFile.FunctionNameToIndex);
            this.Delegates = new ListAndDictionary<FunctionType>(Symbols, ainFile, ainFile.Delegates);
            this.Libraries = new ListAndDictionary<HllLibrary>(Symbols, ainFile, ainFile.Libraries);
            this.Strings = new StringListAndDictionary(ainFile.Strings);
            this.Messages = new StringListAndDictionary(ainFile.Messages);
            this.InitialValues.Add(AinFile.BuiltInFunctionsInverse["array.Numof"].Parameters[0], new InitialValue(1));
            this.InitialValues.Add(AinFile.BuiltInFunctionsInverse["array.Sort"].Parameters[0], new InitialValue(0));
            this.InitialValues.Add(AinFile.BuiltInFunctionsInverse["array.Alloc"].Parameters[0], new InitialValue(1));
            this.InitialValues.Add(AinFile.BuiltInFunctionsInverse["array.Realloc"].Parameters[0], new InitialValue(1));
            this.InitialValues.Add(AinFile.BuiltInFunctionsInverse["array.Find"].Parameters[3], new InitialValue(0));
            this.InitialValues.Add(AinFile.BuiltInFunctionsInverse["float.String"].Parameters[1], new InitialValue(-1));
            this.InitialValues.Add(AinFile.BuiltInFunctionsInverse["string.Erase"].Parameters[1], new InitialValue(1));
            var trueConstant = this.Constants.Get("true");
            var falseConstant = this.Constants.Get("false");

            this.InitialValues[trueConstant] = new InitialValue(true);
            this.InitialValues[falseConstant] = new InitialValue(false);

            this.AinVersion = ainFile.Version;
            this.IsAi2File = ainFile.IsAi2File;
            this.UsesMsg1 = ainFile.UsesMsg1;
            this.KeyCode = ainFile.KeyCode;
            this.gameVersion = ainFile.GameVersion;
            this.UseSimplifiedCode = ainFile.UseSimplifiedCode;
        }

        /// <summary>
        /// Creates a compiler based on a specified AinFile, using its initial variables and other symbols.  Does not modify the original.
        /// </summary>
        /// <param name="ainFileIn">The ain file to read symbols from</param>
        public Compiler(AinFile ainFileIn)
            : this(ainFileIn.Clone(), -1)
        {
            //foreach (var function in ainFile.Functions)
            //{
            //    this.Symbols.Add(function.Name, function);
            //}
            //foreach (var global in ainFile.Globals)
            //{
            //    this.Symbols.Add(global.Name, global);
            //}
        }

        int dummyFunctionNumber = 0;

        public void CompileCodeInPlace(string[] fileNames)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var fileName in fileNames)
            {
                sb.AppendLine("#include " + AssemblerProjectWriter.EscapeAndQuoteString(Path.GetFullPath(fileName)));
            }
            CompileCodeInPlace(sb.ToString());
        }

        public void CompileCodeInPlace(string codeText)
        {
            this.stopwatch.Start();

            var lastDefaultAinFile = Expression.defaultAinFile;
            try
            {
                Expression.defaultAinFile = this.ainFile;

                if (worker != null)
                {
                    worker.ReportProgress(0, "Loading files...");
                }

                List<TokenExpression> tokens = new List<TokenExpression>();
                IncludeString(codeText, tokens);

                ainFile.Version = this.AinVersion;
                ainFile.TargetVMVersion = this.TargetVMVersion;
                ainFile.KeyCode = this.KeyCode;
                ainFile.IsAi2File = this.IsAi2File;
                ainFile.UsesMsg1 = this.UsesMsg1;
                ainFile.GameVersion = this.gameVersion;
                ainFile.UseSimplifiedCode = this.UseSimplifiedCode;
                if (ainFile.Version >= 6)
                {
                    this.Messages.DoNotCombine = true;
                }

                if (worker != null)
                {
                    worker.ReportProgress(20, "First pass, finding functions and classes and other symbols...");
                }

                var firstPass = new FirstPass();
                if (this.Error != null)
                {
                    firstPass.Error += this.Error;
                }

                var result = firstPass.CompileFirstPass(tokens);
                if (firstPass.Errors.Count > 0)
                {
                    this.Errors.AddRange(firstPass.Errors);
                    return;
                }
                var symbolsCreated = CreateSymbols(result, false);

                GetInitialValues(result);

                //DefineZeroFunction();

                if (worker != null)
                {
                    worker.ReportProgress(40, "Second pass, parsing the code...");
                }

                if (!CompileSecondPass())
                {
                    return;
                }

                if (worker != null)
                {
                    worker.ReportProgress(60, "Compiling the code...");
                }

                GenerateExpressions();

                if (worker != null)
                {
                    worker.ReportProgress(80, "Generating binary code...");
                }

                var codeGenerator = new CodeGenerator(ainFile);
                codeGenerator.Code.Write(ainFile.Code, 0, ainFile.Code.Length);
                codeGenerator.Address = ainFile.Code.Length;
                var functionsToOutput = symbolsCreated.OfType<Function>().ToArray();

                foreach (var function in functionsToOutput)
                {
                    var expression = this.FunctionDefinitions.GetOrNull(function);
                    function.Address = codeGenerator.Address + 6;
                    function.Crc = 0;
                    codeGenerator.GenerateCodeForFunction(expression);
                }

                ainFile.Code = codeGenerator.Code.ToArray();
                ainFile.MAIN = Functions.Contains("main") ? (Functions.Get("main").Index) : -1;
                ainFile.MSGF = Functions.Contains("message") ? (Functions.Get("message").Index) : -1;
                ainFile.OJMP = Functions.Contains("onjump") ? (Functions.Get("onjump").Index) : -1;
                RemoveFunctypes(ainFile);
                FixStructs(ainFile);
                ainFile.ComputeFunctionHashes();
                SortGlobalInitialValues(ainFile);
                SortSwitchCases(ainFile);

                if (worker != null)
                {
                    worker.ReportProgress(100, "Saving AIN file...");
                }
            }
            finally
            {
                Expression.defaultAinFile = lastDefaultAinFile;
            }
        }


        public void CompileCode(string codeText, out byte[] codeBytes, out string disassembledCode, bool topLevel, Dictionary<string, string> codePatches)
        {
            int oldStructCount = ainFile.Structs.Count;
            int oldFunctionCount = ainFile.Functions.Count;
            int oldSwitchCount = ainFile.Switches.Count;
            int oldFunctypeCount = ainFile.FunctionTypes.Count;
            int oldDelegateCount = ainFile.Delegates.Count;
            int oldGlobalCount = ainFile.Globals.Count;
            int oldLibraryCount = ainFile.Libraries.Count;

            codeBytes = null;
            disassembledCode = null;

            var lastDefaultAinFile = Expression.defaultAinFile;
            try
            {
                if (!topLevel)
                {
                    string dummyFunctionName = "DUMMY" + dummyFunctionNumber.ToString("0000");
                    codeText = "void " + dummyFunctionName + "()" + Environment.NewLine + "{" + Environment.NewLine + codeText + Environment.NewLine + "}" + Environment.NewLine;
                    dummyFunctionNumber++;
                }
                List<TokenExpression> tokens = new List<TokenExpression>();
                IncludeString(codeText, tokens);

                ainFile.Version = this.AinVersion;
                ainFile.TargetVMVersion = this.TargetVMVersion;
                ainFile.IsAi2File = this.IsAi2File;
                ainFile.UsesMsg1 = this.UsesMsg1;
                ainFile.KeyCode = this.KeyCode;
                ainFile.GameVersion = this.gameVersion;
                ainFile.UseSimplifiedCode = this.UseSimplifiedCode;
                if (ainFile.Version >= 6)
                {
                    this.Messages.DoNotCombine = true;
                }

                var firstPass = new FirstPass();
                if (this.Error != null)
                {
                    firstPass.Error += this.Error;
                }

                var result = firstPass.CompileFirstPass(tokens);
                if (firstPass.Errors.Count > 0)
                {
                    this.Errors.AddRange(firstPass.Errors);
                    return;
                }

                bool createAdditionalFunctions = topLevel;
                var symbolsCreated = CreateSymbols(result, createAdditionalFunctions);
                var functionsToOutput = symbolsCreated.OfType<Function>().ToArray();
                var structsCreated = symbolsCreated.OfType<Struct>().ToArray();
                var librariesToOutput = symbolsCreated.OfType<HllLibrary>().ToArray();
                if (ainFile.Libraries.Count > oldLibraryCount + librariesToOutput.Length)
                {
                    //fix missing libraries
                    librariesToOutput = librariesToOutput.Concat(ainFile.Libraries.Skip(oldLibraryCount)).ToArray();
                }

                GetInitialValues(result);
                if (!CompileSecondPass())
                {
                    return;
                }
                GenerateExpressions();

                var codeGenerator = new CodeGenerator(ainFile);
                foreach (var function in functionsToOutput)
                {
                    var expression = this.FunctionDefinitions.GetOrNull(function);
                    function.Crc = 0;
                    function.Address = codeGenerator.Address + 6;
                    codeGenerator.GenerateCodeForFunction(expression);
                }

                var writer = new AssemblerProjectWriter(ainFile);
                codeBytes = codeGenerator.Code.ToArray();

                StringWriter sw = new StringWriter();
                List<Function> functionsToOutput2 = new List<Function>();

                foreach (var function in functionsToOutput)
                {
                    bool handled = false;
                    if (codePatches != null)
                    {
                        if (function.Index < oldFunctionCount)
                        {
                            disassembledCode = writer.GetDisassembledCode(function, codeBytes);
                            codePatches[function.Name] = disassembledCode;
                            //writer.GetDisassembledCode(codeBytes, new Function[] { function }, ainFile.Structs.Count, ainFile.Delegates.Count, ainFile.FunctionTypes.Count, ainFile.Globals.Count);
                            handled = true;
                        }
                    }

                    if (!handled)
                    {
                        functionsToOutput2.Add(function);
                    }
                }

                writer.GetDisassembledCode(sw, codeBytes, functionsToOutput, functionsToOutput2.ToArray(), structsCreated, librariesToOutput, oldDelegateCount, oldFunctypeCount, oldGlobalCount);
                disassembledCode = sw.ToString();

                ainFile.MAIN = Functions.Contains("main") ? (Functions.Get("main").Index) : -1;
                ainFile.MSGF = Functions.Contains("message") ? (Functions.Get("message").Index) : -1;
                ainFile.OJMP = Functions.Contains("onjump") ? (Functions.Get("onjump").Index) : -1;
                RemoveFunctypes(ainFile);
                FixStructs(ainFile);
                ainFile.ComputeFunctionHashes();
                SortGlobalInitialValues(ainFile);
                SortSwitchCases(ainFile);

                if (!topLevel && Errors.Count > 0)
                {
                    disassembledCode = null;
                    codeBytes = null;
                }
            }
            finally
            {
                Expression.defaultAinFile = lastDefaultAinFile;

                if (!topLevel)
                {
                    this.FunctionDeclarationTokens.Clear();
                    this.FunctionDefinitionTokens.Clear();
                    this.FunctionDefinitions.Clear();
                }
            }

        }

        public void Compile(string projectFilename)
        {
            this.stopwatch.Start();

            var lastDefaultAinFile = Expression.defaultAinFile;
            try
            {
                Expression.defaultAinFile = this.ainFile;

                if (worker != null)
                {
                    worker.ReportProgress(0, "Loading files...");
                }

                List<TokenExpression> tokens = new List<TokenExpression>();
                PreprocessInclude(projectFilename, tokens);
                if (this.TargetVMVersion >= 430)
                {
                    this.UseSimplifiedCode = true;
                }

                ainFile.Version = this.AinVersion;
                ainFile.TargetVMVersion = this.TargetVMVersion;
                ainFile.KeyCode = this.KeyCode;
                ainFile.IsAi2File = this.IsAi2File;
                ainFile.UsesMsg1 = this.UsesMsg1;
                ainFile.GameVersion = this.gameVersion;
                ainFile.UseSimplifiedCode = this.UseSimplifiedCode;
                if (ainFile.Version >= 6)
                {
                    this.Messages.DoNotCombine = true;
                }

                if (worker != null)
                {
                    worker.ReportProgress(20, "First pass, finding functions and classes and other symbols...");
                }

                var firstPass = new FirstPass();
                if (this.Error != null)
                {
                    firstPass.Error += this.Error;
                }

                var result = firstPass.CompileFirstPass(tokens);
                if (firstPass.Errors.Count > 0)
                {
                    return;
                }
                var functionsToOutput = CreateSymbols(result);

                GetInitialValues(result);

                DefineZeroFunction();

                if (worker != null)
                {
                    worker.ReportProgress(40, "Second pass, parsing the code...");
                }

                if (!CompileSecondPass())
                {
                    return;
                }

                if (worker != null)
                {
                    worker.ReportProgress(60, "Compiling the code...");
                }

                GenerateExpressions();

                if (worker != null)
                {
                    worker.ReportProgress(80, "Generating binary code...");
                }


                //List<object> FunctionsToOutput = GetFunctionsToOutput(tokens);

                string prefixLowercase = Path.GetFullPath(mainSourcePrefix).ToLowerInvariant();
                if (!prefixLowercase.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    prefixLowercase += Path.DirectorySeparatorChar;
                }

                var codeGenerator = new CodeGenerator(ainFile);
                foreach (var node in functionsToOutput)
                {
                    var function = node as Function;
                    if (function != null)
                    {
                        var expression = this.FunctionDefinitions.GetOrNull(function);
                        function.Address = codeGenerator.Address + 6;
                        codeGenerator.GenerateCodeForFunction(expression);
                    }
                    string filename = node as string;
                    if (filename != null)
                    {
                        filename = Path.GetFullPath(filename);
                        if (filename.StartsWith(prefixLowercase, StringComparison.OrdinalIgnoreCase))
                        {
                            filename = filename.Substring(prefixLowercase.Length);
                        }
                        else
                        {

                        }
                        int fileNameNumber = ainFile.Filenames.Count;
                        ainFile.Filenames.Add(filename);
                        codeGenerator.WriteInstruction(Instruction.EOF, fileNameNumber);
                    }
                }

                ainFile.Code = codeGenerator.Code.ToArray();
                string outputFileName = Path.Combine(Path.GetDirectoryName(projectFilename), this.codeName);
                ainFile.MAIN = Functions.Contains("main") ? (Functions.Get("main").Index) : -1;
                ainFile.MSGF = Functions.Contains("message") ? (Functions.Get("message").Index) : -1;
                ainFile.OJMP = Functions.Contains("onjump") ? (Functions.Get("onjump").Index) : -1;
                RemoveFunctypes(ainFile);
                FixStructs(ainFile);
                ainFile.ComputeFunctionHashes();
                SortGlobalInitialValues(ainFile);
                SortSwitchCases(ainFile);

                if (worker != null)
                {
                    worker.ReportProgress(100, "Saving AIN file...");
                }
                ainFile.WriteAndEncryptAinFile(outputFileName);
                ainFile.OriginalFilename = outputFileName;
            }
            finally
            {
                Expression.defaultAinFile = lastDefaultAinFile;
            }
        }

        private string GetFilename(string fileName)
        {
            fileName = Path.GetFullPath(fileName);
            string prefix = "";
            if (!String.IsNullOrEmpty(this.mainSourcePrefix))
            {
                prefix = Path.GetFullPath(this.mainSourcePrefix);
            }
            if (!prefix.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                prefix += Path.DirectorySeparatorChar;
            }
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName.Substring(prefix.Length);
            }
            return fileName;
        }


        //private List<object> GetFunctionsToOutput(List<TokenExpression> tokens)
        //{
        //    List<object> FunctionsToOutput = new List<object>();
        //    bool hasOutputZero = false;
        //    foreach (var function in ainFile.Functions)
        //    {
        //        if (function.Name == "NULL")
        //        {

        //        }
        //        else if (function.Name.EndsWith("@2"))
        //        {
        //            if (!hasOutputZero)
        //            {
        //                FunctionsToOutput.Add(ainFile.GetFunction("0"));
        //                hasOutputZero = true;
        //            }
        //            FunctionsToOutput.Add(function);
        //        }
        //        else if (function.Name == "0")
        //        {

        //        }
        //        else
        //        {
        //            FunctionsToOutput.Add(function);
        //        }
        //    }
        //    FunctionsToOutput.Add(ainFile.GetFunction("NULL"));

        //    return FunctionsToOutput;
        //}

        public void Test()
        {
            //List<TokenExpression> tokens = new List<TokenExpression>();
            ////string expression = "a - -obj.method()()[4] - -3;";
            ////string expression = "- -a - -obj.method()()[4] - -3;";
            ////string expression = "- -a - -obj.method()(-9*-8 + 5)[4 == 4 + 5 == 4] - -3;";
            ////string expression = "- -a - -obj.method()(-9*-8 + 5)[4 == 4 + 5 == 4] - -3;";
            //IncludeString(expression, tokens);
            //this.reader = new TokenReader(tokens);
            //var exp = ReadExpression();

            string projectFilename = @"<insert filename here>";

            Compile(projectFilename);

            string outputJamFileName = Path.Combine(Path.GetDirectoryName(projectFilename), "output\\main.jam");
            //string outputJamFileName = Path.Combine(Path.GetDirectoryName(projectFilename), "..\\DISASSEMBLED2\\main.jam");

            //string filename = @"<insert filename here>";
            //List<TokenExpression> tokens = new List<TokenExpression>();
            //IncludeFile(filename, tokens);

            //string libraryPath = @"<insert filename here>";
            //LoadHllFiles(libraryPath);

            ainFile.LoadAlternativeNames();
            ainFile.LoadMetadata();
            AssemblerProjectWriter writer = new AssemblerProjectWriter(ainFile);
            writer.Options.ShowGlobalPrefix = false;
            writer.SaveAsProject(outputJamFileName, false);

            //todo: fix _date_ macro, needs spaces instead of leading zeroes
        }

        #region Finalizing AIN File

        private static void SortSwitchCases(AinFile ainFile)
        {
            foreach (var switchBlock in ainFile.Switches)
            {
                switchBlock.SwitchCases.Sort();
            }
        }

        private static void SortGlobalInitialValues(AinFile ainFile)
        {
            var values = ainFile.GlobalInitialValues.OrderBy(v => v.GlobalIndex).ToArray();
            ainFile.GlobalInitialValues.Clear();
            ainFile.GlobalInitialValues.AddRange(values);
        }

        private static void FixStructs(AinFile ainFile)
        {
            foreach (var structInfo in ainFile.Structs)
            {
                structInfo.NumberOfMembers = structInfo.Members.Count;
                var constructor = ainFile.GetFunction(structInfo.Name + "@0");
                var destructor = ainFile.GetFunction(structInfo.Name + "@1");
                if (constructor != null)
                {
                    structInfo.Constructor = constructor.Index;
                }
                if (destructor != null)
                {
                    structInfo.Destructor = destructor.Index;
                }
            }
        }

        private static void RemoveFunctypes(IEnumerable<IFunction> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                RemoveFunctypes(item);
            }
        }

        private static void RemoveFunctypes(IEnumerable<IVariable> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item is IFunction)
                {
                    RemoveFunctypes((IFunction)item);
                }
                else
                {
                    RemoveFunctypes(item);
                }
            }
        }

        private static void RemoveFunctypes(IFunction item)
        {
            if (item == null) return;
            RemoveFunctypes((IVariable)item);
            RemoveFunctypes(item.Parameters);
        }

        private static void RemoveFunctypes(IVariable item)
        {
            if (item == null) return;
            if (item.DataType.IsFuncTypeOrDelegate())
            {
                item.StructType = -1;
            }
            if (item.DataType == DataType.Void && (item is Variable || item is Global))
            {
                item.StructType = 0;
            }
        }

        public static void RemoveFunctypes(AinFile ainFile)
        {
            RemoveFunctypes(ainFile.Functions.Cast<IFunction>());
            RemoveFunctypes(ainFile.FunctionTypes.Cast<IFunction>());
            RemoveFunctypes(ainFile.Delegates.Cast<IFunction>());
            RemoveFunctypes(ainFile.Globals.Cast<IVariable>());
            RemoveFunctypes(ainFile.Libraries.Cast<IFunction>());
            RemoveFunctypes(ainFile.Structs.Cast<IFunction>());

            //ainFile.functionTypesKnown = false;
        }
        #endregion

        private void GetInitialValues(List<TokenExpression> result)
        {
            var secondPass = new SecondPass(this);
            if (this.Error != null)
            {
                secondPass.Error += this.Error;
            }

            secondPass.GetInitialValues(result);
        }

        private bool CompileSecondPass()
        {
            var secondPass = new SecondPass(this);
            if (this.Error != null)
            {
                secondPass.Error += this.Error;
            }
            secondPass.CompileSecondPass();
            if (secondPass.Errors.Count == 0)
            {
                return true;
            }
            else
            {
                this.Errors.AddRange(secondPass.Errors);
                return false;
            }
        }

        public List<string> Errors = new List<string>();
        string lastErrorFilename = null;
        string lastErrorFunctionName = null;
        public event EventHandler<ErrorEventArgs> Error;
        public MyBackgroundWorker worker = null;

        TinyStopwatch stopwatch = new TinyStopwatch();

        private void RaiseError2(string errorMessage, TokenExpression tokenExpression)
        {
            errorMessage += ": ";
            if (tokenExpression != null)
            {
                errorMessage += tokenExpression.ToStringReal();
            }
            RaiseError(errorMessage, tokenExpression);
        }

        private void RaiseError(string errorMessage, TokenExpression tokenExpression)
        {
            int row = -1, column = -1;
            string fileName = null;
            string functionName = null;
            if (currentFunction != null)
            {
                functionName = currentFunction.Name;
            }
            if (tokenExpression != null)
            {
                row = tokenExpression.Row;
                column = tokenExpression.Column;
                fileName = tokenExpression.fileName;
                errorMessage = "Line " + (row + 1).ToString() + ", column " + (column + 1).ToString() + ": " + errorMessage;
            }
            else
            {
                fileName = lastErrorFilename;
            }
            if (Error != null)
            {
                if (fileName != lastErrorFilename)
                {
                    string errorMessage1 = "In file: " + fileName;
                    if (fileName != null)
                    {
                        Error(this, new ErrorEventArgs(new Exception(errorMessage1)));
                    }
                }
                lastErrorFilename = fileName;
                if (functionName != lastErrorFunctionName)
                {
                    string errorMessage1 = "In function: " + functionName;
                    if (functionName != null)
                    {
                        Error(this, new ErrorEventArgs(new Exception(errorMessage1)));
                    }
                }
                lastErrorFunctionName = functionName;
                Error(this, new ErrorEventArgs(new Exception(errorMessage)));
            }
            Errors.Add(errorMessage);


            //if (0.GetHashCode() == 0)
            //{
            //    throw new NotImplementedException();
            //}
        }

        public static string ComplieCode(AinFile ainFile, string codeText, bool topLevel, out byte[] codeBytes)
        {
            string disassembledCode;
            var compiler = new Compiler(ainFile);
            compiler.CompileCode(codeText, out codeBytes, out disassembledCode, topLevel, null);
            return disassembledCode;
        }
    }
}
