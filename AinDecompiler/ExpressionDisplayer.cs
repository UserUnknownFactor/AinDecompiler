﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    public class ExpressionDisplayer
    {
        public AinFile ainFile;
        public static OperatorTable OperatorTable = new OperatorTable();
        public static HashSet<Instruction> OperatorsNotToHandle = new HashSet<Instruction>(new Instruction[] { Instruction.IfElse, Instruction.RefInitializer, Instruction.RefIntInitializer, Instruction.RefAssignment, Instruction.RefIntAssignment });

        private MyIndentedTextWriter tw;
        private MemoryStream ms;
        public ExpressionMap expressionMap = null;
        private bool DeclareVariables = false;
        private bool BanStatements = false;

        private Expression switchCaseExpression = null;

        bool ShowThis { get { return Options.ShowThis; } set { Options.ShowThis = value; } }
        bool ShowGlobalPrefix { get { return Options.ShowGlobalPrefix; } set { Options.ShowGlobalPrefix = value; } }
        bool ShowCasting { get { return Options.ShowCasting; } set { Options.ShowCasting = value; } }
        bool RemoveReturns { get { return Options.RemoveReturns; } set { Options.RemoveReturns = value; } }
        bool Verbose { get { return Options.Verbose; } set { Options.Verbose = value; } }
        bool DeclareVariablesAtBeginning { get { return Options.DeclareVariablesAtBeginning; } set { Options.DeclareVariablesAtBeginning = value; } }

        public CodeDisplayOptions Options = new CodeDisplayOptions();

        HashSet<int> LocalsDefined = new HashSet<int>();
        Stack<HashSet<int>> LocalsDefinedStack = new Stack<HashSet<int>>();

        public ExpressionDisplayer()
            : this(null)
        {

        }

        public ExpressionDisplayer(AinFile ainFile)
            : this(ainFile, -1)
        {

        }

        public ExpressionDisplayer(AinFile ainFile, int functionNumber)
        {
            this.ainFile = ainFile;
            this.currentFuncNumber = functionNumber;
        }

        public ExpressionDisplayer(AinFile ainFile, CodeDisplayOptions options)
            : this(ainFile)
        {
            this.Options = options;
        }

        public int currentFuncNumber;

        #region Declaration Get functions

        //public string GetDataTypeName(IVariable variable)
        //{
        //    if (variable == null)
        //    {
        //        return "";
        //    }
        //    return GetDataTypeName(variable.DataType, variable.StructType, variable.ArrayDimensions);
        //}

        public static string GetDataTypeName(IVariable variable)
        {
            if (variable == null)
            {
                return "";
            }
            int structType = GetStructType(variable);
            return GetDataTypeName(variable.DataType, structType, variable.ArrayDimensions, variable.Root);
        }

        private static int GetStructType(IVariable variable)
        {
            int structType = variable.StructType;
            if (variable.StructType == -1 && variable.DataType.IsFunction())
            {
                var metadata = variable.GetMetadata();
                if (metadata != null)
                {
                    structType = metadata.FuncTypeIndex;
                }
            }
            return structType;
        }

        public static string GetDataTypeNameReal(IVariable variable)
        {
            if (variable == null)
            {
                return "";
            }
            int structType = GetStructType(variable);
            return GetDataTypeNameReal(variable.DataType, structType, variable.ArrayDimensions, variable.Root);
        }

        public static int DefaultFuncType = -1;
        public static int DefaultDelegateType = -1;

        public static string GetDataTypeName(DataType dataType, int structType, int arrayDimensions, AinFile ainFile)
        {
            string typeString = dataType.ToString();
            bool isRef = false;
            bool isArray = false;

            if (typeString.StartsWith("Ref"))
            {
                isRef = true;
                typeString = typeString.Substring(3);
            }
            if (typeString.StartsWith("Array"))
            {
                isArray = true;
                typeString = typeString.Substring(5);
            }
            typeString = typeString.ToLowerInvariant();
            if (typeString == "struct")
            {
                if (structType != -1 && ainFile != null)
                {
                    if (structType >= 0 && structType < ainFile.Structs.Count)
                    {
                        //typeString = ainFile.Structs[structType].Name;
                        typeString = ainFile.Structs[structType].GetActiveName();
                    }
                }
            }
            else if (typeString == "function" || typeString == "functype")
            {
                if (structType == -1) structType = DefaultFuncType;
                if (structType != -1 && ainFile != null)
                {
                    if (structType >= 0 && structType < ainFile.FunctionTypes.Count)
                    {
                        //typeString = ainFile.FunctionTypes[structType].Name;
                        typeString = ainFile.FunctionTypes[structType].GetActiveName();
                    }
                }
            }
            else if (typeString == "delegate")
            {
                if (structType == -1) structType = DefaultDelegateType;
                if (structType != -1 && ainFile != null)
                {
                    if (structType >= 0 && structType < ainFile.Delegates.Count)
                    {
                        typeString = ainFile.Delegates[structType].GetActiveName();
                    }
                }
            }
            else if (typeString == "imainsystem")
            {
                typeString = "IMainSystem";
            }

            if (isArray)
            {
                if (arrayDimensions == 1)
                {
                    typeString = "array@" + typeString;
                }
                else
                {
                    typeString = "array@" + typeString + "@" + arrayDimensions;
                }
            }
            if (isRef)
            {
                typeString = "ref " + typeString;
            }
            return typeString;
        }

        public static string GetDataTypeNameReal(DataType dataType, int structType, int arrayDimensions, AinFile ainFile)
        {
            string typeString = dataType.ToString();
            bool isRef = false;
            bool isArray = false;

            if (typeString.StartsWith("Ref"))
            {
                isRef = true;
                typeString = typeString.Substring(3);
            }
            if (typeString.StartsWith("Array"))
            {
                isArray = true;
                typeString = typeString.Substring(5);
            }
            typeString = typeString.ToLowerInvariant();
            if (typeString == "struct")
            {
                if (structType != -1 && ainFile != null)
                {
                    if (structType >= 0 && structType < ainFile.Structs.Count)
                    {
                        typeString = ainFile.Structs[structType].Name;
                    }
                }
            }
            else if (typeString == "function" || typeString == "functype")
            {
                if (structType == -1) structType = DefaultFuncType;
                if (structType != -1 && ainFile != null)
                {
                    if (structType >= 0 && structType < ainFile.FunctionTypes.Count)
                    {
                        typeString = ainFile.FunctionTypes[structType].Name;
                    }
                }
            }
            else if (typeString == "delegate")
            {
                if (structType == -1) structType = DefaultDelegateType;
                if (structType != -1 && ainFile != null)
                {
                    if (structType >= 0 && structType < ainFile.Delegates.Count)
                    {
                        typeString = ainFile.Delegates[structType].Name;
                    }
                }
            }
            else if (typeString == "imainsystem")
            {
                typeString = "IMainSystem";
            }

            if (isArray)
            {
                if (arrayDimensions == 1)
                {
                    typeString = "array@" + typeString;
                }
                else
                {
                    typeString = "array@" + typeString + "@" + arrayDimensions;
                }
            }
            if (isRef)
            {
                typeString = "ref " + typeString;
            }
            return typeString;
        }

        public string GetDataTypeName(DataType dataType, int structType, int arrayDimensions)
        {
            return GetDataTypeName(dataType, structType, arrayDimensions, this.ainFile);
        }

        private static string EscapeString(string stringToEscape)
        {
            return AssemblerProjectWriter.EscapeString(stringToEscape);
        }
        private static string EscapeMessage(string stringToEscape)
        {
            return AssemblerProjectWriter.EscapeMessage(stringToEscape);
        }
        private static string EscapeAndQuoteString(string stringToEscape)
        {
            return AssemblerProjectWriter.EscapeAndQuoteString(stringToEscape);
        }
        private static string EscapeAndQuoteMessage(string stringToEscape)
        {
            return AssemblerProjectWriter.EscapeAndQuoteMessage(stringToEscape);
        }
        private static string EscapeString(string stringToEscape, char characterToEscape, bool quoteString)
        {
            return AssemblerProjectWriter.EscapeString(stringToEscape, characterToEscape, quoteString);
        }

        public string GetString(int stringNumber)
        {
            if (stringNumber >= 0 && stringNumber < ainFile.Strings.Count)
            {
                return EscapeString(ainFile.Strings[stringNumber]);
            }
            else
            {
                return "0x" + stringNumber.ToString("X");
            }
        }

        string GetLocalStructMemberName(int memberNumber)
        {
            var structMember = GetLocalStructMember(memberNumber);

            if (structMember != null)
            {
                if (ShowThis)
                {
                    //return "this." + structMember.Name;
                    return "this." + structMember.GetActiveName();
                }
                else
                {
                    //return structMember.Name;
                    return structMember.GetActiveName();
                }
            }
            else
            {
                if (ShowThis)
                {
                    return "this.INVALID_MEMBER[" + memberNumber.ToString() + "]";
                }
                else
                {
                    return "INVALID_MEMBER[" + memberNumber.ToString() + "]";
                }

            }
        }

        string GetGlobalVariableName(int variableNumber)
        {
            var globalVariable = GetGlobalVariable(variableNumber);
            if (globalVariable != null)
            {
                if (globalVariable.GroupIndex != -1 && ShowGlobalPrefix)
                {
                    //return ainFile.GlobalGroupNames[globalVariable.GroupIndex] + "." + globalVariable.Name;
                    return ainFile.GlobalGroupNames[globalVariable.GroupIndex] + "." + globalVariable.GetActiveName();
                }
                else
                {
                    //return globalVariable.Name;
                    return globalVariable.GetActiveName();
                }
            }
            else
            {
                return "INVALID_GLOBAL[" + variableNumber.ToString() + "]";
            }
        }

        private string GetLocalStructName()
        {
            var structInfo = GetLocalStruct();
            if (structInfo == null)
            {
                return "";
            }
            else
            {
                //return structInfo.Name;
                return structInfo.GetActiveName();
            }
        }

        private static string GetStructNameFromFunctionName(string functionName)
        {
            int atPosition = functionName.LastIndexOf('@');
            if (atPosition == -1) atPosition = functionName.Length;
            string className = functionName.Substring(0, atPosition);
            return className;
        }

        public string GetFunctionDeclaration(int functionNumber)
        {
            var function = GetFunction(functionNumber);
            return GetFunctionDeclaration(function, false);
        }

        public Function GetSystemCall(int sysCallNumber)
        {
            if (sysCallNumber >= 0 && sysCallNumber < AinFile.SystemCalls.Count)
            {
                return AinFile.SystemCalls[sysCallNumber];
            }
            return null;
        }

        public string GetSystemCallDeclaration(int sysCallNumber)
        {
            var function = GetSystemCall(sysCallNumber);
            return GetFunctionDeclaration(function, false);
        }

        //public string GetFunctionDeclaration(IFunction function)
        //{
        //    if (function == null)
        //    {
        //        return "";
        //    }
        //    var sb = new StringBuilder();
        //    sb.Append(GetDataTypeName(function) + " ");
        //    sb.Append(function.Name.Replace("@", "::"));   //now invalid
        //    sb.Append("(");
        //    bool needComma = false;
        //    int i;
        //    for (i = 0; i < function.ArgumentCount; i++)
        //    {
        //        if (function.Arguments[i].DataType != DataType.Void)
        //        {
        //            Util.PrintComma(sb, ref needComma);
        //            sb.Append(GetDataTypeName(function.Arguments[i]) + " ");
        //            sb.Append(function.Arguments[i].Name);
        //        }
        //    }
        //    sb.Append(")");
        //    return sb.ToString();
        //}

        public static string GetFunctionDeclaration(IFunction function)
        {
            return GetFunctionDeclaration(function, false);
        }

        public static string GetFunctionDeclaration(IFunction function, bool includeLocalVariables)
        {
            return GetFunctionDeclaration(function, includeLocalVariables, true);
        }

        public static string GetFunctionDeclaration(IFunction function, bool includeLocalVariables, bool includeDot)
        {
            StringBuilder sb = new StringBuilder();
            if (function == null)
            {
                return "";
            }
            //var ainFile = function.Root;
            var func = function as Function;
            var libf = function as HllFunction;
            bool isLabel = func != null && func.IsLabel != 0;

            if (isLabel)
            {
                sb.Append("#");
            }
            var split = function.Name.Split('@');
            var activeNameSplit = function.GetActiveName().Split('@');
            if (split.Length > 1)
            {
                string className = split[0];
                string functionName = split[1];

                string activeClassName = className;
                string activeFunctionName = functionName;

                if (activeNameSplit.Length > 1)
                {
                    activeClassName = activeNameSplit[0];
                    activeFunctionName = activeNameSplit[1];
                }

                if (functionName == "0")
                {
                    //constructor
                    //sb.Append(className + "::" + className);
                    if (includeDot)
                    {
                        sb.Append(activeClassName + "::" + activeClassName);
                    }
                    else
                    {
                        sb.Append(activeClassName);
                    }
                }
                else if (functionName == "1")
                {
                    //destructor
                    //sb.Append(className + "::~" + className);
                    if (includeDot)
                    {
                        sb.Append(activeClassName + "::~" + activeClassName);
                    }
                    else
                    {
                        sb.Append("~" + activeClassName);
                    }
                }
                else
                {
                    if (!(function.DataType == DataType.Void && isLabel))
                    {
                        sb.Append(GetDataTypeName(function) + " ");
                    }
                    if (libf != null && includeDot)
                    {
                        //sb.Append(libf.ParentLibrary.LibraryName + ".");
                        sb.Append(libf.ParentLibrary.GetActiveName() + ".");
                    }
                    //sb.Append(className + "::" + functionName);
                    if (includeDot)
                    {
                        sb.Append(activeClassName + "::" + activeFunctionName);
                    }
                    else
                    {
                        sb.Append(activeFunctionName);
                    }
                }
            }
            else
            {
                if (!(function.DataType == DataType.Void && func != null && func.IsLabel != 0))
                {
                    sb.Append(GetDataTypeName(function) + " ");
                }
                if (libf != null && includeDot)
                {
                    //sb.Append(libf.ParentLibrary.LibraryName + ".");
                    sb.Append(libf.ParentLibrary.GetActiveName() + ".");
                }
                //sb.Append(function.Name);
                sb.Append(function.GetActiveName());
            }

            sb.Append("(");
            bool needComma = false;
            int i;
            for (i = 0; i < function.ParameterCount; i++)
            {
                if (function.Parameters[i].DataType != DataType.Void)
                {
                    Util.PrintComma(sb, ref needComma);
                    var arg = function.Parameters[i];
                    //sb.Append(GetDataTypeName(arg) + " " + arg.Name);
                    sb.Append(GetDataTypeName(arg) + " " + arg.GetActiveName());
                    var metadata = arg.GetMetadata();
                    if (metadata != null && metadata.DefaultValue != null)
                    {
                        sb.Append(" = " + metadata.DefaultValue.GetValueQuoted());
                    }
                }
            }
            sb.Append(")");
            if (includeLocalVariables)
            {
                needComma = true;
                for (; i < function.Parameters.Count; i++)
                {
                    if (function.Parameters[i].DataType != DataType.Void)
                    {
                        Util.PrintComma(sb, ref needComma);
                        var arg = function.Parameters[i];
                        //sb.Append(GetDataTypeName(arg) + " " + arg.Name);
                        sb.Append(GetDataTypeName(arg) + " " + arg.GetActiveName());
                    }
                }
            }
            return sb.ToString();
        }

        //private string GetFunctionDeclaration(Function function)
        //{
        //    if (function == null)
        //    {
        //        return "";
        //    }
        //    var sb = new StringBuilder();
        //    if (function.IsLabel != 0)
        //    {
        //        sb.Append("#");
        //    }
        //    if (!(function.DataType == DataType.Void && function.IsLabel != 0))
        //    {
        //        sb.Append(GetDataTypeName(function) + " ");
        //    }
        //    sb.Append(function.Name);
        //    sb.Append("(");
        //    bool needComma = false;
        //    int i;
        //    for (i = 0; i < function.ArgumentCount; i++)
        //    {
        //        if (function.Arguments[i].DataType != DataType.Void)
        //        {
        //            Util.PrintComma(sb, ref needComma);
        //            sb.Append(GetDataTypeName(function.Arguments[i]) + " ");
        //            sb.Append(function.Arguments[i].Name);
        //        }
        //    }
        //    sb.Append(")");
        //    return sb.ToString();
        //}

        private void PrintFunctionDeclaration(IFunction function)
        {
            //same as GetFunctionDeclaration, but also creates expressions that you can mouse over
            if (function == null)
            {
                return;
            }
            bool isMethod = false;

            //var ainFile = function.Root;
            var func = function as Function;
            var libf = function as HllFunction;

            if (func != null && func.IsLabel != 0)
            {
                tw.Write("#");
            }
            var split = function.Name.Split('@');
            var activeNameSplit = function.GetActiveName().Split('@');
            if (split.Length > 1)
            {
                isMethod = true;
                string className = split[0];
                string functionName = split[1];

                string activeClassName = className;
                string activeFunctionName = functionName;

                if (activeNameSplit.Length > 1)
                {
                    activeClassName = activeNameSplit[0];
                    activeFunctionName = activeNameSplit[1];
                }

                if (functionName == "0")
                {
                    //constructor
                    //tw.Write(className + "::" + className);
                    tw.Write(activeClassName + "::" + activeClassName);
                }
                else if (functionName == "1")
                {
                    //destructor
                    //tw.Write(className + "::~" + className);
                    tw.Write(activeClassName + "::~" + activeClassName);
                }
                else
                {
                    if (!(function.DataType == DataType.Void && func != null && func.IsLabel != 0))
                    {
                        tw.Write(GetDataTypeName(function) + " ");
                    }
                    if (libf != null)
                    {
                        //tw.Write(libf.ParentLibrary.LibraryName + ".");
                        tw.Write(libf.ParentLibrary.GetActiveName() + ".");
                    }
                    //tw.Write(className + "::" + functionName);
                    tw.Write(activeClassName + "::" + activeFunctionName);
                }
            }
            else
            {
                if (!(function.DataType == DataType.Void && func != null && func.IsLabel != 0))
                {
                    tw.Write(GetDataTypeName(function) + " ");
                }
                if (libf != null)
                {
                    //tw.Write(libf.ParentLibrary.LibraryName + ".");
                    tw.Write(libf.ParentLibrary.GetActiveName() + ".");
                }
                //tw.Write(function.Name);
                tw.Write(function.GetActiveName());
            }

            tw.Write("(");
            bool needComma = false;
            int i;
            for (i = 0; i < function.ParameterCount; i++)
            {
                if (function.Parameters[i].DataType != DataType.Void)
                {
                    Util.PrintComma(tw, ref needComma);
                    var arg = function.Parameters[i];
                    int startPosition = GetExpressionStartPosition();
                    tw.Write(GetDataTypeName(arg) + " ");
                    //tw.Write(arg.Name);
                    tw.Write(arg.GetActiveName());
                    if (!(isMethod && !Options.DisplayDefaultValuesForMethods))
                    {
                        var metadata = arg.GetMetadata();
                        if (metadata != null && metadata.DefaultValue != null)
                        {
                            tw.Write(" = " + metadata.DefaultValue.GetValueQuoted());
                        }
                        SaveExpression(new Expression(Instruction.SH_LOCALREF, -1, i), startPosition);
                    }
                }
            }
            tw.Write(")");
        }

        public string GetHllDeclaration(int libraryNumber, int functionNumber)
        {
            IFunction function = GetLibraryFunction(libraryNumber, functionNumber);
            return GetFunctionDeclaration(function);

            //if (function == null) return "";
            //string functionName = GetLibraryFunctionName(libraryNumber, functionNumber);

            //var sb = new StringBuilder();

            //sb.Append(GetDataTypeName(function) + " ");
            //sb.Append(functionName);
            //sb.Append("(");
            //bool needComma = false;
            //for (int i = 0; i < function.ParameterCount; i++)
            //{
            //    if (function.Parameters[i].DataType != DataType.Void)
            //    {
            //        Util.PrintComma(sb, ref needComma);
            //        sb.Append(GetDataTypeName(function.Parameters[i]) + " ");
            //        sb.Append(function.Parameters[i].Name);
            //    }
            //}
            //sb.Append(")");
            //string functionDeclaration = sb.ToString();
            //return functionDeclaration;
        }

        public IVariable GetGlobalVariable(int globalNumber)
        {
            if (globalNumber < 0 || globalNumber >= ainFile.Globals.Count) return null;
            var global = ainFile.Globals[globalNumber];
            return global;
        }

        public IVariable GetLocalStructMember(int memberNumber)
        {
            var localStruct = GetLocalStruct();
            if (localStruct == null || memberNumber < 0 || memberNumber >= localStruct.Members.Count) return null;
            return localStruct.Members[memberNumber];
        }

        public string GetGlobalVariableDeclaration(int globalNumber)
        {
            var global = GetGlobalVariable(globalNumber);
            if (global == null) return "";
            return GetVariableDeclaration(global);
        }

        public string GetVariableDeclaration(IVariable variable)
        {
            if (variable == null)
            {
                return "";
            }
            string variableName = GetVariableName(variable);
            return GetDataTypeName(variable) + " " + variableName;
        }

        public string GetVariableName(IVariable variable)
        {
            //string variableName = ((variable.GroupIndex != -1 && ShowGlobalPrefix) ? ainFile.GlobalGroupNames[variable.GroupIndex] + "." : "") +
            //    variable.Name;
            string variableName = ((variable.GroupIndex != -1 && ShowGlobalPrefix) ? ainFile.GlobalGroupNames[variable.GroupIndex] + "." : "") +
                variable.GetActiveName();
            return variableName;
        }

        public Function GetCurrentFunction()
        {
            return GetFunction(currentFuncNumber);
        }

        public Function GetFunction(int functionNumber)
        {
            if (functionNumber >= 0 && functionNumber < ainFile.Functions.Count)
            {
                return ainFile.Functions[functionNumber];
            }
            else
            {
                return null;
            }
        }

        public IVariable GetLocalVariable(int localVariableNumber)
        {
            var function = GetCurrentFunction();
            if (function == null) return null;
            if (localVariableNumber < 0 || localVariableNumber >= function.Parameters.Count) return null;
            var variable = function.Parameters[localVariableNumber];
            return variable;
        }

        public string GetLocalVariableDeclaration(int localVariableNumber)
        {
            var localVariable = GetLocalVariable(localVariableNumber);
            if (localVariable == null) return "";
            return GetVariableDeclaration(localVariable);
        }

        public Struct GetLocalStruct()
        {
            var function = GetCurrentFunction();
            if (function == null) return null;

            string className = GetStructNameFromFunctionName(function.Name);
            var structInfo = ainFile.GetStruct(className);
            return structInfo;

            //if (ainFile.StructNameToIndex.ContainsKey(className))
            //{
            //    int structIndex = ainFile.StructNameToIndex[className];
            //    var structInfo = ainFile.Structs[structIndex];
            //    return structInfo;
            //}
            //return null;
        }

        public string GetLocalStructMemberDeclaration(int structVariableNumber)
        {
            var structInfo = GetLocalStruct();
            if (structInfo == null) return "";
            if (structVariableNumber < 0 || structVariableNumber >= structInfo.Members.Count) return "";
            var member = structInfo.Members[structVariableNumber];
            //return GetDataTypeName(member) + " " + structInfo.Name + "." + structInfo.Members[structVariableNumber].Name;
            return GetDataTypeName(member) + " " + structInfo.GetActiveName() + "." + structInfo.Members[structVariableNumber].GetActiveName();
        }

        public string GetFileName(int filenameNumber)
        {
            if (filenameNumber >= 0 && filenameNumber < ainFile.Filenames.Count)
            {
                return ainFile.Filenames[filenameNumber];
            }
            else
            {
                return "0x" + filenameNumber.ToString("X");
            }
        }

        #endregion

        static bool _doNotRecurse = false;
        Dictionary<Expression, Variable> ExpressionsToDeclareVariablesAt = null;

        public string PrintExpression2(Expression expression, bool declareVariables)
        {
            if (DeclareVariablesAtBeginning)
            {
                declareVariables = false;
            }

            if (declareVariables)
            {
                if (_doNotRecurse == false)
                {
                    _doNotRecurse = true;
                    this.ExpressionsToDeclareVariablesAt = ExpressionDisplayer.FindVariableDeclarations(expression);
                    _doNotRecurse = false;
                }

                LocalsDefined = new HashSet<int>();
                LocalsDefinedStack = new Stack<HashSet<int>>();
                expressionMap = new ExpressionMap();
                expressionMap.displayer = this;
                DeclareVariables = true;
            }
            else
            {
                DeclareVariables = false;
            }

            var utf8 = new UTF8Encoding(false);
            ms = new MemoryStream();

            if (Verbose)
            {
                this.ShowCasting = true;
                this.RemoveReturns = false;
            }

            var streamWriter = new StreamWriter(ms, utf8);
            //var stringWriter = new StringWriter();
            tw = new MyIndentedTextWriter(streamWriter);
            PrintExpression(expression);
            tw.Flush();

            DeclareVariables = false;
            LocalsDefined.Clear();
            LocalsDefinedStack.Clear();

            return utf8.GetString(ms.GetBuffer(), 0, (int)ms.Length);

        }

        public string PrintExpression2(Expression expression)
        {
            return PrintExpression2(expression, true);
        }

        void OpenBlock()
        {
            if (DeclareVariables)
            {
                LocalsDefinedStack.Push(LocalsDefined);
                LocalsDefined = new HashSet<int>(LocalsDefined);
            }
            isTopExpression = 2;
            if (!tw.TabsPending) { tw.WriteLine(); }
            tw.Write("{");
            tw.Indent++;
            tw.WriteLine();
        }

        void CloseBlock()
        {
            CloseBlock("");
        }

        void CloseBlock(string trailingString)
        {
            if (DeclareVariables)
            {
                LocalsDefined = LocalsDefinedStack.Pop();
            }
            tw.Indent--;
            if (!tw.TabsPending)
            {
                tw.WriteLine();
            }
            if (trailingString == null)
            {
                tw.Write("}");
            }
            else
            {
                tw.WriteLine("}" + trailingString);
            }
        }

        void PrintBlockExpression(Expression expression)
        {
            PrintBlockExpression(expression, "");
        }

        void PrintBlockExpression(Expression expression, string trailingString)
        {
            OpenBlock();
            int twRow = tw.LineNumber;
            int twColumn = tw.Column;
            PrintExpression(expression);
            if (twRow != tw.LineNumber || twColumn != tw.Column)
            {
                PrintLastSemicolonIfNeeded(expression);
            }
            CloseBlock(trailingString);
        }

        private void PrintLastSemicolonIfNeeded(Expression expression)
        {
            if (expression != null && expression.ExpressionType != Instruction.Statement &&
                !expression.IsBlock() && !(expression.ExpressionType.IsLabel()))
            {
                if (!expression.ExpressionType.IsLabel())
                {
                    tw.Write(";");
                }
                tw.WriteLine();
            }
            else
            {
                if (!tw.TabsPending)
                {
                    tw.WriteLine();
                }
            }
        }

        public IVariable GetReferencedVariableType(Expression expression)
        {
            if (expression == null)
            {
                return null;
            }
            var arg1 = expression.Arg1;
            var arg2 = expression.Arg2;

            if (expression.ExpressionType == Instruction.PUSH)
            {
                return new Variable() { Name = expression.Value.ToString(), DataType = DataType.Int, Root = ainFile };
            }

            if (arg1 != null && arg1.ExpressionType == Instruction.PUSH)
            {
                var temp = arg2;
                arg2 = arg1;
                arg1 = temp;
            }

            if (arg1 != null && arg2 != null && arg2.ExpressionType == Instruction.PUSH)
            {
                int value = arg2.Value;
                if (arg1.ExpressionType == Instruction.PUSHGLOBALPAGE)
                {
                    return GetGlobalVariable(value);
                }
                if (arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                {
                    return GetLocalStructMember(value);
                }
                if (arg1.ExpressionType == Instruction.PUSHLOCALPAGE)
                {
                    return GetLocalVariable(value);
                }
            }

            if (Expression.IsLeafRef(expression.ExpressionType))
            {
                int value = expression.Value;
                if (expression.ExpressionType == Instruction.SH_GLOBALREF)
                {
                    return GetGlobalVariable(value);
                }
                else if (expression.ExpressionType == Instruction.SH_STRUCTREF)
                {
                    return GetLocalStructMember(value);
                }
                else
                {
                    return GetLocalVariable(value);
                }
            }

            if (expression.IsReference())
            {
                if (arg1 != null && arg2 == null)
                {
                    //need to do sometihng with childType
                    var childType = GetReferencedVariableType(arg1);
                    if ((expression.ExpressionType == Instruction.REF || expression.ExpressionType == Instruction.AssignmentRef) && (arg1.ExpressionType == Instruction.REFREF || arg1.ExpressionType == Instruction.S_REFREF || arg1.ExpressionType == Instruction.SR_REFREF || arg1.ExpressionType == Instruction.SH_LOCALREF))
                    {
                        //change "REF" into a base type
                        var newVariable = new Variable();
                        newVariable.Name = childType.Name;
                        //newVariable.name = expression.ExpressionType.ToString() + " " + childType.Name;
                        newVariable.SetVariableType(childType);
                        newVariable.Parent = childType.Parent;
                        newVariable.Root = childType.Root;
                        newVariable.DataType = newVariable.DataType.GetTypeOfRef();
                        return newVariable;

                    }
                    else if (expression.ExpressionType == Instruction.SR_REF || expression.ExpressionType == Instruction.SR_REF2)
                    {
                        ////need to test this
                        //if (arg1 != null && arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                        //{
                        //    //make a "this"
                        //    var newVariable = GetLocalStruct().Clone();
                        //    newVariable.Name = "this";
                        //    return newVariable;
                        //}
                        //else
                        //{
                        return GetReferencedVariableType(arg1);
                        //}
                    }
                }

                if (arg1 != null && arg2 != null)
                {
                    if (expression.ExpressionType == Instruction.StructAssignmentRef)
                    {
                        //reutrn left, ignore right
                        return GetReferencedVariableType(arg1);
                    }


                    var left = GetReferencedVariableType(arg1);
                    var right = GetReferencedVariableType(arg2);
                    if (left != null && left.ArrayDimensions > 0)
                    {
                        //next array dimension?
                        if (left.ArrayDimensions > 1)
                        {
                            var newVariable = new Variable();
                            newVariable.SetVariableType(left);
                            newVariable.Parent = left.Parent;
                            newVariable.Name = left.Name + "[" + right.Name + "]";
                            newVariable.ArrayDimensions--;
                            newVariable.Root = left.Root;
                            return newVariable;
                        }
                        else
                        {
                            //return type of array element
                            var newVariable = new Variable();
                            newVariable.SetVariableType(left);
                            newVariable.Parent = left.Parent;
                            newVariable.Name = left.Name + "[" + right.Name + "]";
                            newVariable.DataType = newVariable.DataType.GetTypeOfArrayElement();
                            newVariable.ArrayDimensions = 0;
                            newVariable.Root = left.Root;
                            return newVariable;
                        }
                    }
                    else if (left != null && (left.DataType == DataType.RefStruct || left.DataType == DataType.Struct) && left.StructType != -1)
                    {
                        if (arg2.ExpressionType == Instruction.PUSH)
                        {
                            int value = arg2.Value;
                            return GetStructMember(left.StructType, value);
                        }
                    }
                    else if ((left.DataType == DataType.String || left.DataType == DataType.RefString) && right.DataType == DataType.Int)
                    {
                        var newVariable = new Variable();
                        newVariable.DataType = DataType.Int;
                        newVariable.Parent = left.Parent;
                        newVariable.Root = left.Root;
                        newVariable.Name = left.Name + "[" + right.Name + "]";
                        return newVariable;
                    }
                }
            }
            else if (expression.ExpressionType == Instruction.PrintStructType)
            {
                //fake instruction to make it output a dummy struct
                int structIndex = expression.Value;
                var structInfo = ainFile.Structs[structIndex];

                int memberIndex = expression.Value2;
                if (memberIndex == -1)
                {
                    return structInfo;
                }
                else
                {
                    var newVariable = structInfo.Members[memberIndex];
                    return newVariable;
                }
            }
            else if (expression.ExpressionType == Instruction.PUSHSTRUCTPAGE)
            {
                //make a "this"
                var localStruct = GetLocalStruct();
                if (localStruct != null)
                {
                    var newVariable = localStruct.Clone();
                    newVariable.Name = "this";
                    return newVariable;
                }
                else
                {
                    return null;
                }
            }
            else if (expression.ExpressionType == Instruction.NEW)
            {
                if (expression.Arg1 != null && expression.Arg1.ExpressionType == Instruction.PUSH)
                {
                    int structIndex = expression.Arg1.Value;
                    if (structIndex >= 0 && structIndex < ainFile.Structs.Count)
                    {
                        var structInfo = ainFile.Structs[structIndex];
                        var constructor = structInfo.GetConstructor();
                        var newVariable = new Variable("new " + structInfo.Name, DataType.RefStruct, structIndex, 0);
                        if (constructor != null)
                        {
                            newVariable.Root = constructor.Root;
                        }
                        else
                        {
                            newVariable.Root = ainFile;
                        }
                        return newVariable;
                    }
                }
                return null;
            }
            else // (!expression.IsReference())
            {
                if (castOperations.Contains(expression.ExpressionType))
                {
                    var left = GetReferencedVariableType(expression.Arg1);
                    if (left != null)
                    {
                        var newVariable = new Variable();
                        newVariable.SetVariableType(left);
                        newVariable.DataType = expression.GetDataType();
                        newVariable.Name = left.Name;
                        newVariable.Parent = left.Parent;
                        newVariable.Root = left.Root;
                        return newVariable;
                    }
                    else
                    {

                    }
                }

                if (expression.ExpressionType == Instruction.C_ASSIGN)
                {
                    var stringVariable = GetReferencedVariableType(arg1);
                    var arrayIndex = GetReferencedVariableType(arg2);
                    var newVariable = new Variable(stringVariable.Name + "[" + arrayIndex.Name + "]", DataType.Int, -1, 0);
                    newVariable.Root = ainFile;
                    return newVariable;

                }

                if (Expression.IsAssignment(expression.ExpressionType))
                {
                    return GetReferencedVariableType(arg1);
                }

                if (expression.ExpressionType == Instruction.CALLFUNC || expression.ExpressionType == Instruction.CALLMETHOD || expression.ExpressionType == Instruction.FUNC)
                {
                    return GetFunction(expression.Value);
                }

                if (expression.ExpressionType == Instruction.DG_CALLBEGIN)
                {
                    int delegateNumber = expression.Value;
                    if (delegateNumber >= 0 && delegateNumber < ainFile.Delegates.Count)
                    {
                        return ainFile.Delegates[delegateNumber];
                    }
                }

                if (expression.ExpressionType == Instruction.CALLSYS)
                {
                    return GetSystemCall(expression.Value);
                }

                if (expression.ExpressionType == Instruction.CALLHLL)
                {
                    return GetLibraryFunction(expression.Value, expression.Value2);
                }

                if (expression.ExpressionType == Instruction.CALLFUNC2)
                {
                    if (arg1 != null && arg2 != null && arg2.ExpressionType == Instruction.PUSH)
                    {
                        //var var1 = GetReferencedVariableType(arg1);
                        var funcType = ainFile.GetFuncType(arg2.Value);
                        return funcType;
                        //if (funcType != null && arg1 != null)
                        //{
                        //    var newVariable = new FunctionType();
                        //    newVariable.Root = ainFile;
                        //    newVariable.DataType = funcType.DataType;
                        //    newVariable.StructType = funcType.StructType;
                        //    newVariable.Name = var1.Name;
                        //    newVariable.Parameters = funcType.Parameters;
                        //    return newVariable;
                        //}
                    }
                }

                if (expression.ExpressionType == Instruction.IfElse)
                {
                    //needs testing
                    var variable = new Variable();
                    variable.Name = expression.ExpressionType.ToString();
                    variable.Root = ainFile;
                    if (expression.Arg2 != null && expression.Arg2.ExpressionType != Instruction.Statement &&
                        expression.Arg3 != null && expression.Arg3.ExpressionType != Instruction.Statement)
                    {
                        variable.Name = "QuestionColonOperator";
                        variable.SetVariableType(expression.Arg2.Variable);
                    }
                    else
                    {
                        variable.DataType = DataType.Void;
                    }
                    return variable;
                }

                {
                    var variable = new Variable();
                    variable.DataType = expression.GetDataType();
                    variable.Name = expression.ExpressionType.ToString();
                    variable.Root = ainFile;
                    return variable;
                }
            }

            return null;
        }

        public static IVariable GetReferencedVariableType2(Expression expression)
        {
            if (expression == null)
            {
                return null;
            }
            var ainFile = expression.ainFile;
            var funcExpression = expression.Root;
            Function function = null;
            Struct localStructInfo = null;
            if (funcExpression.ExpressionType == Instruction.FUNC)
            {
                function = ainFile.GetFunction(funcExpression.Value);
            }
            if (function != null)
            {
                localStructInfo = function.GetClass();
            }
            return GetReferencedVariableType2(expression, function, localStructInfo);
        }

        public static IVariable GetReferencedVariableType2(Expression expression, Function function, Struct localStructInfo)
        {
            if (expression == null)
            {
                return null;
            }
            var ainFile = expression.ainFile;

            var arg1 = expression.Arg1;
            var arg2 = expression.Arg2;

            if (expression.ExpressionType == Instruction.PUSH)
            {
                return new Variable() { Name = expression.Value.ToString(), DataType = DataType.Int, Root = ainFile };
            }

            if (arg1 != null && arg1.ExpressionType == Instruction.PUSH)
            {
                var temp = arg2;
                arg2 = arg1;
                arg1 = temp;
            }

            if (arg1 != null && arg2 != null && arg2.ExpressionType == Instruction.PUSH && expression.ExpressionType != Instruction.DG_NEW_FROM_METHOD)
            {
                int value = arg2.Value;
                if (arg1.ExpressionType == Instruction.PUSHGLOBALPAGE)
                {
                    return ainFile.GetGlobal(value);
                }
                if (arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                {
                    if (localStructInfo == null)
                    {
                        return null;
                    }
                    return localStructInfo.Members.GetOrNull(value);
                }
                if (arg1.ExpressionType == Instruction.PUSHLOCALPAGE)
                {
                    if (function == null)
                    {
                        return null;
                    }
                    return function.Parameters.GetOrNull(value);
                }
            }

            if (Expression.IsLeafRef(expression.ExpressionType))
            {
                if (expression.ExpressionType == Instruction.SH_STRUCTREF2)
                {
                    return ainFile.GetStructMember(localStructInfo, expression.Value).GetStructType().Members.GetOrNull(expression.Value2);
                }
                var argumentKind = expression.ExpressionType.GetArgumentKinds().FirstOrDefault();

                int value = expression.Value;
                if (argumentKind == ArgumentKind.Local)
                {
                    return ainFile.GetFunctionParameter(function, value);
                }
                else if (argumentKind == ArgumentKind.Global)
                {
                    return ainFile.GetGlobal(value);
                }
                else if (argumentKind == ArgumentKind.LocalMember)
                {
                    return ainFile.GetStructMember(localStructInfo, value);
                }

                //if (expression.ExpressionType == Instruction.SH_GLOBALREF)
                //{
                //    return ainFile.GetGlobal(value);
                //}
                //else if (expression.ExpressionType == Instruction.SH_STRUCTREF)
                //{
                //    if (localStructInfo == null)
                //    {
                //        return null;
                //    }
                //    return localStructInfo.Members.GetOrNull(value);
                //}
                //else
                //{
                //    if (function == null)
                //    {
                //        return null;
                //    }
                //    return function.Parameters.GetOrNull(value);
                //}
            }

            if (expression.IsReference())
            {
                if (arg2 == null && (expression.ExpressionType == Instruction.S_REF2 || expression.ExpressionType == Instruction.PAGE_REF))
                {
                    arg2 = new Expression(Instruction.PUSH, expression.address, expression.Value);
                }

                if (arg1 != null && arg2 == null)
                {
                    //need to do sometihng with childType
                    var childType = GetReferencedVariableType2(arg1, function, localStructInfo);
                    if ((expression.ExpressionType == Instruction.REF || expression.ExpressionType == Instruction.AssignmentRef) && (arg1.ExpressionType == Instruction.REFREF || arg1.ExpressionType == Instruction.S_REFREF || arg1.ExpressionType == Instruction.SR_REFREF || arg1.ExpressionType == Instruction.SH_LOCALREF || arg1.ExpressionType == Instruction.SH_GLOBALREF || arg1.ExpressionType == Instruction.SH_LOCAL_S_REF || arg1.ExpressionType == Instruction.SH_LOCALREFREF))
                    {
                        //change "REF" into a base type
                        var newVariable = new Variable();
                        newVariable.Name = childType.Name;
                        //newVariable.name = expression.ExpressionType.ToString() + " " + childType.Name;
                        newVariable.SetVariableType(childType);
                        newVariable.Parent = childType.Parent;
                        newVariable.Root = childType.Root;
                        newVariable.Index = childType.Index;
                        newVariable.DataType = newVariable.DataType.GetTypeOfRef();
                        return newVariable;

                    }
                    else if (expression.ExpressionType == Instruction.SR_REF || expression.ExpressionType == Instruction.SR_REF2)
                    {
                        ////need to test this
                        //if (arg1 != null && arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                        //{
                        //    //make a "this"
                        //    var newVariable = GetLocalStruct().Clone();
                        //    newVariable.Name = "this";
                        //    return newVariable;
                        //}
                        //else
                        //{
                        return GetReferencedVariableType2(arg1, function, localStructInfo);
                        //}
                    }
                    else
                    {
                        return GetReferencedVariableType2(arg1, function, localStructInfo);
                    }
                }

                if (arg1 != null && arg2 != null)
                {
                    if (expression.ExpressionType == Instruction.StructAssignmentRef)
                    {
                        //reutrn left, ignore right
                        return GetReferencedVariableType2(arg1, function, localStructInfo);
                    }


                    var left = GetReferencedVariableType2(arg1, function, localStructInfo);
                    var right = GetReferencedVariableType2(arg2, function, localStructInfo);
                    if (left != null && left.ArrayDimensions > 0)
                    {
                        //next array dimension?
                        if (left.ArrayDimensions > 1)
                        {
                            var newVariable = new Variable();
                            newVariable.SetVariableType(left);
                            newVariable.Parent = left.Parent;
                            newVariable.Name = left.Name + "[" + right.Name + "]";
                            newVariable.ArrayDimensions--;
                            newVariable.Root = left.Root;
                            return newVariable;
                        }
                        else
                        {
                            //return type of array element
                            var newVariable = new Variable();
                            newVariable.SetVariableType(left);
                            newVariable.Parent = left.Parent;
                            newVariable.Name = left.Name + "[" + right.Name + "]";
                            newVariable.DataType = newVariable.DataType.GetTypeOfArrayElement();
                            newVariable.ArrayDimensions = 0;
                            newVariable.Root = left.Root;
                            return newVariable;
                        }
                    }
                    else if (left != null && (left.DataType == DataType.RefStruct || left.DataType == DataType.Struct) && left.StructType != -1)
                    {
                        if (arg2.ExpressionType == Instruction.PUSH)
                        {
                            int value = arg2.Value;
                            return ainFile.GetStructMember(left.StructType, value);
                        }
                    }
                    else if (left != null && (left.DataType == DataType.String || left.DataType == DataType.RefString) && right.DataType == DataType.Int)
                    {
                        var newVariable = new Variable();
                        newVariable.DataType = DataType.Int;
                        newVariable.Parent = left.Parent;
                        newVariable.Root = left.Root;
                        newVariable.Name = left.Name + "[" + right.Name + "]";
                        return newVariable;
                    }
                }
            }
            else if (expression.ExpressionType == Instruction.PrintStructType)
            {
                //fake instruction to make it output a dummy struct
                int structIndex = expression.Value;
                var member = ainFile.GetStructMember(structIndex, expression.Value2);
                if (member != null)
                {
                    return member;
                }
                var structInfo = ainFile.GetStruct(structIndex);
                return structInfo;
            }
            else if (expression.ExpressionType == Instruction.PUSHSTRUCTPAGE)
            {
                //make a "this"
                if (localStructInfo != null)
                {
                    var newVariable = localStructInfo.Clone();
                    newVariable.Name = "this";
                    return newVariable;
                }
                else
                {
                    return null;
                }
            }
            else if (expression.ExpressionType == Instruction.NEW)
            {
                if (expression.Arg1 != null && expression.Arg1.ExpressionType == Instruction.PUSH)
                {
                    int structIndex = expression.Arg1.Value;
                    if (structIndex >= 0 && structIndex < ainFile.Structs.Count)
                    {
                        var structInfo = ainFile.Structs[structIndex];
                        var constructor = structInfo.GetConstructor();
                        var newVariable = new Variable(structInfo.Name, DataType.RefStruct, structIndex, 0);
                        if (constructor != null)
                        {
                            newVariable.Root = constructor.Root;
                        }
                        else
                        {
                            newVariable.Root = ainFile;
                        }
                        return newVariable;
                    }
                }
                return null;
            }
            else // (!expression.IsReference())
            {
                if (castOperations.Contains(expression.ExpressionType))
                {
                    var left = GetReferencedVariableType2(expression.Arg1, function, localStructInfo);
                    if (left != null)
                    {
                        var newVariable = new Variable();
                        newVariable.SetVariableType(left);
                        newVariable.DataType = expression.GetDataType();
                        newVariable.Name = left.Name;
                        newVariable.Parent = left.Parent;
                        newVariable.Root = left.Root;
                        return newVariable;
                    }
                    else
                    {

                    }
                }

                if (expression.ExpressionType == Instruction.C_ASSIGN)
                {
                    var stringVariable = GetReferencedVariableType2(arg1, function, localStructInfo);
                    var arrayIndex = GetReferencedVariableType2(arg2, function, localStructInfo);
                    var newVariable = new Variable(stringVariable.Name + "[" + arrayIndex.Name + "]", DataType.Int, -1, 0);
                    newVariable.Root = ainFile;
                    return newVariable;

                }

                if (Expression.IsAssignment(expression.ExpressionType))
                {
                    return GetReferencedVariableType2(arg1, function, localStructInfo);
                }

                if (expression.ExpressionType == Instruction.CALLFUNC || expression.ExpressionType == Instruction.CALLMETHOD || expression.ExpressionType == Instruction.FUNC)
                {
                    return ainFile.GetFunction(expression.Value);
                }

                if (expression.ExpressionType == Instruction.DG_CALLBEGIN)
                {
                    int delegateNumber = expression.Value;
                    if (delegateNumber >= 0 && delegateNumber < ainFile.Delegates.Count)
                    {
                        return ainFile.Delegates[delegateNumber];
                    }
                }

                if (expression.ExpressionType == Instruction.CALLSYS)
                {
                    return AinFile.GetSystemCall(expression.Value);
                }

                if (expression.ExpressionType == Instruction.CALLHLL)
                {
                    return ainFile.GetLibraryFunction(expression.Value, expression.Value2);
                }

                if (expression.ExpressionType == Instruction.CALLFUNC2)
                {
                    if (arg1 != null && arg2 != null && arg2.ExpressionType == Instruction.PUSH)
                    {
                        //var var1 = GetReferencedVariableType(arg1);
                        var funcType = ainFile.GetFuncType(arg2.Value);
                        return funcType;
                        //if (funcType != null && arg1 != null)
                        //{
                        //    var newVariable = new FunctionType();
                        //    newVariable.Root = ainFile;
                        //    newVariable.DataType = funcType.DataType;
                        //    newVariable.StructType = funcType.StructType;
                        //    newVariable.Name = var1.Name;
                        //    newVariable.Parameters = funcType.Parameters;
                        //    return newVariable;
                        //}
                    }
                }

                if (expression.ExpressionType == Instruction.IfElse)
                {
                    //needs testing
                    var variable = new Variable();
                    variable.Name = expression.ExpressionType.ToString();
                    variable.Root = ainFile;
                    if (expression.Arg2 != null && expression.Arg2.ExpressionType != Instruction.Statement &&
                        expression.Arg3 != null && expression.Arg3.ExpressionType != Instruction.Statement)
                    {
                        variable.Name = "QuestionColonOperator";
                        variable.SetVariableType(expression.Arg2.Variable);
                    }
                    else
                    {
                        variable.DataType = DataType.Void;
                    }
                    return variable;
                }

                if (expression.ExpressionType == Instruction.DG_NEW_FROM_METHOD)
                {
                    //var method = ainFile.GetFunction(expression.Arg2.Value);
                    //string name = expression.Arg1.ToString(false)+"." + method.GetFunctionName();
                    //int delegateTypeIndex = -1;
                    //var delegateType = ainFile.GetDelegateType(method);
                    //if (delegateType != null)
                    //{
                    //    delegateTypeIndex = delegateType.Index;
                    //}
                    return new Variable("DG_NEW_FROM_METHOD", DataType.Delegate, -1, 0);
                }

                if (expression.ExpressionType >= Instruction.SH_SR_ASSIGN)
                {
                    switch (expression.ExpressionType)
                    {
                        //bool:
                        case Instruction.SH_GLOBALSREF_EMPTY:
                        case Instruction.SH_GLOBALSREF_NE_STR0:
                        case Instruction.SH_LOC_LT_IMM_OR_LOC_GE_IMM:
                        case Instruction.SH_SREF_EMPTY:
                        case Instruction.SH_LOCALSREF_EMPTY:
                        case Instruction.SH_LOCALSREF_EQ_STR0:
                        case Instruction.SH_LOCALSREF_NE_STR0:
                        case Instruction.SH_STRUCTSREF_EMPTY:
                        case Instruction.SH_STRUCTSREF_EQ_LOCALSREF:
                        case Instruction.SH_STRUCTSREF_NE_LOCALSREF:
                        case Instruction.SH_STRUCTSREF_NE_STR0:
                            return new Variable(expression.ExpressionType.ToString(), DataType.Bool, -1, 0);
                            break;
                        //int:
                        case Instruction.SH_A_FIND_SREF:
                            return new Variable(expression.ExpressionType.ToString(), DataType.Int, -1, 0);
                            break;
                        //void:
                        case Instruction.SH_GLOBAL_A_PUSHBACK_LOCAL_STRUCT:
                        case Instruction.SH_GLOBAL_APUSHBACK_LOCALSREF:
                        case Instruction.SH_LOCAL_A_PUSHBACK_LOCAL_STRUCT:
                        case Instruction.SH_LOCAL_APUSHBACK_LOCALSREF:
                        case Instruction.SH_STRUCT_A_PUSHBACK_LOCAL_STRUCT:
                        case Instruction.SH_STRUCT_APUSHBACK_LOCALSREF:
                            return new Variable(expression.ExpressionType.ToString(), DataType.Void, -1, 0);
                            break;
                        case Instruction.SH_STRUCTREF_CALLMETHOD_NO_PARAM:
                        case Instruction.SH_STRUCTREF2_CALLMETHOD_NO_PARAM:
                            return ainFile.GetFunction(expression.Value2);
                            break;
                        case Instruction.THISCALLMETHOD_NOPARAM:
                            return ainFile.GetFunction(expression.Value);
                            break;
                        case Instruction.PAGE_REF:
                        case Instruction.S_REF2:
                            break;
                        case Instruction.DG_NEW_FROM_METHOD:
                            break;

                    }

                    if (expression.ExpressionType != Instruction.PAGE_REF && expression.ExpressionType != Instruction.S_REF2)
                    {


                        //if (CompositeInstructions.ExpandedInstructions.ContainsKey((int)expression.ExpressionType))
                        //{

                        //}
                        //if (expression.ExpressionType == Instruction.SH_STRUCTREF_CALLMETHOD_NO_PARAM || expression.ExpressionType == Instruction.SH_STRUCTREF2_CALLMETHOD_NO_PARAM)
                        //{
                        //    return ainFile.GetFunction(expression.Value2);
                        //}
                        //if (expression.ExpressionType == Instruction.THISCALLMETHOD_NOPARAM)
                        //{
                        //    return ainFile.GetFunction(expression.Value);
                        //}
                        //if (expression.ExpressionType == Instruction.SH_LOCAL_APUSHBACK_LOCALSREF)
                        //{
                        //    return null;
                        //}

                        var kinds = expression.ExpressionType.GetArgumentKinds();
                        var firstKind = kinds.FirstOrDefault();
                        if (firstKind == ArgumentKind.Local)
                        {
                            return ainFile.GetFunctionParameter(function, expression.Value);
                        }
                        else if (firstKind == ArgumentKind.LocalMember)
                        {
                            return ainFile.GetStructMember(localStructInfo, expression.Value);
                        }
                        else if (firstKind == ArgumentKind.Global)
                        {
                            return ainFile.GetGlobal(expression.Value);
                        }
                        else if (firstKind == ArgumentKind.Function)
                        {
                            return ainFile.GetFunction(expression.Value);
                        }
                        if (expression.ExpressionType == Instruction.SH_SR_ASSIGN)
                        {
                            return GetReferencedVariableType2(expression.Arg1, function, localStructInfo);
                        }

                        //FIXME
                        //return null;
                    }
                }

                if (expression.ExpressionType == Instruction.PassThroughExpression)
                {
                    //ignore POP and CALLSYS 3/4, and take first child
                    foreach (var arg in expression.Args)
                    {
                        if (arg != null && arg.ExpressionType != Instruction.POP && !(arg.ExpressionType == Instruction.CALLSYS && (arg.Value == 3 || arg.Value == 4)))
                        {
                            return GetReferencedVariableType2(arg, function, localStructInfo);
                        }
                    }
                }


                {
                    var variable = new Variable();
                    variable.DataType = expression.GetDataType();
                    variable.Name = expression.ExpressionType.ToString();
                    variable.Root = ainFile;
                    return variable;
                }
            }

            return null;
        }

        private HllFunction GetLibraryFunction(int libraryNumber, int functionNumber)
        {
            if (libraryNumber >= 0 && libraryNumber < ainFile.Libraries.Count)
            {
                var library = ainFile.Libraries[libraryNumber];
                var functions = library.Functions;
                if (functionNumber >= 0 && functionNumber < functions.Count)
                {
                    return functions[functionNumber];
                }
            }
            return null;
        }

        private string GetLibraryFunctionName(int libraryNumber, int functionNumber)
        {
            if (libraryNumber >= 0 && libraryNumber < ainFile.Libraries.Count)
            {
                var library = ainFile.Libraries[libraryNumber];
                var functions = library.Functions;
                if (functionNumber >= 0 && functionNumber < functions.Count)
                {
                    //return library.LibraryName + "." + functions[functionNumber].Name;
                    return library.GetActiveName() + "." + functions[functionNumber].GetActiveName();
                }
                //return library.LibraryName + "." + "INVALID_LIBRARY_FUNCTION[" + functionNumber.ToString() + "]";
                return library.GetActiveName() + "." + "INVALID_LIBRARY_FUNCTION[" + functionNumber.ToString() + "]";
            }
            return "INVALID_LIBRARY[" + libraryNumber.ToString() + "].INVALID_LIBRARY_FUNCTION[" + functionNumber.ToString() + "]";
        }

        private Variable GetStructMember(int structNumber, int structMemberNumber)
        {
            if (structNumber >= 0 && structNumber < ainFile.Structs.Count)
            {
                var structInfo = ainFile.Structs[structNumber];
                if (structMemberNumber >= 0 && structMemberNumber < structInfo.Members.Count)
                {
                    var member = structInfo.Members[structMemberNumber];
                    return member;
                }
            }
            return null;
        }


        public void PrintReferencedVariableName(Expression expression)
        {
            if (expression == null)
            {
                return;
            }
            var arg1 = expression.Arg1;
            var arg2 = expression.Arg2;

            //if (expression.ExpressionType == Instruction.PUSH)
            //{
            //    tw.Write(expression.Value.ToString());
            //}

            if (arg1 != null && arg1.ExpressionType == Instruction.PUSH)
            {
                var temp = arg2;
                arg2 = arg1;
                arg1 = temp;
            }

            if (arg1 != null && arg2 != null && arg2.ExpressionType == Instruction.PUSH)
            {
                int value = arg2.Value;
                if (arg1.ExpressionType == Instruction.PUSHGLOBALPAGE)
                {
                    PrintGlobalVariableName(value);
                    return;
                }
                if (arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                {
                    PrintLocalStructMemberName(value);
                    return;
                }
                if (arg1.ExpressionType == Instruction.PUSHLOCALPAGE)
                {
                    PrintLocalVariableName(value);
                    return;
                }
            }

            if (Expression.IsLeafRef(expression.ExpressionType))
            {
                int value = expression.Value;
                if (expression.ExpressionType == Instruction.SH_GLOBALREF)
                {
                    PrintGlobalVariableName(value);
                    return;
                }
                else if (expression.ExpressionType == Instruction.SH_STRUCTREF)
                {
                    PrintLocalStructMemberName(value);
                    return;
                }
                else
                {
                    PrintLocalVariableName(value);
                    return;
                }
            }

            if (arg1 != null && arg2 == null)
            {
                if (arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                {
                    tw.Write("this");
                    return;
                }
                PrintExpression(arg1);
                return;
            }

            if (expression.ExpressionType == Instruction.StructAssignmentRef)
            {
                //ignore "right", display left
                PrintReferencedVariableName(expression.Arg1);
                return;
            }

            //if (expression.ExpressionType == Instruction.REF && arg1 != null && arg1.ExpressionType == Instruction.REF)
            //{
            //    string variableName = GetReferencedVariableName(arg1);

            //    var left = GetReferencedVariable(arg1);
            //    return variableName;

            //}


            if (arg1 != null && arg2 != null)
            {
                var left = GetReferencedVariableType(arg1);
                var right = GetReferencedVariableType(arg2);

                if (left != null && left.ArrayDimensions > 0)
                {
                    CheckForVariableDeclaration(left, true);
                    CheckForVariableDeclaration(right, true);

                    PrintExpression(arg1);
                    tw.Write("[");
                    PrintExpression(arg2);
                    tw.Write("]");
                    return;
                }
                else if (left != null && (left.DataType == DataType.RefStruct || left.DataType == DataType.Struct) && left.StructType != -1)
                {
                    if (arg2.ExpressionType == Instruction.PUSH)
                    {
                        CheckForVariableDeclaration(left, true);
                        CheckForVariableDeclaration(right, true);

                        int value = arg2.Value;
                        string memberName = GetStructMemberName(left.StructType, value);
                        PrintExpression(arg1);
                        tw.Write("." + memberName);
                        return;
                    }
                }
                else if (left != null && (left.DataType == DataType.String || left.DataType == DataType.RefString))
                {
                    if (right != null && right.DataType == DataType.Int || right.DataType == DataType.RefInt)
                    {
                        CheckForVariableDeclaration(left, true);
                        CheckForVariableDeclaration(right, true);

                        PrintExpression(arg1);
                        tw.Write("[");
                        PrintExpression(arg2);
                        tw.Write("]");
                        return;
                    }
                }
            }

            if (expression.ExpressionType == Instruction.AssignmentRef && arg1 != null && arg1.ExpressionType == Instruction.PUSH && arg2 != null && arg2.ExpressionType == Instruction.PUSH)
            {
                if (arg1.Value == 0 && arg2.Value == -1)
                {
                    tw.Write("NULL");
                    return;
                }
            }

            {
                //all else failed, just print the two elements
                int lastColumn = tw.Column;
                int lastLineNumber = tw.LineNumber;
                PrintExpression(arg1);
                if (tw.Column != lastColumn || tw.LineNumber != lastLineNumber)
                {
                    tw.Write(" ");
                }
                PrintExpression(arg2);
            }
        }

        private void PrintLocalStructMemberName(int memberNumber)
        {
            tw.Write(GetLocalStructMemberName(memberNumber));
        }

        private void PrintGlobalVariableName(int variableNumber)
        {
            tw.Write(GetGlobalVariableName(variableNumber));
        }

        private void PrintLocalVariableName(int variableNumber)
        {
            if (DeclareVariables && !this.LocalsDefined.Contains(variableNumber))
            {
                this.LocalsDefined.Add(variableNumber);
                tw.Write(GetLocalVariableDeclaration(variableNumber));
            }
            else
            {
                tw.Write(GetLocalVariableName(variableNumber));
            }
        }

        string GetLocalVariableName(int variableNumber)
        {
            var localVariable = GetLocalVariable(variableNumber);
            if (localVariable != null)
            {
                //return localVariable.Name;
                return localVariable.GetActiveName();
            }
            else
            {
                return "INVALID_LOCAL[" + variableNumber.ToString() + "]";
            }
        }

        private string GetStructMemberName(int structType, int memberNumber)
        {
            var member = GetStructMember(structType, memberNumber);
            string memberName = "";
            if (member != null)
            {
                //memberName = member.Name;
                memberName = member.GetActiveName();
            }
            else
            {
                memberName = "INVALID_MEMBER[" + memberNumber.ToString() + "]";
            }
            return memberName;
        }

        static Dictionary<Expression, Variable> FindVariableDeclarations(Expression rootExpression)
        {
            var finder = new VariableDeclarationFinder(rootExpression);
            return finder.FindVariableDeclarations();
        }

        class VariableDeclarationFinder
        {
            //HashSet<Variable> VariablesUsedInThisScope = new HashSet<Variable>();
            //HashSet<Variable> VariablesUsedInChildScopes = new HashSet<Variable>();

            Dictionary<Expression, Expression> ParentOfScope = new Dictionary<Expression, Expression>();

            Dictionary<Variable, Expression> StatementOfFirstUse = new Dictionary<Variable, Expression>();
            Dictionary<Variable, Expression> ScopeOfFirstUse = new Dictionary<Variable, Expression>();
            Dictionary<Variable, Expression> StatementToDeclareAt = new Dictionary<Variable, Expression>();
            Expression rootExpression;
            public VariableDeclarationFinder(Expression rootExpression)
            {
                this.rootExpression = rootExpression;
            }
            void FindVariableDeclarations(Expression expression, Expression scope)
            {
                //want to declare variables if:
                //same variable appears in two child scopes,
                //or in a scope and in a child scope.

                Expression statement;
                for (statement = expression; statement != null; statement = statement.GetNextStatement(false, false))
                {
                    if (statement.ExpressionType == Instruction.Statement)
                    {
                        statement = statement.GetNextStatement(false, false);
                        if (statement == null)
                        {
                            break;
                        }
                    }
                    Expression nextStatementSameBlock = statement.GetNextStatement(false, false);
                    if (statement.IsBlock() && statement.ExpressionType != Instruction.FUNC)
                    {
                        Expression expressionToVisit = statement.GetNextStatement(true, false);
                        foreach (var e in statement.GetChildExpressions().Skip(1))
                        {
                            if (e == expressionToVisit || e == null || e.ExpressionType == Instruction.Statement)
                            {
                                break;
                            }
                            VisitExpression(e, statement, scope);
                        }

                        while (expressionToVisit != null && expressionToVisit != nextStatementSameBlock)
                        {
                            //recurse
                            if (!ParentOfScope.ContainsKey(expressionToVisit) && expressionToVisit != scope)
                            {
                                ParentOfScope[expressionToVisit] = scope;
                            }
                            FindVariableDeclarations(expressionToVisit, expressionToVisit);

                            var expressionToFind = expressionToVisit;
                            if (expressionToVisit.ExpressionType != Instruction.Statement && expressionToVisit.Parent != null && expressionToVisit.Parent.ExpressionType == Instruction.Statement)
                            {
                                expressionToFind = expressionToFind.Parent;
                            }


                            int idx = statement.Args.IndexOf(expressionToFind);
                            if (idx >= 0)
                            {
                                expressionToVisit = statement.Args.GetOrNull(idx + 1);
                            }
                            else
                            {
                                expressionToVisit = null;
                            }
                            if (expressionToVisit != null && expressionToVisit.ExpressionType == Instruction.Statement)
                            {
                                expressionToVisit = expressionToVisit.Arg1;
                            }
                        }

                        //VariablesUsedInThisScope = oldThisScope;
                        //VariablesUsedInChildScopes = oldChildScope;

                        //VariablesUsedInChildScopes.AddRange(newThisScope);
                        //VariablesUsedInChildScopes.AddRange(newChildScopes);
                    }
                    else
                    {
                        foreach (var e in statement.GetChildExpressions())
                        {
                            if (e == nextStatementSameBlock)
                            {
                                break;
                            }
                            //if (e.ExpressionType == Instruction.Statement)
                            //{
                            //    break;
                            //}
                            VisitExpression(e, statement, scope);
                            //if (notFirstUsage)
                            //{
                            //    if (foundVariable != null && this.StatementOfFirstUse[foundVariable] != statement && ScopeOfFirstUse[foundVariable] != thisScope)
                            //    {
                            //        var otherScope = ScopeOfFirstUse[foundVariable];
                            //    }
                            //}
                            //if (foundVariable != null && this.VariablesUsedInChildScopes.Contains(foundVariable))
                            //{

                            //}
                        }
                    }
                }
            }

            private void VisitExpression(Expression e, Expression statement, Expression scope)
            {
                if (e != null)
                {
                    if (e.ExpressionType != Instruction.MSG && e.ExpressionType != Instruction.S_PUSH)
                    {
                        var v = e.Variable;
                        if (v != null)
                        {
                            v = v.Canonicalize();
                        }
                        Variable v2 = v as Variable;
                        if (v2 != null && e.ExpressionType != Instruction.SH_LOCALDELETE)
                        {
                            var f = v2.Parent as Function;
                            if (f != null)
                            {
                                if (v2.Index >= f.ParameterCount)
                                {
                                    VisitVariable(statement, scope, v2);
                                }
                            }
                        }
                    }
                }
            }

            private void VisitVariable(Expression statement, Expression scope, Variable v2)
            {
                //VariablesUsedInThisScope.Add(v2);
                if (!this.StatementOfFirstUse.ContainsKey(v2) && !this.ScopeOfFirstUse.ContainsKey(v2))
                {
                    this.StatementOfFirstUse[v2] = statement;
                    this.ScopeOfFirstUse[v2] = scope;
                }
                else
                {
                    var scopeOfFirstUse = this.ScopeOfFirstUse[v2];
                    if (scopeOfFirstUse != scope)
                    {
                        //if "scope" is a child of "scopeOfFirstUse", then do nothing
                        var commonParent = GetCommonParentOfScope(scopeOfFirstUse, scope);
                        if (commonParent == null)
                        {
                            commonParent = (scopeOfFirstUse ?? scope).Root;
                            if (commonParent.ExpressionType == Instruction.FUNC)
                            {
                                commonParent = commonParent.Arg1;
                            }
                            if (commonParent.ExpressionType == Instruction.Statement)
                            {
                                commonParent = commonParent.Arg1;
                            }
                        }

                        if (commonParent == scopeOfFirstUse)
                        {
                            //do nothing
                        }
                        else
                        {
                            this.ScopeOfFirstUse[v2] = commonParent;

                            //look down scopeOfFirstUse to find commonParent, make the BLOCK into the new statement
                            var s = scopeOfFirstUse;
                            var newStatementOfFirstUse = commonParent;
                            if (s != commonParent)
                            {
                                while (ParentOfScope.ContainsKey(s) && ParentOfScope[s] != commonParent)
                                {
                                    s = ParentOfScope[s];
                                }
                                if (s.ExpressionType != Instruction.Statement)
                                {
                                    s = s.Parent;
                                }
                                if (s.ExpressionType == Instruction.Statement)
                                {
                                    s = s.Parent;
                                }
                                if (s.ExpressionType.IsBlock() && s.ExpressionType != Instruction.FUNC)
                                {
                                    newStatementOfFirstUse = s;
                                }
                            }
                            this.StatementOfFirstUse[v2] = newStatementOfFirstUse;
                        }
                    }
                }
            }

            Dictionary<Expression, Dictionary<Expression, Expression>> _commonParentOfScope = new Dictionary<Expression, Dictionary<Expression, Expression>>();

            Expression GetCommonParentOfScope(Expression scope1, Expression scope2)
            {
                if (scope1 == null || scope2 == null)
                {
                    return null;
                }
                var dic1 = _commonParentOfScope.GetOrAddNew(scope1);
                if (dic1.ContainsKey(scope2))
                {
                    return dic1[scope2];
                }
                var dic2 = _commonParentOfScope.GetOrAddNew(scope2);
                if (dic2.ContainsKey(scope1))
                {
                    return dic2[scope1];
                }

                var commonParent = FindCommonParentOfScope(scope1, scope2);
                dic1[scope2] = commonParent;
                dic2[scope1] = commonParent;
                return commonParent;
            }

            private Expression FindCommonParentOfScope(Expression scope1, Expression scope2)
            {
                HashSet<Expression> parentsOfScope1 = new HashSet<Expression>();
                parentsOfScope1.Add(scope1);
                Expression parent1;
                parent1 = ParentOfScope.GetOrNull(scope1);
                while (parent1 != null)
                {
                    if (!parentsOfScope1.Add(parent1))
                    {
                        break;
                    }
                    parent1 = ParentOfScope.GetOrNull(scope1);
                }

                if (parentsOfScope1.Contains(scope2))
                {
                    return scope2;
                }
                Expression parent2;
                parent2 = ParentOfScope.GetOrNull(scope2);
                while (parent2 != null)
                {
                    if (parentsOfScope1.Contains(parent2))
                    {
                        return parent2;
                    }
                    parent2 = ParentOfScope.GetOrNull(parent2);
                }
                return null;
            }


            public Dictionary<Expression, Variable> FindVariableDeclarations()
            {
                Expression expression = rootExpression;
                if (rootExpression.ExpressionType == Instruction.FUNC)
                {
                    rootExpression = rootExpression.Arg1;
                }
                FindVariableDeclarations(rootExpression, rootExpression);
                return this.StatementOfFirstUse.Inverse();
            }
        }

        int isTopExpression = 0;

        void PrintExpression(Expression expression)
        {
            isTopExpression--;
            if (expression == null)
            {
                return;
            }

            bool declareVariableNow = false;
            if (expression != null && this.ExpressionsToDeclareVariablesAt != null && this.ExpressionsToDeclareVariablesAt.ContainsKey(expression) && this.DeclareVariables)
            {
                declareVariableNow = true;
            }

            if (declareVariableNow)
            {
                bool wantToDeclare = false;
                var localToDeclare = this.ExpressionsToDeclareVariablesAt[expression];
                if (!LocalsDefined.Contains(localToDeclare.Index) && !localToDeclare.Name.StartsWith("<dummy"))
                {
                    if (expression.ExpressionType.IsBlock() && expression.ExpressionType != Instruction.FUNC)
                    {
                        wantToDeclare = true;
                    }
                    else if (expression.ExpressionType.IsAssignment() || expression.ExpressionType == Instruction.SH_LOCALASSIGN || expression.ExpressionType == Instruction.SH_LOCALCREATE)
                    {
                        wantToDeclare = false;
                    }
                    else if (expression.ExpressionType == Instruction.SH_LOCALDELETE || expression.ExpressionType == Instruction.A_FREE || expression.ExpressionType == Instruction.DG_CLEAR || expression.ExpressionType == Instruction.A_ALLOC)
                    {
                        wantToDeclare = false;
                    }
                    else if (expression.ExpressionType == Instruction.CALLMETHOD || expression.ExpressionType == Instruction.CALLFUNC || expression.ExpressionType == Instruction.CALLHLL)
                    {
                        wantToDeclare = true;
                    }
                    else
                    {
                        wantToDeclare = true;
                    }
                    if (wantToDeclare)
                    {
                        tw.WriteLine(GetVariableDeclaration(localToDeclare) + ";");
                        LocalsDefined.Add(localToDeclare.Index);
                    }
                }
            }

            tw.Flush();
            int startPosition = GetExpressionStartPosition();
            bool saveExpression = false;

            bool handled = true;
            if (expression.ExpressionType == Instruction.Statement)
            {
                PrintStatement(expression);
            }
            else if (OperatorTable.ContainsKey(expression.ExpressionType) && !OperatorsNotToHandle.Contains(expression.ExpressionType))
            {
                //check for <dummy> <- FunctionCall();
                if (expression.ExpressionType == Instruction.R_ASSIGN && expression.Arg1 != null)
                {
                    bool parentIsReturn = false;
                    var parent = expression.Parent;
                    if (parent != null)
                    {
                        parentIsReturn = parent.ExpressionType == Instruction.RETURN;
                    }
                    if (parentIsReturn || expression.IsVoidContext())
                    {
                        var v1 = expression.Arg1.Variable;
                        if (v1 != null)
                        {
                            v1 = v1.Canonicalize();
                        }
                        if (v1 != null)
                        {
                            if (v1.Name.StartsWith("<dummy"))
                            {
                                PrintExpression(expression.Arg2);
                                return;
                            }
                        }
                    }
                }

                PrintOperator(expression);
                saveExpression = true;
            }
            else
            {
                saveExpression = true;
                switch (expression.ExpressionType)
                {
                    case Instruction.CALLSYS:
                        {
                            bool hide = false;
                            if (!Verbose && (expression.Value == 3 || expression.Value == 4))
                            {
                                //hide = true;
                            }
                            if (hide)
                            {
                                handled = true;
                            }
                            else
                            {
                                var function = GetSystemCall(expression.Value);
                                if (function != null)
                                {
                                    tw.Write(function.Name);
                                    tw.Write("(");
                                    PrintCommaExpression(expression.Arg1, function, 0);
                                    //PrintExpression(expression.Arg1);
                                    tw.Write(")");
                                }
                                else
                                {
                                    handled = false;
                                }
                            }
                        }
                        break;
                    case Instruction.CALLFUNC:
                        {
                            int functionNumber = expression.Value;
                            var function = GetFunction(functionNumber);
                            string functionName = GetFunctionName(functionNumber);
                            tw.Write(functionName);
                            tw.Write("(");
                            PrintCommaExpression(expression.Arg1, function, 0);
                            //PrintExpression(expression.Arg1);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.CALLHLL:
                        {
                            var function = GetLibraryFunction(expression.Value, expression.Value2);
                            string functionName = GetLibraryFunctionName(expression.Value, expression.Value2);
                            tw.Write(functionName);
                            tw.Write("(");
                            PrintCommaExpression(expression.Arg1, function, 0);
                            //PrintExpression(expression.Arg1);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.CALLMETHOD:
                        {
                            bool thisHandled = false;
                            int functionNumber = expression.Value;
                            var function = GetFunction(functionNumber);
                            string methodName = GetMethodName(functionNumber);
                            if (methodName == "2")
                            {
                                break;
                            }

                            //bool wasComma = false;
                            var arg1 = expression.Arg1;
                            Expression firstArgument = null;
                            Expression objectWithMethod = arg1;
                            if (objectWithMethod.ExpressionType == Instruction.Comma)
                            {
                                firstArgument = objectWithMethod.Arg2;
                                objectWithMethod = objectWithMethod.Arg1;
                                //wasComma = true;
                            }

                            if (objectWithMethod != null && objectWithMethod.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                            {
                                if (ShowThis)
                                {
                                    tw.Write("this.");
                                }
                                tw.Write(methodName);
                                tw.Write("(");
                                PrintCommaExpression(firstArgument, function, 1);
                                //PrintExpression(firstArgument);
                                tw.Write(")");
                                thisHandled = true;
                            }
                            else
                            {
                                PrintExpression(objectWithMethod);
                                tw.Write("." + methodName);
                                tw.Write("(");
                                PrintCommaExpression(firstArgument, function, 1);
                                //PrintExpression(firstArgument);
                                tw.Write(")");
                                thisHandled = true;
                            }

                            if (!thisHandled)
                            {
                                handled = false;
                            }
                        }
                        break;
                    case Instruction.IfElse:
                        {
                            var ifBlock = expression.Arg2;
                            var elseBlock = expression.Arg3;
                            var condition = expression.Arg1;

                            if (isTopExpression > 0 || elseBlock == null)
                            {
                                tw.Write("if (");
                                PrintBoolExpression(condition);
                                tw.Write(")");

                                SaveExpression(expression, startPosition);
                                saveExpression = false;

                                tw.WriteLine();

                                PrintBlockExpression(ifBlock);

                                if (elseBlock != null)
                                {
                                    //tw.WriteLine();
                                    startPosition = GetExpressionStartPosition();
                                    tw.Write("else");
                                    SaveExpression(expression, startPosition);
                                    if (elseBlock.ExpressionType == Instruction.IfElse)
                                    {
                                        isTopExpression = 2;
                                        tw.Write(" ");
                                        PrintExpression(elseBlock);
                                    }
                                    else
                                    {
                                        tw.WriteLine();
                                        PrintBlockExpression(expression.Arg3);
                                    }
                                }
                            }
                            else
                            {
                                //it's a ? : operator
                                int precedence = 3;

                                Operator arg1op = GetOperator(expression.Arg1), arg2op = GetOperator(expression.Arg2), arg3op = GetOperator(expression.Arg3);
                                bool arg1ContainsLowerPrecedence = (arg1op != null && arg1op.precedence <= precedence);
                                bool arg2ContainsLowerPrecedence = (arg2op != null && arg2op.precedence <= precedence);
                                bool arg3ContainsLowerPrecedence = (arg3op != null && arg3op.precedence <= precedence);

                                if (arg1ContainsLowerPrecedence)
                                {
                                    tw.Write("(");
                                }
                                PrintExpression(condition);
                                if (arg1ContainsLowerPrecedence)
                                {
                                    tw.Write(")");
                                }
                                tw.Write(" ? ");

                                if (arg2ContainsLowerPrecedence)
                                {
                                    tw.Write("(");
                                }
                                PrintExpression(ifBlock);
                                if (arg2ContainsLowerPrecedence)
                                {
                                    tw.Write(")");
                                }
                                tw.Write(" : ");

                                if (arg3ContainsLowerPrecedence)
                                {
                                    tw.Write("(");
                                }
                                PrintExpression(elseBlock);
                                if (arg3ContainsLowerPrecedence)
                                {
                                    tw.Write(")");
                                }

                            }
                        }
                        break;
                    case Instruction.MSG:
                        tw.Write(EscapeAndQuoteMessage(ainFile.Messages[expression.Value]));
                        break;
                    case Instruction.PUSH:
                        {
                            bool handledInt = false;
                            //Check for a special int
                            //see if this is one side of an operator
                            var parent = expression.SkipParentCastOperations() ?? Expression.Empty;
                            if (parent.ExpressionType.IsOperator() && (parent.ExpressionType.IsBinary() || parent.ExpressionType.IsAssignment()))
                            {
                                var otherOperand = expression.GetOtherSideOfBinaryExpression();
                                if (otherOperand != null)
                                {
                                    PrintSpecialInt(expression, otherOperand);
                                    handledInt = true;
                                }
                            }
                            else if (parent.ExpressionType == Instruction.Comma || parent.ExpressionType.IsFunctionCall())
                            {
                                var parameter = expression.GetFunctionCallParameter();
                                PrintSpecialInt(expression, parameter);
                                handledInt = true;
                            }
                            else if (parent.ExpressionType == Instruction.RETURN) //rance quest doesn't use ITOB for this
                            {
                                var currentFunction = ainFile.GetFunction(this.currentFuncNumber);
                                if (currentFunction != null)
                                {
                                    PrintSpecialInt(expression, currentFunction);
                                    handledInt = true;
                                }
                            }
                            if (!handledInt)
                            {
                                PrintIntExpression(expression);
                            }
                        }
                        break;
                    case Instruction.S_PUSH:
                        {
                            tw.Write("\"" + GetString(expression.Value) + "\"");
                        }
                        break;
                    case Instruction.SJUMP:
                        {
                            bool thisHandled = false;
                            if (expression.Arg1 != null && expression.Arg1.ExpressionType == Instruction.CALLONJUMP)
                            {
                                var arg1 = expression.Arg1;
                                if (arg1.Arg1 != null)
                                {
                                    tw.Write("jump ");
                                    if (arg1.Arg1 != null && arg1.Arg1.ExpressionType == Instruction.S_PUSH)
                                    {
                                        string stringValue = GetString(arg1.Arg1.Value);
                                        if (ainFile.FunctionNameToIndex.ContainsKey(stringValue))
                                        {
                                            int callNumber = ainFile.FunctionNameToIndex[stringValue];
                                            int start2 = GetExpressionStartPosition();
                                            tw.Write(stringValue);
                                            var functionCall = new Expression(Instruction.CALLFUNC, arg1.Arg1.address, callNumber);
                                            SaveExpression(functionCall, start2);
                                        }
                                        else
                                        {
                                            tw.Write(stringValue);
                                        }
                                    }
                                    else
                                    {
                                        PrintExpression(arg1.Arg1);
                                    }
                                    thisHandled = true;
                                }

                            }
                            if (!thisHandled)
                            {
                                tw.Write("jumps ");
                                PrintExpression(expression.Arg1);
                            }
                        }
                        break;
                    case Instruction.SH_LOCALREF:
                    case Instruction.SH_GLOBALREF:
                    case Instruction.SH_STRUCTREF:
                    case Instruction.S_REF:
                    case Instruction.REF:
                    case Instruction.REFREF:
                    case Instruction.SR_REF:
                    case Instruction.SR_REFREF:
                    case Instruction.S_REF2:
                    case Instruction.PAGE_REF:
                    case Instruction.SR_REF2:
                    case Instruction.StructAssignmentRef:
                    case Instruction.AssignmentRef:
                    case Instruction.C_REF:
                    case Instruction.A_REF:
                        {
                            PrintReferencedVariableName(expression);
                        }
                        break;
                    case Instruction.SH_LOCALASSIGN:
                        {
                            PrintReferencedVariableName(expression);
                            tw.Write(" = ");
                            PrintSpecialInt(new Expression(Instruction.PUSH, expression.address, expression.Value2), expression);
                            //if (expression.Variable.DataType == DataType.Bool)
                            //{
                            //    tw.Write(" = ");
                            //    if (expression.Value2 == 0)
                            //    {
                            //        tw.Write("false");
                            //    }
                            //    else
                            //    {
                            //        tw.Write("true");
                            //    }
                            //}
                            //else if (expression.Variable.DataType.IsFunction())
                            //{
                            //    var function = GetFunction(expression.Value2);
                            //    if (function != null)
                            //    {
                            //        //tw.Write(" = &" + function.Name);
                            //        tw.Write(" = &" + function.GetActiveName());
                            //    }
                            //    else
                            //    {
                            //        tw.Write(" = " + expression.Value2.ToString());
                            //    }
                            //}
                            //else
                            //{
                            //    tw.Write(" = " + expression.Value2.ToString());
                            //}
                        }
                        break;
                    case Instruction.SH_LOCALINC:
                        {
                            tw.Write("++");
                            PrintReferencedVariableName(expression);
                        }
                        break;
                    case Instruction.SH_LOCALDEC:
                        {
                            tw.Write("--");
                            PrintReferencedVariableName(expression);
                        }
                        break;
                    case Instruction.SH_LOCALCREATE:
                        {
                            //if a localcreate is followed immediately by an assignment to that variable, display nothing
                            bool displayDeclaration = true;
                            var nextExpression = expression.GetNextExpression();
                            if (nextExpression != null && nextExpression.ExpressionType == Instruction.Statement)
                            {
                                nextExpression = nextExpression.GetNextExpression();
                            }
                            if (nextExpression != null && nextExpression.ExpressionType == Instruction.SR_ASSIGN)
                            {
                                if (nextExpression.Arg1.Arg1 != null && expression.Variable == nextExpression.Arg1.Arg1.Variable)
                                {
                                    displayDeclaration = false;
                                }
                            }


                            if (displayDeclaration && DeclareVariables)
                            {
                                PrintReferencedVariableName(expression);
                            }
                            else
                            {
                                saveExpression = false;
                            }

                            if (Verbose)
                            {
                                tw.Write(" = ");
                                handled = false;
                            }
                        }
                        break;
                    case Instruction.SH_LOCALDELETE:
                        {
                            if (Verbose)
                            {
                                handled = false;
                            }
                            else
                            {
                                //var nextExpression = expression.GetNextExpression();
                                //if (nextExpression != null && nextExpression.ExpressionType == Instruction.Statement)
                                //{
                                //    nextExpression = nextExpression.GetNextExpression();
                                //}
                                //if (nextExpression.ExpressionType == Instruction.SH_LOCALCREATE &&
                                //    expression.Value == nextExpression.Value && expression.Variable == nextExpression.Variable)
                                //{

                                //}
                                //else
                                //{
                                //    handled = false;
                                //}

                                //PrintReferencedVariableName(expression);
                                saveExpression = false;
                            }
                        }
                        break;
                    case Instruction.FUNC:
                        {
                            int functionNumber = expression.Value;
                            currentFuncNumber = functionNumber;
                            var function = GetFunction(functionNumber);
                            if (function != null)
                            {
                                PrintFunctionDeclaration(function);
                                //tw.Write(GetFunctionDeclaration(functionNumber));
                                SaveExpression(expression, startPosition);
                                saveExpression = false;

                                tw.WriteLine();

                                if (DeclareVariables)
                                {
                                    for (int i = 0; i < function.ParameterCount; i++)
                                    {
                                        this.LocalsDefined.Add(i);
                                    }
                                }
                            }

                            OpenBlock();
                            if (DeclareVariablesAtBeginning)
                            {
                                for (int i = function.ParameterCount; i < function.Parameters.Count; i++)
                                {
                                    tw.Write(GetDataTypeName(function.Parameters[i]) + " ");
                                    //tw.Write(function.Parameters[i].Name + ";");
                                    tw.Write(function.Parameters[i].GetActiveName() + ";");
                                    tw.WriteLine();
                                }
                            }
                            PrintExpression(expression.Arg1);
                            PrintLastSemicolonIfNeeded(expression.Arg1);
                            CloseBlock();

                            //PrintBlockExpression(expression.Arg1);
                            //OpenBlock();


                            //PrintExpression(expression.Arg1);
                            //CloseBlock();

                            //tw.WriteLine();

                        }
                        break;
                    //case Instruction.AssignmentRef:
                    //    {
                    //string variableName = GetReferencedVariableName(expression);
                    //if (variableName != null)
                    //{
                    //    tw.Write(variableName);
                    //}
                    //else
                    //{
                    //    handled = false;
                    //}

                    //bool thisHandled = false;
                    //string variableName = GetReferencedVariableName(expression);

                    //var variable = GetReferencedVariable(expression);
                    //if (variable != null)
                    //{
                    //    tw.Write(variable.Name);
                    //    thisHandled = true;
                    //}

                    //bool thisHandled = false;
                    //if (currentFuncNumber != -1)
                    //{
                    //    if (expression.Arg2 != null && expression.Arg2.ExpressionType == Instruction.PUSHLOCALPAGE &&
                    //        expression.Arg1 != null && expression.Arg1.ExpressionType == Instruction.push)
                    //    {
                    //        tw.Write(ainFile.Functions[currentFuncNumber].variables[expression.Arg1.Value].name);
                    //        thisHandled = true;
                    //    }

                    //    if (expression.Arg2 != null && expression.Arg2.ExpressionType == Instruction.PUSHSTRUCTPAGE &&
                    //        expression.Arg1 != null && expression.Arg1.ExpressionType == Instruction.push)
                    //    {
                    //        string structName = GetStructName();

                    //        if (ainFile.StructNameToIndex.ContainsKey(structName))
                    //        {
                    //            int structIndex = ainFile.StructNameToIndex[structName];
                    //            var structInfo = ainFile.Structs[structIndex];
                    //            string memberName = structInfo.members[expression.Arg1.Value].name;

                    //            tw.Write(memberName);
                    //            thisHandled = true;
                    //        }

                    //        //tw.Write(ainFile.Functions[currentFuncNumber].variables[expression.Arg1.Value].name);
                    //        //thisHandled = true;
                    //    }
                    //}
                    //if (!thisHandled)
                    //{
                    //    handled = false;
                    //}
                    //}
                    //break;
                    case Instruction.SWITCH:
                    case Instruction.STRSWITCH:
                        {
                            tw.Write("switch (");
                            PrintExpression(expression.Arg1);
                            tw.Write(")");
                            SaveExpression(expression, startPosition);
                            saveExpression = false;
                            tw.WriteLine();

                            var lastSwitchExpression = this.switchCaseExpression;
                            this.switchCaseExpression = expression;

                            OpenBlock();
                            tw.Indent++;
                            var caseBlock = expression.Arg2;
                            PrintExpression(caseBlock);
                            tw.Indent--;
                            CloseBlock();

                            this.switchCaseExpression = lastSwitchExpression;

                            //OpenBlock();
                            //var caseExpression = expression.Arg2;
                            //while (caseExpression != null && (caseExpression.ExpressionType == Instruction.SwitchCase || caseExpression.ExpressionType == Instruction.DefaultSwitchCase))
                            //{
                            //    var body = caseExpression.Arg1;

                            //    int startAddr = GetExpressionStartPosition();
                            //    if (caseExpression.ExpressionType == Instruction.SwitchCase)
                            //    {
                            //        var caseNumber = caseExpression.Value;
                            //        if (expression.ExpressionType == Instruction.SWITCH)
                            //        {
                            //            tw.Write("case " + caseNumber.ToString() + ":");
                            //        }
                            //        else
                            //        {
                            //            tw.Write("case \"" + EscapeString(GetString(caseNumber)) + "\":");
                            //        }
                            //    }
                            //    else if (caseExpression.ExpressionType == Instruction.DefaultSwitchCase)
                            //    {
                            //        tw.Write("default:");
                            //    }
                            //    SaveExpression(caseExpression, startAddr);

                            //    tw.Indent++;
                            //    tw.WriteLine();
                            //    PrintExpression(body);
                            //    PrintLastSemicolonIfNeeded(body);
                            //    tw.Indent--;
                            //    caseExpression = caseExpression.Arg2;
                            //}
                            ////if (expression.Arg2 != null)
                            ////{
                            ////    var body = expression.Arg2;
                            ////    tw.Write("default:");
                            ////    tw.Indent++;
                            ////    tw.WriteLine();

                            ////    PrintExpression(body);
                            ////    PrintLastSemicolonIfNeeded(body);
                            ////    //tw.Write("break;");
                            ////    tw.Indent--;
                            ////}
                            //CloseBlock();
                            ////tw.WriteLine();
                        }
                        break;
                    case Instruction.SwitchCase:
                        {
                            tw.Indent--;
                            int caseNumber = expression.Value;
                            int switchType = expression.Value2;
                            tw.Write("case ");
                            if (switchType != 4)
                            {
                                if (this.switchCaseExpression != null)
                                {
                                    PrintSpecialInt(new Expression(Instruction.PUSH, -1, expression.Value), this.switchCaseExpression.Arg1);
                                }
                                else
                                {
                                    PrintIntExpression(expression);
                                }
                            }
                            else
                            {
                                tw.Write(EscapeAndQuoteString(GetString(caseNumber)));
                            }
                            tw.Write(":");
                            tw.Indent++;
                        }
                        break;
                    case Instruction.DefaultSwitchCase:
                        {
                            tw.Indent--;
                            tw.Write("default:");
                            tw.Indent++;
                        }
                        break;
                    case Instruction.RETURN:
                        {
                            bool displayReturn = true;
                            if (RemoveReturns)
                            {
                                displayReturn = ShouldDisplayReturn(expression);
                            }
                            if (displayReturn)
                            {
                                tw.Write("return");
                                if (expression.Arg1 != null)
                                {
                                    tw.Write(" ");
                                    if (expression.Arg1.ExpressionType == Instruction.PUSH && GetCurrentFunction().DataType == DataType.RefStruct && expression.Arg1.Value == -1)
                                    {
                                        int startPos = GetExpressionStartPosition();
                                        tw.Write("NULL");
                                        SaveExpression(expression.Arg1, startPos);
                                    }
                                    else
                                    {
                                        PrintExpression(expression.Arg1);
                                    }
                                }
                            }
                            else
                            {
                                saveExpression = false;
                            }
                        }
                        break;
                    case Instruction.DG_NEW_FROM_METHOD:
                        {
                            //arg1 = object
                            //arg2 = method number of object
                            if (expression.Arg2.ExpressionType == Instruction.PUSH)
                            {
                                tw.Write("&");
                                var function = GetFunction(expression.Arg2.Value);
                                if (function != null)
                                {
                                    string functionName = function.GetFunctionName();
                                    if (expression.Arg1.ExpressionType == Instruction.PUSH && expression.Arg1.Value == -1)
                                    {
                                        //it's null, just use a function
                                        int pos = GetExpressionStartPosition();
                                        var newExpression = new Expression(Instruction.CALLFUNC, expression.Arg2.address, expression.Arg2.Value);
                                        tw.Write(functionName);
                                        SaveExpression(newExpression, pos);
                                    }
                                    else if (expression.Arg1.Variable.DataType.IsStruct())
                                    {

                                        PrintExpression(expression.Arg1);
                                        tw.Write(".");
                                        int pos = GetExpressionStartPosition();
                                        var newExpression = new Expression(Instruction.CALLMETHOD, expression.Arg2.address, expression.Arg2.Value);
                                        tw.Write(functionName);
                                        SaveExpression(newExpression, pos);
                                    }
                                    else
                                    {
                                        //what other cases do we possibly have?
                                    }
                                }
                            }
                            else if (expression.Arg2.ExpressionType == Instruction.DG_STR_TO_METHOD)
                            {
                                if (expression.Arg1.ExpressionType == Instruction.PUSH && expression.Arg1.Value == -1)
                                {
                                    //string to function name, output the string variable
                                    PrintExpression(expression.Arg2.Arg1);
                                }
                                else
                                {
                                    //???
                                }
                            }
                            else
                            {
                                //any other possible cases?
                            }
                        }
                        break;
                    case Instruction.ITOLI:
                        {
                            PrintExpression(expression.Arg1);
                        }
                        break;
                    case Instruction.FTOI:
                    case Instruction.ITOF:
                    //case Instruction.ITOLI:
                    case Instruction.FTOS:
                    case Instruction.STOI:
                        //case Instruction.DG_COPY:
                        {
                            if (ShowCasting || Verbose)
                            {
                                string dataTypeName = GetDataTypeName(expression.GetDataType(), -1, 0);
                                tw.Write(dataTypeName);
                                tw.Write("(");
                                PrintExpression(expression.Arg1);
                                tw.Write(")");
                            }
                            else
                            {
                                PrintExpression(expression.Arg1);
                            }

                            //if (expression.Arg1 != null)
                            //{
                            //    if (ShowCasting)
                            //    {
                            //        if (expression.ExpressionType == Instruction.FTOI)
                            //        {
                            //            tw.Write("int");
                            //        }
                            //        else if (expression.ExpressionType == Instruction.ITOF)
                            //        {
                            //            tw.Write("float");
                            //        }
                            //    }
                            //    if (OperatorTable.ContainsKey(expression.Arg1.ExpressionType))
                            //    {
                            //        tw.Write("(");
                            //        PrintExpression(expression.Arg1);
                            //        tw.Write(")");
                            //    }
                            //    else
                            //    {
                            //        if (ShowCasting)
                            //        {
                            //            tw.Write("(");
                            //        }
                            //        PrintExpression(expression.Arg1);
                            //        if (ShowCasting)
                            //        {
                            //            tw.Write(")");
                            //        }
                            //    }
                            //}
                            //do nothing
                            //PrintExpression(expression.Arg1);
                        }
                        break;
                    case Instruction.DG_COPY:
                        {
                            //do nothing
                            PrintExpression(expression.Arg1);
                        }
                        break;
                    case Instruction.F_PUSH:
                        {
                            float floatValue = BitConverter.ToSingle(BitConverter.GetBytes(expression.Value), 0);
                            string floatString = floatValue.FloatToString();
                            if (!floatString.Contains('.'))
                            {
                                floatString += ".0";
                            }
                            tw.Write(floatString);
                        }
                        break;
                    case Instruction.A_NUMOF:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Numof(");
                            var variable = GetReferencedVariableType(expression.Arg1);
                            if (variable.ArrayDimensions > 1 && !(expression.Arg2.ExpressionType == Instruction.PUSH && expression.Arg2.Value == 1))
                            {
                                PrintExpression(expression.Arg2);
                            }
                            tw.Write(")");
                            break;
                        }
                    case Instruction.ArrayIndex:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write("[");
                            PrintExpression(expression.Arg2);
                            tw.Write("]");
                        }
                        break;
                    //case Instruction.A_REALLOC:
                    //    {
                    //        PrintExpression(expression.Arg1);
                    //        tw.Write(".Realloc(");
                    //        PrintExpression(expression.Arg2);
                    //        tw.Write(")");
                    //    }
                    //    break;
                    case Instruction.A_FREE:
                        {
                            //if this is a declaration, do not display ".Free()"
                            bool displayFree = true;

                            var variableToFree = GetReferencedVariableType(expression.Arg1).Canonicalize();
                            if (DeclareVariables)
                            {
                                bool needToDeclare = CheckForVariableDeclarations(expression, false);
                                if (needToDeclare)
                                {
                                    CheckForVariableDeclarations(expression, true);
                                    break;
                                }
                            }

                            //If this a local variable destruction, do not display the statement at all
                            //is this a local variable?
                            if (displayFree == true && expression.Arg1.Variable.Parent is Function && this.DeclareVariables)
                            {
                                //look at next statements
                                Expression e = expression;
                                //Expression block = null;
                                bool onlySawDestruction = true;
                                while (e != null)
                                {
                                    if (e.ExpressionType == Instruction.SH_LOCALDELETE || e.ExpressionType == Instruction.A_FREE || e.ExpressionType == Instruction.DG_CLEAR)
                                    {

                                    }
                                    else if (e.ExpressionType == Instruction.Break || e.ExpressionType == Instruction.Continue)
                                    {
                                        break;
                                    }
                                    else if (e.ExpressionType == Instruction.Goto)
                                    {
                                        //TODO: check of the destination is out of scope
                                        break;
                                    }
                                    else
                                    {
                                        onlySawDestruction = false;
                                        break;
                                    }
                                    var nextStatement = e.GetNextStatement(true, false);
                                    if (nextStatement == null)
                                    {
                                        break;
                                    }
                                    e = nextStatement;
                                }
                                if (onlySawDestruction)
                                {
                                    bool variableExistsInThisScope = LocalsDefined.Contains(variableToFree.Index);
                                    bool variableExistsInParentScope = false;
                                    if (LocalsDefinedStack.Count > 0)
                                    {
                                        var stackPeek = LocalsDefinedStack.Peek();
                                        if (stackPeek.Contains(variableToFree.Index))
                                        {
                                            variableExistsInParentScope = true;
                                        }
                                    }

                                    if (variableExistsInThisScope && !variableExistsInParentScope)
                                    {
                                        displayFree = false;
                                    }
                                }
                            }


                            if (displayFree)
                            {
                                PrintExpression(expression.Arg1);
                                tw.Write(".Free()");
                            }
                            else
                            {
                                saveExpression = false;
                            }
                        }
                        break;
                    case Instruction.A_COPY:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Copy(");
                            //arg2 is a comma, displays all 4 arguments
                            PrintExpression(expression.Arg2);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.A_PUSHBACK:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".PushBack(");
                            PrintExpression(expression.Arg2);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.A_POPBACK:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".PopBack()");
                        }
                        break;
                    case Instruction.ITOB:
                        {
                            PrintBoolExpression(expression.Arg1);
                        }
                        break;
                    case Instruction.WhileLoop:
                        {
                            this.BanStatements = true;
                            tw.Write("while (");
                            PrintBoolExpression(expression.Arg1);
                            tw.Write(")");
                            this.BanStatements = false;
                            SaveExpression(expression, startPosition);
                            saveExpression = false;
                            tw.WriteLine();
                            //OpenBlock();
                            PrintBlockExpression(expression.Arg2);
                            //CloseBlock();
                            //tw.WriteLine();
                        }
                        break;
                    case Instruction.DoWhileLoop:
                        {
                            tw.Write("do");
                            tw.WriteLine();

                            PrintBlockExpression(expression.Arg2, null);
                            //still on same line, can write the while now
                            tw.Write(" while (");
                            this.BanStatements = true;
                            PrintBoolExpression(expression.Arg1);
                            this.BanStatements = false;
                            tw.Write(");");
                            tw.WriteLine();
                        }
                        break;
                    case Instruction.ForLoop:
                        {
                            this.BanStatements = true;
                            CheckForVariableDeclarations(expression.Arg1, true);
                            tw.Write("for (");
                            PrintExpression(expression.Arg1);
                            tw.Write("; ");
                            PrintBoolExpression(expression.Arg2);
                            tw.Write("; ");
                            PrintExpression(expression.Arg3);
                            tw.Write(")");
                            SaveExpression(expression, startPosition);
                            saveExpression = false;
                            tw.WriteLine();
                            this.BanStatements = false;

                            //OpenBlock();
                            PrintBlockExpression(expression.Arg4);
                            //CloseBlock();
                            //tw.WriteLine();
                        }
                        break;
                    case Instruction.Break:
                        tw.Write("break");
                        break;
                    case Instruction.Continue:
                        tw.Write("continue");
                        break;
                    case Instruction.Nop:
                        break;
                    case Instruction.PassThroughExpression:
                        {
                            foreach (var arg in expression.Args)
                            {
                                if (arg.ExpressionType != Instruction.POP)
                                {
                                    PrintExpression(arg);
                                }
                            }
                        }
                        break;
                    case Instruction.DELETE:
                        if (Verbose)
                        {
                            handled = false;
                        }
                        break;
                    case Instruction.S_LENGTHBYTE:
                    case Instruction.S_LENGTHBYTE2:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".LengthByte()");
                        }
                        break;
                    case Instruction.A_EMPTY:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Empty()");
                        }
                        break;
                    case Instruction.I_STRING:
                        {
                            tw.Write("string(");
                            PrintExpression(expression.Arg1);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.S_LENGTH:
                    case Instruction.S_LENGTH2:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Length()");
                        }
                        break;
                    case Instruction.S_PUSHBACK:
                    case Instruction.S_PUSHBACK2:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".PushBack(");
                            PrintExpression(expression.Arg2);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.S_POPBACK:
                    case Instruction.S_POPBACK2:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".PopBack()");
                        }
                        break;
                    case Instruction.A_ALLOC:
                    case Instruction.A_REALLOC:
                        {
                            bool declaring = false;
                            if (expression.ExpressionType == Instruction.A_ALLOC)
                            {
                                declaring = CheckForVariableDeclarations(expression.Arg1, false);
                            }
                            else
                            {
                                CheckForVariableDeclarations(expression.Arg1, true);
                            }
                            PrintExpression(expression.Arg1);
                            if (declaring && expression.ExpressionType == Instruction.A_ALLOC)
                            {
                                for (int i = 1; i < expression.Args.Count; i++)
                                {
                                    tw.Write("[");
                                    PrintExpression(expression.Args[i]);
                                    tw.Write("]");
                                }
                            }
                            else
                            {
                                if (expression.ExpressionType == Instruction.A_ALLOC)
                                {
                                    tw.Write(".Alloc(");
                                }
                                else if (expression.ExpressionType == Instruction.A_REALLOC)
                                {
                                    tw.Write(".Realloc(");
                                }
                                if (expression.Args.Count > 2)
                                {

                                }
                                for (int i = 1; i < expression.Args.Count; i++)
                                {
                                    PrintExpression(expression.Args[i]);
                                    if (i != expression.Args.Count - 1)
                                    {
                                        tw.Write(", ");
                                    }
                                }
                                tw.Write(")");
                            }
                        }
                        break;
                    case Instruction.ASSERT:
                        {
                            //if (expression.Arg2.ExpressionType == Instruction.S_PUSH)
                            //{
                            //    tw.Write("assert(" + expression.Arg2 + ")");
                            //}
                            //else
                            //{
                            tw.Write("assert(");
                            PrintExpression(expression.Arg1);
                            tw.Write(")");
                            //}
                            if (Verbose)
                            {
                                tw.Write("\t/* original code: ");
                                PrintExpression(expression.Arg2);
                                tw.Write(" filename: ");
                                PrintExpression(expression.Arg3);
                                tw.Write(" line number: ");
                                PrintExpression(expression.Arg4);
                                tw.Write("*/");
                            }
                        }
                        break;
                    case Instruction.S_FIND:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Find(");
                            PrintExpression(expression.Arg2);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.S_GETPART:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".GetPart(");
                            PrintExpression(expression.Arg2);
                            tw.Write(", ");
                            PrintExpression(expression.Arg3);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.A_INSERT:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Insert(");
                            PrintExpression(expression.Arg2);
                            tw.Write(", ");
                            PrintExpression(expression.Arg3);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.CALLFUNC2:
                        {
                            int funcTypeIndex = expression.Arg2.Value;
                            var functionPointer = expression.Arg1;
                            var args = expression.Arg3;
                            //var funcType = ainFile.FunctionTypes[funcTypeIndex];
                            //int argumentCount = funcType.functionArguments;

                            PrintExpression(functionPointer);
                            tw.Write("(");
                            PrintExpression(args);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.DG_CALLBEGIN:
                        {
                            int delegateIndex = expression.Value;
                            var functionPointer = expression.Arg2;
                            var args = expression.Arg1;
                            //var delg = ainFile.Delegates[delegateIndex];
                            //int argumentCount = delg.functionArguments;

                            PrintExpression(functionPointer);
                            tw.Write("(");
                            PrintExpression(args);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.S_EMPTY:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Empty()");
                        }
                        break;
                    case Instruction.R_ASSIGN:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(" = ");
                            PrintExpression(expression.Arg2);
                        }
                        break;
                    case Instruction.FT_ASSIGNS:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(" = ");
                            PrintExpression(expression.Arg2);
                            //arg3 = function pointer type
                        }
                        break;
                    case Instruction.Label:
                        {
                            tw.Indent--;
                            tw.Write("label" + expression.Value.ToString() + ":");
                            tw.Indent++;
                        }
                        break;
                    case Instruction.Goto:
                        {
                            tw.Write("goto label" + expression.Value.ToString());
                        }
                        break;
                    case Instruction.A_FILL:
                        {
                            //array, start, length, value
                            PrintExpression(expression.Arg1);
                            tw.Write(".Fill(");
                            PrintExpression(expression.Arg2);
                            tw.Write(", ");
                            PrintExpression(expression.Arg3);
                            tw.Write(", ");
                            PrintExpression(expression.Arg4);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.A_SORT:
                        {
                            //array, function number (int value)
                            PrintExpression(expression.Arg1);
                            tw.Write(".Sort(");
                            if (expression.Arg2.ExpressionType == Instruction.PUSH)
                            {
                                int value = expression.Arg2.Value;
                                if (value > 0)
                                {
                                    tw.Write("&");
                                }
                                string functionName = GetFunctionName(value);
                                tw.Write(functionName);
                            }
                            else
                            {
                                PrintExpression(expression.Arg2);
                            }
                            tw.Write(")");
                        }
                        break;
                    case Instruction.A_REVERSE:
                        {
                            //array
                            PrintExpression(expression.Arg1);
                            tw.Write(".Reverse()");
                        }
                        break;
                    //case Instruction.STOI:
                    //    {
                    //        //string
                    //        PrintExpression(expression.Arg1);
                    //        tw.Write(".Int()");
                    //    }
                    //    break;
                    case Instruction.A_ERASE:
                    case Instruction.S_ERASE:
                    case Instruction.S_ERASE2:
                        {
                            //string/array, index to erase at
                            PrintExpression(expression.Arg1);
                            tw.Write(".Erase(");
                            PrintExpression(expression.Arg2);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.OBJSWAP:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(" <=> ");
                            PrintExpression(expression.Arg2);
                        }
                        break;
                    case Instruction.C_ASSIGN:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write("[");
                            PrintExpression(expression.Arg2);
                            tw.Write("]");
                            tw.Write(" = ");
                            var charExpression = expression.Arg3;
                            if (charExpression != null && charExpression.ExpressionType == Instruction.PUSH)
                            {
                                PrintChar(charExpression);
                            }
                            else
                            {
                                PrintExpression(charExpression);
                            }
                        }
                        break;
                    case Instruction.A_FIND:
                        {
                            PrintExpression(expression.Arg1);  //array
                            tw.Write(".Find(");
                            PrintExpression(expression.Arg2);  //startIndex
                            tw.Write(", ");
                            PrintExpression(expression.Arg3);  //endIndex
                            tw.Write(", ");
                            PrintExpression(expression.Arg4);  //lookFor
                            var arg5 = expression.Args[4]; //comparison function
                            if (arg5.ExpressionType == Instruction.PUSH && arg5.Value == 0 && !Verbose)
                            {
                                //omit &NULL?  Have no source code to see if this is how it should appear.
                            }
                            else
                            {
                                tw.Write(", ");
                                if (arg5.ExpressionType == Instruction.PUSH)
                                {
                                    int value = arg5.Value;
                                    if (value > 0)
                                    {
                                        tw.Write("&");
                                    }
                                    string functionName = GetFunctionName(value);
                                    int startPos = GetExpressionStartPosition();
                                    tw.Write(functionName);
                                    SaveExpression(new Expression(Instruction.CALLFUNC, arg5.address, arg5.Value), startPos);
                                }
                                else
                                {
                                    PrintExpression(expression.Arg2);
                                }
                            }
                            tw.Write(")");
                        }
                        break;
                    case Instruction.SH_SR_ASSIGN:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(" = ");
                            PrintExpression(expression.Arg2);
                        }
                        break;
                    case Instruction.PUSHSTRUCTPAGE:
                        {
                            tw.Write("this");
                        }
                        break;
                    case Instruction.NEW:
                        {
                            tw.Write("new ");
                            var arg1 = expression.Arg1;
                            if (arg1 != null && arg1.ExpressionType == Instruction.PUSH)
                            {
                                int structIndex = arg1.Value;
                                if (structIndex >= 0 && structIndex < ainFile.Structs.Count)
                                {
                                    var structInfo = ainFile.Structs[structIndex];
                                    tw.Write(structInfo.GetActiveName());


                                    //var constructor = structInfo.GetConstructor();
                                    //if (constructor != null)
                                    //{
                                    //    var newExpression = new Expression(Instruction.CALLFUNC, expression.address, constructor.Index);
                                    //    int startPosition2 = GetExpressionStartPosition();
                                    //    //tw.Write(structInfo.Name + "()");
                                    //    tw.Write(structInfo.GetActiveName() + "()");
                                    //    SaveExpression(newExpression, startPosition2);
                                    //}
                                }

                            }
                            else
                            {

                            }
                        }
                        break;
                    case Instruction.PUSHLOCALPAGE:
                    case Instruction.PUSHGLOBALPAGE:
                    case Instruction.IFZ:
                    case Instruction.IFNZ:
                    case Instruction.JUMP:
                        {
                            var foo = expression.ExpressionType;
                            handled = false;
                        }
                        break;
                    case Instruction.DG_ASSIGN:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(" = ");
                            PrintExpression(expression.Arg2);
                        }
                        break;
                    case Instruction.DG_SET:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(" = ");
                            tw.Write("&");
                            PrintExpression(expression.Arg2);
                            tw.Write(".");
                            int functionNumber = expression.Arg3.Value;
                            var function = GetFunction(functionNumber);
                            if (function != null)
                            {
                                int pos = GetExpressionStartPosition();
                                string functionName = function.GetFunctionName();
                                tw.Write(functionName);
                                var exp = new Expression(Instruction.CALLMETHOD, expression.Arg3.address, functionNumber);
                                SaveExpression(exp, pos);
                            }
                        }
                        break;
                    case Instruction.DG_PLUSA:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(" += ");
                            PrintExpression(expression.Arg2);
                        }
                        break;
                    case Instruction.DG_MINUSA:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(" -= ");
                            PrintExpression(expression.Arg2);
                        }
                        break;
                    case Instruction.DG_NUMOF:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Numof()");
                        }
                        break;
                    case Instruction.DG_CLEAR:
                        {
                            if (CheckForVariableDeclarations(expression.Arg1, true))
                            {

                            }
                            else
                            {
                                PrintExpression(expression.Arg1);
                                tw.Write(".Clear()");
                            }
                        }
                        break;
                    case Instruction.DG_ADD:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Add(");
                            PrintDelegateExpression(expression);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.DG_ERASE:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Erase(");
                            PrintDelegateExpression(expression);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.DG_EXIST:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Exist(");
                            PrintDelegateExpression(expression);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.DG_NEW:
                        {
                            tw.Write("NULL");
                        }
                        break;
                    case Instruction.A_SORT_MEM:
                        {
                            PrintExpression(expression.Arg1);
                            tw.Write(".Sort(");

                            int memberNumber = expression.Arg2.Value;
                            string memberNameString = memberNumber.ToString();
                            var variable = expression.Arg1.Variable;
                            Expression expression2 = expression.Arg2;
                            if (variable != null && expression.Arg2.ExpressionType == Instruction.PUSH)
                            {
                                var structType = variable.GetStructType();
                                if (structType != null && memberNumber >= 0 && memberNumber < structType.Members.Count)
                                {
                                    //var variableExpressionCopy = expression.Arg1.Clone();
                                    //var arrayIndexExpression = new Expression(Instruction.PUSH, expression2.address, 0);
                                    //var memberIndexExpression = new Expression(Instruction.PUSH, expression2.address, expression2.Value);
                                    //var memberExpression = new Expression(Instruction.REF, expression.address, variableExpressionCopy, memberIndexExpression);

                                    //tw.Write("&");
                                    //PrintReferencedVariableName(memberExpression);

                                    //expression2 = new Expression(Instruction.PrintStructType, expression2.address, structType.Index, memberNumber);
                                    ////memberNameString = "&" + structType.Members[memberNumber].Name;
                                    memberNameString = "&" + structType.GetActiveName() + "." + structType.Members[memberNumber].GetActiveName();
                                }
                            }
                            int startPos = GetExpressionStartPosition();
                            tw.Write(memberNameString);
                            SaveExpression(expression2, startPos);
                            tw.Write(")");
                        }
                        break;
                    case Instruction.RefAssignment:
                    case Instruction.RefIntAssignment:
                    case Instruction.RefIntInitializer:
                    case Instruction.RefInitializer:
                        {
                            if (expression.Arg2 == null || (expression.Arg2.ExpressionType == Instruction.PUSH && expression.Arg2.Value == -1))
                            {
                                var variable = expression.Arg1.Variable;
                                if (variable != null && (variable is Global || variable.Parent is Struct))
                                {
                                    PrintReferencedVariableName(expression.Arg1);
                                    tw.Write(" <- NULL");
                                }
                                else
                                {
                                    if (DeclareVariables && !this.LocalsDefined.Contains(variable.Index))
                                    {
                                        PrintReferencedVariableName(expression.Arg1);
                                        if (expression.Value == 2)
                                        {
                                            tw.Write(" = NULL");
                                        }
                                    }
                                    else
                                    {
                                        PrintReferencedVariableName(expression.Arg1);
                                        tw.Write(" <- NULL");
                                    }
                                }
                            }
                            else
                            {
                                if (expression.Arg2.ExpressionType == Instruction.R_ASSIGN && expression.Arg2.Variable.Name.StartsWith("<dummy"))
                                {
                                    PrintReferencedVariableName(expression.Arg1);
                                    tw.Write(" ");
                                    tw.Write(OperatorTable[expression.ExpressionType].symbol);
                                    tw.Write(" ");
                                    PrintExpression(expression.Arg2.Arg2);
                                }
                                else
                                {
                                    PrintOperator(expression);
                                }
                            }
                        }
                        break;
                    default:
                        {
                            var foo = expression.ExpressionType;

                            handled = false;
                        }
                        break;
                }
            }

            if (!handled)
            {
                tw.Write(expression.ExpressionType.ToString());
                tw.Write("(");
                int totalWords = expression.ExpressionType.GetNumberOfWords();
                bool needComma = false;
                if (totalWords >= 1)
                {
                    Util.PrintComma(tw, ref needComma);
                    tw.Write(expression.Value);
                }
                if (totalWords >= 2)
                {
                    Util.PrintComma(tw, ref needComma);
                    tw.Write(expression.Value2);
                }
                if (totalWords >= 3)
                {
                    Util.PrintComma(tw, ref needComma);
                    tw.Write(expression.Value3);
                }
                int totalArguments = expression.Args.Count;
                for (int i = 0; i < totalArguments; i++)
                {
                    Util.PrintComma(tw, ref needComma);
                    PrintExpression(expression.Args[i]);
                }
                tw.Write(")");
            }

            if (saveExpression)
            {
                SaveExpression(expression, startPosition);
            }
        }

        private void PrintDelegateExpression(Expression expression)
        {
            if (expression.Arg3.ExpressionType == Instruction.DG_STR_TO_METHOD)
            {
                if (expression.Arg2.ExpressionType != Instruction.PUSH && expression.Arg2.Value != -1)
                {
                    //TODO: something besides NULL here
                }
                var a3 = expression.Arg3;
                PrintExpression(a3.Arg1);
            }
            else if (expression.Arg3.ExpressionType == Instruction.PUSH)
            {
                var function = ainFile.GetFunction(expression.Arg3.Value);
                tw.Write("&");
                if (expression.Arg2.ExpressionType != Instruction.PUSH && expression.Arg2.Value != -1)
                {
                    PrintExpression(expression.Arg2);
                    tw.Write(".");
                    tw.Write(function.GetFunctionName());
                }
                else
                {
                    tw.Write(function.Name);
                }
            }
            else
            {
                PrintExpression(expression.Arg3);
            }
        }

        private bool CheckForVariableDeclarations(Expression expression, bool outputDeclarations)
        {
            if (expression == null || !DeclareVariables) return false;
            foreach (var e in Enumerable.Repeat(expression, 1).Concat(expression.GetChildExpressions()))
            {
                if (e != null)
                {
                    var v = e.Variable;
                    bool handled = CheckForVariableDeclaration(v, outputDeclarations);
                    if (handled)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CheckForVariableDeclaration(IVariable v, bool outputDeclarations)
        {
            if (v != null)
            {
                v = v.Canonicalize();
                if (v != null && v.Parent is Function)
                {
                    if (DeclareVariables && !this.LocalsDefined.Contains(v.Index))
                    {
                        if (outputDeclarations && !v.Name.StartsWith("<dummy"))
                        {
                            this.LocalsDefined.Add(v.Index);
                            tw.Write(GetLocalVariableDeclaration(v.Index));
                            tw.WriteLine(";");
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        bool DefaultValueMatches(Expression expression, IFunction function, int parameterIndex)
        {
            var parameter = function.GetNonVoidFunctionParameter(parameterIndex);
            if (parameter != null)
            {
                var defaultValue = parameter.GetDefaultValue();
                if (defaultValue != null)
                {
                    var e2 = expression.SkipChildCastOperations();
                    switch (e2.ExpressionType)
                    {
                        case Instruction.PUSH:
                            if (e2.Value == defaultValue.IntValue)
                            {
                                return true;
                            }
                            break;
                        case Instruction.S_PUSH:
                            if (ainFile.GetString(e2.Value) == defaultValue.StringValue)
                            {
                                return true;
                            }
                            break;
                        case Instruction.F_PUSH:
                            float floatValue = BitConverter.ToSingle(BitConverter.GetBytes(e2.Value), 0);
                            if (floatValue == defaultValue.FloatValue)
                            {
                                return true;
                            }
                            break;
                    }
                }
            }
            return false;
        }

        private void PrintCommaExpression(Expression firstArgument, IFunction function, int parameterIndex)
        {
            //invalid argument checks
            if (firstArgument == null)
            {
                return;
            }
            if (function == null)
            {
                PrintExpression(firstArgument);
                return;
            }

            //get which parameters match defaults
            List<bool> matches = new List<bool>(function.ParameterCount);
            {
                int i = parameterIndex;
                var e = firstArgument;
                while (e.ExpressionType == Instruction.Comma)
                {
                    var arg1 = e.Arg1;
                    if (DefaultValueMatches(arg1, function, i - parameterIndex))
                    {
                        matches.Add(true);
                    }
                    else
                    {
                        matches.Add(false);
                    }
                    e = e.Arg2;
                    i++;
                }
                if (DefaultValueMatches(e, function, i - parameterIndex))
                {
                    matches.Add(true);
                }
                else
                {
                    matches.Add(false);
                }
            }
            if (matches.AnyEqualTo(true))
            {
                //filter: remove any trues before the last false to avoid empty entries in the argument list
                int lastFalseIndex = matches.LastIndexOf(false);
                if (lastFalseIndex >= 0)
                {
                    for (int matchIndex = 0; matchIndex < lastFalseIndex; matchIndex++)
                    {
                        matches[matchIndex] = false;
                    }
                }

                int i = 0;
                var e = firstArgument;
                bool allRemainingMatch = matches.FirstOrDefault();
                while (e.ExpressionType == Instruction.Comma)
                {
                    bool thisMatches = matches[i];
                    if (!thisMatches)
                    {
                        var arg1 = e.Arg1;
                        PrintExpression(arg1);
                    }
                    allRemainingMatch = matches.Skip(i + 1).AllEqualTo(true);
                    if (allRemainingMatch)
                    {
                        break;
                    }
                    tw.Write(", ");
                    e = e.Arg2;
                    i++;
                }
                if (!allRemainingMatch)
                {
                    PrintExpression(e);
                }
            }
            else
            {
                PrintExpression(firstArgument);
            }
        }

        private void PrintBoolExpression(Expression expression)
        {
            if (expression != null && (expression.ExpressionType == Instruction.PUSH))
            {
                int startPosition = GetExpressionStartPosition();
                if (expression.Value == 0)
                {
                    tw.Write("false");
                }
                else
                {
                    tw.Write("true");
                }
                SaveExpression(expression, startPosition);
            }
            else
            {
                PrintExpression(expression);
            }
        }

        static Encoding shiftJisWithExceptions = Encoding.GetEncoding("shift_jis", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        private void PrintChar(Expression charExpression)
        {
            int startPos = GetExpressionStartPosition();
            int value = charExpression.Value;
            if ((value >= 32 && value < 127) ||
                value == '\r' || value == '\t' || value == '\n' ||
                (value >= 0xA1 && value <= 0xDF))
            {
                tw.Write(EscapeAndQuoteMessage(((char)value).ToString()));
            }
            else
            {
                int byte1 = value & 0xFF;
                int byte2 = (value >> 8) & 0xFF;
                if (byte1 >= 0x81 && byte1 <= 0x9F ||
                    byte1 >= 0xE0 && byte1 <= 0xEF)
                {
                    var bytes = new byte[2];
                    bytes[0] = (byte)byte1;
                    bytes[1] = (byte)byte2;

                    try
                    {
                        string stringValue = shiftJisWithExceptions.GetString(bytes);
                        tw.Write(EscapeAndQuoteMessage(stringValue));
                    }
                    catch (DecoderFallbackException)
                    {
                        tw.Write(value.ToString());
                    }
                }
                else
                {
                    tw.Write(value.ToString());
                }
            }
            SaveExpression(charExpression, startPos);
        }

        private string GetMethodName(int functionNumber)
        {
            string functionName = GetFunctionName(functionNumber);
            var split = functionName.Split('@');
            var className = split[0];
            if (split.Length > 1)
            {
                functionName = split[1];
            }
            return functionName;
        }

        private string GetFunctionName(int functionNumber)
        {
            var function = GetFunction(functionNumber);
            string functionName;
            if (function != null)
            {
                //functionName = function.Name;
                functionName = function.GetActiveName();
            }
            else
            {
                functionName = "INVALID_FUNCTION[" + functionNumber.ToString() + "]";
            }
            return functionName;
        }

        private bool ShouldDisplayReturn(Expression expression)
        {
            bool displayReturn = true;
            //next expression is empty?
            var nextExpression = expression.GetParentNextExpression();
            var nextStatement = expression.GetNextStatement(false, false);
            while (nextStatement != null && nextStatement.ExpressionType == Instruction.CALLSYS && (nextStatement.Value == 3 || nextStatement.Value == 4))
            {
                nextStatement = nextStatement.GetNextStatement(false, false);
                nextExpression = nextStatement;
            }

            if (nextExpression == null || nextExpression.ExpressionType == Instruction.ENDFUNC)
            {
                var currentFunction = GetCurrentFunction();
                if (currentFunction != null)
                {
                    var parent = expression.Parent;

                    //is this void, and return has no arguments?
                    if (currentFunction.DataType == DataType.Void && expression.Arg1 == null)
                    {
                        //keep return if it's the only statement in the entire function
                        if (parent != null && parent.ExpressionType == Instruction.Statement)
                        {
                            displayReturn = false;
                        }
                    }

                    //is previous expression a return statement?
                    if (parent != null && parent.ExpressionType == Instruction.Statement)
                    {
                        var parentArg1 = parent.Arg1;
                        if (parentArg1 != null && parentArg1.ExpressionType == Instruction.RETURN)
                        {
                            var arg1 = expression.Arg1;
                            if ((currentFunction.DataType == DataType.Int || currentFunction.DataType == DataType.Bool || currentFunction.DataType == DataType.Lint) &&
                                arg1.ExpressionType == Instruction.PUSH &&
                                arg1.Value == 0)
                            {
                                displayReturn = false;
                            }
                            if (currentFunction.DataType == DataType.String &&
                                arg1.ExpressionType == Instruction.S_PUSH &&
                                this.GetString(arg1.Value) == "")
                            {
                                displayReturn = false;
                            }
                            if (currentFunction.DataType == DataType.RefString &&
                                arg1.ExpressionType == Instruction.PUSH && arg1.Value == -1)
                            {
                                displayReturn = false;
                            }
                            if (currentFunction.DataType == DataType.Struct &&
                                arg1.ExpressionType == Instruction.PUSH &&
                                arg1.Value == -1)
                            {
                                displayReturn = false;
                            }
                            if (currentFunction.DataType == DataType.RefStruct &&
                                arg1.ExpressionType == Instruction.PUSH &&
                                arg1.Value == -1)
                            {
                                displayReturn = false;
                            }
                            if (currentFunction.DataType == DataType.Float &&
                                arg1.ExpressionType == Instruction.F_PUSH &&
                                arg1.Value == 0)
                            {
                                displayReturn = false;
                            }
                            if (currentFunction.DataType.IsPrimitiveRefType() && arg1.ExpressionType == Instruction.AssignmentRef &&
                                arg1.Arg1.ExpressionType == Instruction.PUSH && arg1.Arg2.ExpressionType == Instruction.PUSH &&
                                arg1.Arg1.Value == -1 && arg1.Arg2.Value == 0)
                            {
                                displayReturn = false;
                            }
                            if (currentFunction.DataType == DataType.Functype && arg1.ExpressionType == Instruction.PUSH && arg1.Value == 0)
                            {
                                displayReturn = false;
                            }
                            if (currentFunction.DataType == DataType.Delegate && arg1.ExpressionType == Instruction.PUSH && arg1.Value == -1)
                            {
                                displayReturn = false;
                            }
                        }
                    }
                }

            }
            return displayReturn;
        }

        private void SaveExpression(Expression expression, int startPosition)
        {
            tw.Flush();
            int endPosition = (int)ms.Position;
            if (expressionMap != null)
            {
                int count = endPosition - startPosition;
                if (count < 0)
                {
                    count = 0;
                    startPosition = 0;
                }
                if (startPosition >= ms.Length)
                {
                    startPosition = 0;
                    count = 0;
                }
                //var text = UTF8Encoding.UTF8.GetString(ms.GetBuffer(), startPosition, count);
                expressionMap.Add(expression, startPosition, endPosition, tw.LineNumber, tw.Column);
            }
        }

        private int GetExpressionStartPosition()
        {
            tw.Flush();
            int startPosition = (int)ms.Position;
            if (tw.TabsPending)
            {
                startPosition += tw.TabString.Length * tw.Indent;
            }
            return startPosition;
        }

        private void PrintOperator(Expression expression)
        {
            Instruction expressionType = expression.ExpressionType;
            if ((expression.ExpressionType == Instruction.NOTE || expression.ExpressionType == Instruction.EQUALE))
            {
                var v1 = expression.Arg1.Variable;
                var v2 = expression.Arg2.Variable;
                bool v1IsRefType = v1 != null && (v1.DataType.IsRef() || v1.DataType.IsRefArray()) && !v1.DataType.IsPrimitiveRefType();
                bool v2IsRefType = v2 != null && (v2.DataType.IsRef() || v2.DataType.IsRefArray()) && !v2.DataType.IsPrimitiveRefType();
                if (v1IsRefType || v2IsRefType)
                {
                    if (expressionType == Instruction.NOTE) expressionType = Instruction.R_NOTE;
                    else if (expressionType == Instruction.EQUALE) expressionType = Instruction.R_EQUALE;
                }
            }
            var op = OperatorTable[expressionType];
            PrintOperator(expression, op);
        }

        private bool OperatorNeedsParenthesis(Expression arg, Operator op)
        {
            Operator argOp = GetOperator(arg);
            if (argOp != null)
            {
                if (argOp.precedence < op.precedence)
                {
                    return true;
                }
                if (argOp.precedence == op.precedence)
                {
                    if (argOp.instruction == Instruction.ASSIGN && op.symbol == "<-")
                    {
                        op = OperatorTable[Instruction.ASSIGN];
                    }
                    if (argOp.symbol == op.symbol)
                    {
                        var e = arg.Parent;
                        var arg1 = e.Arg1;
                        var arg2 = e.Arg2;
                        Operator op1 = GetOperator(arg1);
                        Operator op2 = GetOperator(arg2);
                        bool isRightAssociative = arg.ExpressionType.IsAssignment() || op.instruction == Instruction.Comma;
                        bool op1HasSameSymbol = (op1 != null && op1.symbol == op.symbol);
                        bool op2HasSameSymbol = (op2 != null && op2.symbol == op.symbol);

                        if (!isRightAssociative && op1HasSameSymbol && !op2HasSameSymbol)
                        {
                            return false;
                        }
                        if (isRightAssociative && !op1HasSameSymbol && op2HasSameSymbol)
                        {
                            return false;
                        }
                        return true;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void PrintOperator(Expression expression, Operator op)
        {
            bool arg1ContainsLowerPrecedence = false;
            bool arg2ContainsLowerPrecedence = false;

            Expression arg1 = expression.Arg1 ?? Expression.Empty;
            Expression arg2 = expression.Arg2 ?? Expression.Empty;

            //Operator arg1op = GetOperator(arg1), arg2op = GetOperator(arg2);

            arg1ContainsLowerPrecedence = OperatorNeedsParenthesis(arg1, op);
            arg2ContainsLowerPrecedence = OperatorNeedsParenthesis(arg2, op);

            //if (arg1op != null)
            //{
            //    if (arg1op.precedence == op.precedence && op.symbol == arg1op.symbol)
            //    {
            //        //is it left associative  (ie, not an assignment operator)
            //    }

            //    && (arg1op.precedence < op.precedence || (arg1op.precedence == op.precedence && op.symbol != arg1op.symbol)))
            //{
            //    arg1ContainsLowerPrecedence = true;
            //}

            //if (arg2op != null && (arg2op.precedence < op.precedence || (arg2op.precedence == op.precedence && op.symbol != arg2op.symbol)))
            //{
            //    arg2ContainsLowerPrecedence = true;
            //}

            if (op.position == OperatorPosition.Infix)
            {
                bool argumentsAreBool = false;
                if (expression.ExpressionType == Instruction.LogicalAnd || expression.ExpressionType == Instruction.LogicalOr)
                {
                    argumentsAreBool = true;
                }

                //check for dummy assignments
                if (expression.ExpressionType == Instruction.ASSIGN)
                {
                    var variable = arg1.Variable;
                    if (variable != null && variable.Name.StartsWith("<dummy :"))
                    {
                        //bypass the assignment to the dummy variable
                        PrintExpression(arg2);
                        return;
                    }
                }
                //check for new variables being declared
                if (expression.ExpressionType == Instruction.RefAssignment)
                {
                    if (CheckForVariableDeclarations(expression.Arg1, false))
                    {
                        //change symbol to = instead of <-
                        op = OperatorTable.GetOrNull(Instruction.ASSIGN);
                    }
                }

                if (arg1ContainsLowerPrecedence)
                {
                    tw.Write("(");
                }

                //print the expression
                if (argumentsAreBool && arg1.ExpressionType == Instruction.PUSH)
                {
                    PrintBoolExpression(arg1);
                }
                else
                {
                    PrintExpression(arg1);
                }

                if (arg1ContainsLowerPrecedence)
                {
                    tw.Write(")");
                }

                WriteOperatorSymbol(op);

                if (arg2ContainsLowerPrecedence)
                {
                    tw.Write("(");
                }

                //print the expression
                if (argumentsAreBool && arg2.ExpressionType == Instruction.PUSH)
                {
                    PrintBoolExpression(arg2);
                }
                else
                {
                    PrintExpression(arg2);
                }

                if (arg2ContainsLowerPrecedence)
                {
                    tw.Write(")");
                }
            }
            else if (op.position == OperatorPosition.UnaryPrefix)
            {
                bool argumentsAreBool = false;
                if (expression.ExpressionType == Instruction.NOT)
                {
                    argumentsAreBool = true;
                }
                var returnType = expression.GetDataType();

                WriteOperatorSymbol(op);

                if (arg1ContainsLowerPrecedence)
                {
                    tw.Write("(");
                }

                if (argumentsAreBool && arg1.ExpressionType == Instruction.PUSH)  //if ! operator and an int, make the int into a bool
                {
                    PrintBoolExpression(arg1);
                }
                else
                {
                    PrintExpression(arg1);
                }

                if (arg1ContainsLowerPrecedence)
                {
                    tw.Write(")");
                }
            }
            else if (op.position == OperatorPosition.UnaryPostfix)
            {
                if (arg1ContainsLowerPrecedence)
                {
                    tw.Write("(");
                }

                PrintExpression(arg1);

                if (arg1ContainsLowerPrecedence)
                {
                    tw.Write(")");
                }
                WriteOperatorSymbol(op);
            }
        }

        private void PrintSpecialInt(Expression me, Expression other)
        {
            var otherInstruction = other.ExpressionType;

            if (otherInstruction == Instruction.C_REF)
            {
                PrintChar(me);
                return;
            }

            IVariable otherType = other.Variable;
            var otherCanon = otherType.Canonicalize();
            if (otherCanon == null)
            {
                if (other.ExpressionType == Instruction.REF || other.ExpressionType == Instruction.ArrayIndex)
                {
                    if (otherType != null && otherType.DataType.GetTypeOfArrayElement().IsPrimitiveType())
                    {

                    }
                    else
                    {
                        otherType = other.Arg1.Variable;
                    }
                }
            }

            //special check - do me and other have same parent, and incorrectly selected bool?
            if (me.Parent == other.Parent)
            {
                var parentExpressionType = me.Parent.ExpressionType;
                if (otherType != null && otherType.DataType == DataType.Bool)
                {
                    if (parentExpressionType == Instruction.LogicalAnd || parentExpressionType == Instruction.LogicalOr || me.Value == 0 || me.Value == 1)
                    {

                    }
                    else
                    {
                        otherType = null;
                    }
                }
            }

            PrintSpecialInt(me, otherType);
        }

        private void PrintSpecialInt(Expression me, IVariable otherType)
        {
            int value = me.Value;

            if (otherType != null && otherType.DataType == DataType.Bool)
            {
                PrintBoolExpression(me);
            }
            else if (otherType != null && (otherType.DataType == DataType.Functype || otherType.DataType == DataType.Delegate))
            {
                var function = GetFunction(value);
                if (function != null)
                {
                    int startPosition = GetExpressionStartPosition();
                    if (function.Index != 0)
                    {
                        tw.Write('&');
                    }
                    //tw.Write(function.Name);
                    tw.Write(function.GetActiveName());
                    SaveExpression(me, startPosition);
                }
                else
                {
                    PrintIntExpression(me);
                }
            }
            else if (otherType != null && (otherType.DataType == DataType.RefStruct || otherType.DataType.IsRefArray() || otherType.DataType == DataType.RefString))
            {
                if (value == -1)
                {
                    if (otherType.DataType == DataType.RefString)
                    {
                        var parentInstruction = (me.Parent ?? Expression.Empty).ExpressionType;
                        if (parentInstruction == Instruction.EQUALE || parentInstruction == Instruction.NOTE || parentInstruction == Instruction.R_EQUALE || parentInstruction == Instruction.R_NOTE)
                        {
                            //proceed to display NULL
                        }
                        else
                        {
                            //do not display NULL for instructions other than EQUALE or NOTE, it might be a % operator or something
                            goto defaultCase;
                        }
                    }

                    int startPosition = GetExpressionStartPosition();
                    tw.Write("NULL");
                    SaveExpression(me, startPosition);
                }
                else
                {
                    PrintIntExpression(me);
                }
            }
            else if (otherType != null && otherType.DataType == DataType.IMainSystem)
            {
                if (value == 0)
                {
                    tw.Write("NULL");
                }
                else
                {
                    PrintIntExpression(me);
                }
            }
            //else if (otherInstruction == Instruction.DG_NUMOF)
            //{
            //    var function = GetFunction(value);
            //    if (function == null)
            //    {
            //        PrintExpression(me);
            //    }
            //    else
            //    {
            //        int startPosition = GetExpressionStartPosition();
            //        var expression = new Expression(Instruction.CALLFUNC, me.address, value);
            //        tw.Write(function.Name);
            //        SaveExpression(expression, startPosition);
            //    }
            //}
            else
            {
                goto defaultCase;
            }
            return;
        defaultCase:
            bool handled = false;
            if (otherType != null)
            {
                var metaData = ainFile.MetadataFile.Metadata.GetOrNull(otherType);
                if (metaData != null)
                {
                    var enumerationType = metaData.EnumerationType;
                    PrintEnumeration(enumerationType, me);
                    handled = true;
                }
            }

            if (!handled)
            {
                PrintIntExpression(me);
            }
        }

        private void PrintIntExpression(Expression expression)
        {
            int startPosition = GetExpressionStartPosition();
            tw.Write(expression.Value);
            SaveExpression(expression, startPosition);
        }

        private void PrintEnumeration(EnumerationType enumerationType, Expression pushExpression)
        {
            int startPosition = GetExpressionStartPosition();
            int value = pushExpression.Value;
            if (enumerationType != null && enumerationType.ContainsKey(value))
            {
                if (this.Options.UseEnumerations)
                {
                    tw.Write(enumerationType[value]);
                }
                else
                {
                    tw.Write(value.ToString());
                }
            }
            else
            {
                tw.Write(value.ToString());
            }
            SaveExpression(pushExpression, startPosition);
        }

        public static HashSet<Instruction> castOperations = new HashSet<Instruction>(new Instruction[]{
            Instruction.FTOI, Instruction.FTOS, Instruction.ITOB, Instruction.ITOF, Instruction.ITOLI, Instruction.DG_COPY,
        });

        private Operator GetOperator(Expression argument)
        {
            if (argument == null) return null;
            Operator op = null;
            if (!this.ShowCasting)
            {
                argument = argument.SkipChildCastOperations();
            }

            if (OperatorTable.ContainsKey(argument.ExpressionType))
            {
                op = OperatorTable[argument.ExpressionType];
            }
            return op;
        }

        //private Operator GetOperatorSkipCasting(Expression argument)
        //{
        //    while (!this.ShowCasting && argument != null && castOperations.Contains(argument.ExpressionType))
        //    {
        //        argument = argument.Arg1;
        //    }
        //    return GetOperator(argument);
        //}

        private void WriteOperatorSymbol(Operator op)
        {
            if (op.spaceBefore)
            {
                tw.Write(" ");
            }
            tw.Write(op.symbol);
            if (op.spaceAfter)
            {
                tw.Write(" ");
            }
        }

        private void PrintStatement(Expression expression)
        {
            int twColumn = tw.Column;
            int twRow = tw.LineNumber;
            if (this.BanStatements && expression.ExpressionType == Instruction.Statement)
            {
                if (expression.Arg1 != null && expression.Arg2 != null)
                {
                    PrintExpression(expression.Arg1);
                    tw.Write(", ");
                    PrintExpression(expression.Arg2);
                    return;
                }
            }
        Restart:
            var arg1 = expression.Arg1 ?? Expression.Empty;
            var arg2 = expression.Arg2 ?? Expression.Empty;
            isTopExpression = 2;

            //if ((arg1.ExpressionType == Instruction.CALLSYS && arg1.Value == 3) || arg1.ExpressionType == Instruction.DELETE) //lockpeek or delete
            //{
            //    Expression assignment = null;
            //    Expression nextExpression = null;
            //    if (IsRefAssignment(expression, out assignment, out nextExpression))
            //    {
            //        PrintExpression(assignment);
            //        if (tw.Column != twColumn || tw.LineNumber != twRow)
            //        {
            //            tw.WriteLine(";");
            //        }
            //        expression = nextExpression;
            //        PrintExpression(expression);
            //        return;
            //    }
            //}
            PrintExpression(expression.Arg1);

            if (arg1.ExpressionType == Instruction.MSG)
            {
                Expression nextStatement = null;
                if (arg2.ExpressionType == Instruction.Statement)
                {
                    nextStatement = arg2.Arg1;
                }
                else
                {
                    nextStatement = arg2;
                }
                if (nextStatement != null && (nextStatement.ExpressionType == Instruction.CALLFUNC || nextStatement.ExpressionType == Instruction.CALLSYS))
                {
                    if (arg2.Arg2 != null && arg2.ExpressionType == Instruction.Statement)
                    {
                        arg2 = arg2.Arg2;
                    }
                    else
                    {
                        arg2 = null;
                    }
                    tw.Write(" ");
                    if (nextStatement.Arg1 == null)
                    {
                        int startPosition = GetExpressionStartPosition();
                        string functionName = GetFunctionName(nextStatement.Value);
                        tw.Write(functionName);
                        SaveExpression(nextStatement, startPosition);
                    }
                    else
                    {
                        PrintExpression(nextStatement);
                    }
                }
            }

            if (!arg1.IsBlock() && !tw.TabsPending)
            {
                if (tw.Column != twColumn || tw.LineNumber != twRow)
                {
                    if (!arg1.ExpressionType.IsLabel())
                    {
                        tw.Write(";");
                    }
                    tw.WriteLine();
                }
            }
            isTopExpression = 2;

            if (arg2 != null && arg2.ExpressionType == Instruction.Statement)
            {
                expression = arg2;
                goto Restart;
            }

            PrintExpression(arg2);

            if (arg2 != null && arg2.ExpressionType != Instruction.Statement)
            {
                if (!expression.Arg2.IsBlock() && !tw.TabsPending && !expression.Arg2.ExpressionType.IsLabel())
                {
                    if (tw.Column != twColumn || tw.LineNumber != twRow)
                    {
                        tw.Write(";");
                    }
                }
            }
        }

        private bool IsRefAssignment(Expression expression, out Expression assignment, out Expression nextExpression)
        {
            assignment = null;
            nextExpression = null;

            //callsys 3
            var statement1 = expression;
            if (statement1 == null || statement1.ExpressionType != Instruction.Statement) { return false; }
            //delete (ref (pushlocalpage push))
            var statement2 = statement1.Arg2;
            if (statement2 == null || statement2.ExpressionType != Instruction.Statement) { return false; }
            //assign (assignmentref(pushlocalpage push) ref(...))
            var statement3 = statement2.Arg2;
            if (statement3 == null || statement3.ExpressionType != Instruction.Statement) { return false; }
            //callsys 4
            var statement4 = statement3.Arg2;
            if (statement4 == null || statement4.ExpressionType != Instruction.Statement) { return false; }

            var expression1 = statement1.Arg1;
            var expression2 = statement2.Arg1;
            var expression3 = statement3.Arg1;
            var expression4 = statement4.Arg1;

            if (expression1 != null && expression1.ExpressionType == Instruction.CALLSYS && expression1.Value == 3 &&
                expression2 != null && expression2.ExpressionType == Instruction.DELETE &&
                expression3 != null && expression3.ExpressionType == Instruction.ASSIGN &&
                expression4 != null && expression4.ExpressionType == Instruction.CALLSYS && expression4.Value == 4)
            {
                if (expression2.Arg1 != null && expression2.Arg1.ExpressionType == Instruction.REF &&
                    expression2.Arg1.Variable.DataType == DataType.RefStruct &&
                    expression3.Arg1 != null && expression3.Arg1.ExpressionType == Instruction.AssignmentRef &&
                    expression3.Arg1.Variable == expression2.Arg1.Variable &&
                    expression3.Arg2 != null)
                {
                    assignment = expression3;
                    nextExpression = statement4.Arg2;
                    return true;
                }
            }

            if (expression1 != null && expression1.ExpressionType == Instruction.DELETE &&
                expression2 != null && expression2.ExpressionType == Instruction.R_ASSIGN &&
                expression3 != null && expression3.ExpressionType == Instruction.CALLSYS && expression3.Value == 3 &&
                expression4 != null && expression4.ExpressionType == Instruction.CALLSYS && expression4.Value == 4)
            {
                if (expression1.Arg1 != null && expression2.Arg1 != null &&
                    expression1.Arg1.Variable == expression2.Arg1.Variable)
                {
                    assignment = expression2;
                    nextExpression = statement4.Arg2;
                    return true;
                }
            }
            return false;

        }

        public string GetClassDeclaration(string className)
        {
            var structInfo = ainFile.GetStruct(className);
            if (structInfo != null)
            {
                return GetClassDeclaration(structInfo);
            }
            return "";
        }

        public string GetClassDeclaration(Struct structInfo)
        {
            var utf8 = new UTF8Encoding(false);
            ms = new MemoryStream();

            var streamWriter = new StreamWriter(ms, utf8);
            tw = new MyIndentedTextWriter(streamWriter);
            PrintClassDeclaration(structInfo);
            tw.Flush();

            return utf8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        public static string GetClassDeclaration2(Struct structInfo)
        {
            var displayer = new ExpressionDisplayer(structInfo.Root);
            return displayer.GetClassDeclaration(structInfo);
        }

        private void PrintClassDeclaration(Struct structInfo)
        {
            int startPosition;
            startPosition = GetExpressionStartPosition();
            //tw.Write("class " + structInfo.Name);
            tw.Write("class " + structInfo.GetActiveName());

            SaveExpression(new Expression(Instruction.PrintStructType, -1, structInfo.Index, -1), startPosition);
            OpenBlock();
            tw.Indent--;
            tw.WriteLine("public:");
            tw.Indent++;

            //find methods
            List<Function> methods = new List<Function>();
            HashSet<string> methodsSeen = new HashSet<string>();

            string prefix = structInfo.Name + "@";
            string prefix2 = structInfo.GetActiveName() + "::";
            foreach (var function in ainFile.Functions)
            {
                if (function.Name.StartsWith(prefix) && !methodsSeen.Contains(function.Name))
                {
                    methodsSeen.Add(function.Name);
                    methods.Add(function);
                }
            }

            var arrayInitializerFunction = ainFile.GetFunction(prefix + "2");
            Dictionary<Variable, string> ArraySizes = new Dictionary<Variable, string>();

            if (arrayInitializerFunction != null)
            {
                var code = ainFile.DecompiledCodeCache.GetDecompiledCode(arrayInitializerFunction);
                if (code.Arg1.ExpressionType != Instruction.RETURN)
                {
                    foreach (var expression in code.GetChildExpressions())
                    {
                        if (expression.ExpressionType == Instruction.A_ALLOC)
                        {
                            var variable = expression.Arg1.Variable.Canonicalize() as Variable;
                            if (variable.Parent == structInfo && expression.Arg2.ExpressionType == Instruction.PUSH)
                            {
                                if (variable.ArrayDimensions == 1)
                                {
                                    int arraySize = expression.Arg2.Value;
                                    ArraySizes[variable] = arraySize.ToString();
                                }
                                else
                                {
                                    var sb = new StringBuilder();
                                    for (int i = 1; i < expression.Args.Count; i++)
                                    {
                                        if (i > 1) sb.Append("][");
                                        var eArg = expression.Args[i];
                                        if (eArg != null && eArg.ExpressionType == Instruction.PUSH)
                                        {
                                            sb.Append(eArg.Value.ToString());
                                        }
                                        else
                                        {
                                            //error - make something up instead
                                            sb.Append("0");
                                        }
                                    }
                                    ArraySizes[variable] = sb.ToString();
                                }
                            }
                        }
                    }
                }
            }

            //output each field
            if (structInfo.Members.Count > 0)
            {
                foreach (var member in structInfo.Members)
                {
                    if (member.DataType != DataType.Void)
                    {
                        startPosition = GetExpressionStartPosition();
                        var expression = new Expression(Instruction.PrintStructType, -1, structInfo.Index, member.Index);
                        tw.Write(member.ToString());
                        if (member.ArrayDimensions > 0 && ArraySizes.Count > 0 && ArraySizes.ContainsKey(member))
                        {
                            tw.Write("[" + ArraySizes[member] + "]");
                        }
                        SaveExpression(expression, startPosition);
                        tw.WriteLine(";");
                    }
                }

            }
            if (structInfo.Members.Count > 0 && methods.Count > 0)
            {
                tw.WriteLine();
            }
            if (methods.Count > 0)
            {
                foreach (var method in methods)
                {
                    string functionName = method.Name.Substring(prefix.Length);
                    if (functionName != "2")
                    {
                        string methodText = GetFunctionDeclaration(method, false);
                        methodText = methodText.Replace(prefix2, "");
                        startPosition = GetExpressionStartPosition();
                        var expression = new Expression(Instruction.CALLMETHOD, -1, method.Index);
                        tw.Write(methodText);
                        SaveExpression(expression, startPosition);
                        tw.WriteLine(";");
                    }
                }
            }
            CloseBlock(";");
        }

        public string GetFunctypeDeclaration(FunctionType funcType)
        {
            if (funcType == null) return "";
            StringBuilder sb = new StringBuilder();
            sb.Append("functype ");
            sb.Append(GetDataTypeNameReal(funcType));
            sb.Append(" ");
            sb.Append(funcType.Name);
            sb.Append(" (");
            if (funcType.ParameterCount == 0)
            {
                sb.Append("void");
            }
            else
            {
                for (int i = 0; i < funcType.Parameters.Count; i++)
                {
                    var parameter = funcType.Parameters[i];

                    if (parameter.DataType != DataType.Void)
                    {
                        if (i > 0)
                        {
                            sb.Append(", ");
                        }
                        sb.Append(GetDataTypeNameReal(parameter));
                    }
                }
            }
            sb.Append(");");
            return sb.ToString();
        }

        public string GetDelegateDeclaration(FunctionType funcType)
        {
            if (funcType == null) return "";
            StringBuilder sb = new StringBuilder();
            sb.Append("delegate ");
            sb.Append(GetDataTypeNameReal(funcType));
            sb.Append(" ");
            sb.Append(funcType.Name);
            sb.Append(" (");
            if (funcType.ParameterCount == 0)
            {
                sb.Append("void");
            }
            else
            {
                for (int i = 0; i < funcType.Parameters.Count; i++)
                {
                    var parameter = funcType.Parameters[i];

                    if (parameter.DataType != DataType.Void)
                    {
                        if (i > 0)
                        {
                            sb.Append(", ");
                        }
                        sb.Append(GetDataTypeNameReal(parameter));
                    }
                }
            }
            sb.Append(");");
            return sb.ToString();
        }
    }
}
