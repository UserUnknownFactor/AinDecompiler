using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler.Compiler
{
    public static class CompilerExtensions
    {
        public static IEnumerable<TokenExpression> TokensWhere(this IEnumerable<TokenExpression> tokens, TokenType tokenType)
        {
            return tokens.Where(t => t.TokenType == tokenType);
        }

        public static IEnumerable<TokenExpression> TokensWhereRecursiveFirst(this IEnumerable<TokenExpression> tokens, TokenType tokenType)
        {
            return tokens.SelectMany(t => TokensWhereRecursiveFirst(t, tokenType));
        }

        public static IEnumerable<TokenExpression> TokensWhereRecursiveFirst(this TokenExpression token, TokenType tokenType)
        {
            if (token.TokenType == tokenType)
            {
                yield return token;
                yield break;
            }
            foreach (var subexpression in token.Subexpressions)
            {
                var whereRecursive = TokensWhereRecursiveFirst(subexpression, tokenType);
                foreach (var w in whereRecursive)
                {
                    yield return w;
                }
            }
        }

        public static IEnumerable<TokenExpression> TokensWhereRecursive(this TokenExpression token, TokenType tokenType)
        {
            if (token.TokenType == tokenType)
            {
                yield return token;
            }
            foreach (var subexpression in token.Subexpressions)
            {
                var whereRecursive = TokensWhereRecursive(subexpression, tokenType);
                foreach (var w in whereRecursive)
                {
                    yield return w;
                }
            }
        }

    }

    public partial class Compiler
    {
        string GetFunctionName(TokenExpression functionNameToken)
        {
            return GetFunctionName(functionNameToken, null);
        }

        string GetFunctionName(TokenExpression functionNameToken, string className)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var token in functionNameToken.Subexpressions)
            {
                if (token.TokenType != TokenType.OpenParenthesis)
                {
                    if (token.TokenType == TokenType.ScopeResolution)
                    {
                        if (!string.IsNullOrEmpty(className))
                        {
                            sb.Length = 0;
                        }
                        else
                        {
                            sb.Append("@");
                        }
                    }
                    else
                    {
                        sb.Append(token.Token.Value);
                    }
                }
            }
            string functionName = sb.ToString();
            int indexOfAt = functionName.IndexOf('@');
            if (indexOfAt >= 0)
            {
                className = functionName.Substring(0, indexOfAt);
                functionName = functionName.Substring(indexOfAt + 1);
            }

            if (string.IsNullOrEmpty(className))
            {
                return functionName;
            }
            else
            {
                if (functionName == className)
                {
                    return className + "@0";
                }
                if (functionName == "~" + className)
                {
                    return className + "@1";
                }
                return className + "@" + functionName;
            }
        }

        internal Variable GetDataType(TokenExpression dataTypeToken)
        {
            bool isRef = false;
            bool isArray = false;
            int structType = -1;
            int arrayDimensions = 0;
            DataType dataType = DataType.Void;

            for (int i = 0; i < dataTypeToken.Subexpressions.Count; i++)
            {
                var token = dataTypeToken.Subexpressions[i];

                switch (token.TokenType)
                {
                    case TokenType.ArrayType:
                        dataTypeToken = token;
                        i = -1;
                        continue;
                    case TokenType.Ref:
                        isRef = true;
                        break;
                    case TokenType.Array:
                        isArray = true;
                        //i++;
                        arrayDimensions = 1;
                        break;
                    case TokenType.Number:
                        arrayDimensions = token.Token.ToInt();
                        break;
                    case TokenType.Void:
                        dataType = DataType.Void;
                        break;
                    case TokenType.Int:
                        dataType = DataType.Int;
                        if (isArray) dataType += 4;
                        if (isRef) dataType += 8;
                        break;
                    case TokenType.Struct:
                        dataType = DataType.Struct;
                        structType = -1;
                        if (isArray) dataType += 4;
                        if (isRef) dataType += 8;
                        break;
                    case TokenType.String:
                        dataType = DataType.String;
                        if (isArray) dataType += 4;
                        if (isRef) dataType += 8;
                        break;
                    case TokenType.Float:
                        dataType = DataType.Float;
                        if (isArray) dataType += 4;
                        if (isRef) dataType += 8;
                        break;
                    case TokenType.Bool:
                        dataType = DataType.Bool;
                        if (isArray && !isRef) dataType += 3;
                        else if (!isArray && isRef) dataType += 4;
                        else if (isArray && isRef) dataType += 5;
                        break;
                    case TokenType.Lint:
                        dataType = DataType.Lint;
                        if (isArray && !isRef) dataType += 3;
                        else if (!isArray && isRef) dataType += 4;
                        else if (isArray && isRef) dataType += 5;
                        break;
                    case TokenType.IMainSystem:
                        dataType = DataType.IMainSystem;
                        break;
                    case TokenType.Identifier:
                        string name = token.Token.Value;
                        if (Structs.Contains(name))
                        {
                            structType = Structs.NameToIndex[name];
                            dataType = DataType.Struct;
                            if (isArray) dataType += 4;
                            if (isRef) dataType += 8;
                        }
                        else if (Functypes.Contains(name))
                        {
                            structType = Functypes.NameToIndex[name];
                            dataType = DataType.Functype;
                            if (isArray && !isRef) dataType += 3;
                            else if (!isArray && isRef) dataType += 4;
                            else if (isArray && isRef) dataType += 5;
                        }
                        else if (Delegates.Contains(name))
                        {
                            structType = Delegates.NameToIndex[name];
                            dataType = DataType.Delegate;
                            if (isArray && !isRef) dataType += 3;
                            else if (!isArray && isRef) dataType += 4;
                            else if (isArray && isRef) dataType += 5;
                        }
                        else
                        {
                            //HLL data types
                            if (name == "intp")
                            {
                                dataType = DataType.RefInt;
                            }
                            else if (name == "stringp")
                            {
                                dataType = DataType.RefString;
                            }
                            else if (name == "floatp")
                            {
                                dataType = DataType.RefFloat;
                            }
                            else if (name == "boolp")
                            {
                                dataType = DataType.RefBool;
                            }
                            else
                            {
                                //unknown data type
                                dataType = DataType.Struct;
                                if (isRef)
                                {
                                    dataType = DataType.RefStruct;
                                }
                                structType = -1;
                            }
                        }
                        break;
                }
            }
            return new Variable("", dataType, structType, arrayDimensions);
        }

        void CreateHllSymbols(IList<TokenExpression> tokens, string libraryName, string libraryAlias)
        {
            var functionDeclarations = tokens.TokensWhere(TokenType.FunctionDeclaration);
            foreach (var token in functionDeclarations)
            {
                var function = CreateSymbolForLibraryFunction(token, libraryName, libraryAlias);
            }
        }

        List<object> CreateSymbols(List<TokenExpression> tokens)
        {
            return CreateSymbols(tokens, true);
        }

        List<object> CreateSymbols(List<TokenExpression> tokens, bool GenerateAdditionalFunctions)
        {
            List<object> declarationOrder = new List<object>();

            var classDefinitions = tokens.TokensWhere(TokenType.ClassDefinition).ToArray();
            var functypeDeclarations = tokens.TokensWhere(TokenType.FunctypeDeclaration).ToArray();

            //var globalDefinitions = tokens.TokensWhere(TokenType.VariableDeclaration);
            //var constDefinitions = tokens.TokensWhere(TokenType.ConstDeclaration);
            //var functionDeclarations = tokens.TokensWhere(TokenType.FunctionDeclaration);

            //create structs
            foreach (var token in classDefinitions)
            {
                string className = token.Subexpressions[1].Token.Value;
                var structInfo = Structs.Get(className);
                declarationOrder.Add(structInfo);
            }

            //create function/delegate
            foreach (var token in functypeDeclarations)
            {
                var functionNameToken = token.Subexpressions[2];
                string funcTypeName = GetFunctionName(functionNameToken, null);
                if (token.Subexpressions[0].TokenType == TokenType.Delegate)
                {
                    var delegateInfo = Delegates.Get(funcTypeName);
                    declarationOrder.Add(delegateInfo);
                }
                else
                {
                    var functype = Functypes.Get(funcTypeName);
                    declarationOrder.Add(functype);
                }
            }

            //data types exist now - now create structs, functions, functypes, globals, constants

            foreach (var token in tokens)
            {
                switch (token.TokenType)
                {
                    case TokenType.ClassDefinition:
                        {
                            string className = token.Subexpressions[1].Token.Value;
                            var structInfo = this.Structs.Get(className);

                            HashSet<string> memberNames = new HashSet<string>();
                            memberNames.AddRange(structInfo.Members.Names());

                            var entries = token.Subexpressions[2].Subexpressions;
                            foreach (var entry0 in entries)
                            {
                                var entry = entry0.Subexpressions[0];
                                switch (entry.TokenType)
                                {
                                    case TokenType.FunctionDeclaration:
                                        {
                                            Function function;
                                            var blockToken = entry0.GetFirstSubexpression(TokenType.Block);
                                            if (blockToken != null)
                                            {
                                                //create function, and add it to the list (gives it an index)
                                                function = CreateSymbolForFunction(entry, className, true);
                                                this.FunctionDefinitionTokens[function] = blockToken;
                                                declarationOrder.Add(function);
                                            }
                                            else
                                            {
                                                //declaration without a definition - don't add it to the list until it's actually defined
                                                function = CreateSymbolForFunction(entry, className, false);
                                            }
                                            var functionParameterList = entry.GetFirstSubexpression(TokenType.FunctionParameterList);
                                            bool hasAssignment = (functionParameterList.GetSubexpressionsRecursive(TokenType.Assign).FirstOrDefault() != null);
                                            if (hasAssignment)
                                            {
                                                this.FunctionDeclarationTokens[function] = entry;
                                            }
                                        }
                                        break;
                                    case TokenType.ConstDeclaration:
                                        {
                                            CreateSymbolForConst(entry, className);
                                        }
                                        break;
                                    case TokenType.VariableDeclaration:
                                        {
                                            var variables = GetVariablesInVariableDeclaration(entry);
                                            foreach (var variable in variables)
                                            {
                                                //does symbol already exist?
                                                if (variable.DataType != DataType.Void && memberNames.Contains(variable.Name))
                                                {
                                                    //do nothing
                                                }
                                                else
                                                {
                                                    int index = structInfo.Members.Count;
                                                    structInfo.Members.Add(variable);
                                                    variable.Index = index;
                                                    variable.Parent = structInfo;
                                                    variable.Root = ainFile;

                                                    memberNames.Add(variable.Name);
                                                }
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                        break;
                    case TokenType.FunctypeDeclaration:
                        {
                            var declarationToken = token.Subexpressions[0];
                            var dataTypeToken = token.Subexpressions[1];
                            var functionNameToken = token.Subexpressions[2];
                            var parameterListToken = token.Subexpressions[3];

                            var dataType = GetDataType(dataTypeToken);
                            string functypeName = GetFunctionName(functionNameToken);

                            FunctionType funcType;
                            if (declarationToken.TokenType == TokenType.Delegate)
                            {
                                funcType = Delegates.Get(functypeName);
                            }
                            else
                            {
                                funcType = Functypes.Get(functypeName);
                            }
                            funcType.SetVariableType(dataType);

                            int parameterNumber = 0;
                            foreach (var parameterDataTypeToken in parameterListToken.Subexpressions)
                            {
                                int index = funcType.Parameters.Count;
                                var parameter = GetDataType(parameterDataTypeToken);
                                if (parameter.DataType != DataType.Void)
                                {
                                    parameter.Name = parameterNumber.ToString();
                                    parameter.Index = index;
                                    parameter.Root = ainFile;
                                    funcType.Parameters.Add(parameter);
                                    if (parameter.DataType.IsPrimitiveRefType())
                                    {
                                        var voidParameter = new Variable("<void>", DataType.Void, 0, 0);
                                        voidParameter.Index = index + 1;
                                        voidParameter.Root = ainFile;
                                        funcType.Parameters.Add(voidParameter);
                                    }
                                }
                                parameterNumber++;
                            }
                            funcType.ParameterCount = funcType.Parameters.Count;
                        }
                        break;
                    case TokenType.FunctionDeclaration:
                        {
                            var function = CreateSymbolForFunction(token);
                            declarationOrder.Add(function);
                            var blockToken = token.GetFirstSubexpression(TokenType.Block);
                            if (blockToken != null)
                            {
                                this.FunctionDefinitionTokens[function] = blockToken;
                            }
                            var functionParameterList = token.GetFirstSubexpression(TokenType.FunctionParameterList);
                            bool hasAssignment = (functionParameterList.GetSubexpressionsRecursive(TokenType.Assign).FirstOrDefault() != null);
                            if (hasAssignment)
                            {
                                this.FunctionDeclarationTokens[function] = token;
                            }
                        }
                        break;
                    case TokenType.GlobalGroupDeclaration:
                        {
                            var groupNameToken = token.Subexpressions.FirstOrDefault();
                            string groupName = groupNameToken.Token.Value;
                            int groupIndex = this.GlobalGroupNames.Add(groupName);

                            foreach (var variableDeclarationToken in token.Subexpressions.TokensWhere(TokenType.VariableDeclaration))
                            {
                                var variables = GetVariablesInVariableDeclaration(variableDeclarationToken);
                                foreach (var variable in variables)
                                {
                                    if (variable.Name == "<void>")
                                    {
                                        var global = new Global();
                                        global.GroupIndex = groupIndex;
                                        global.Name = variable.Name;
                                        global.SetVariableType(variable);
                                        global.Index = Globals.List.Count;
                                        global.Root = ainFile;
                                        Globals.List.Add(global);

                                    }
                                    else
                                    {
                                        var global = Globals.Get(variable.Name);
                                        if (this.ArraySizeExpressions.ContainsKey(variable))
                                        {
                                            this.ArraySizeExpressions[global] = this.ArraySizeExpressions[variable];
                                        }
                                        global.SetVariableType(variable);
                                        global.GroupIndex = groupIndex;
                                        declarationOrder.Add(global);
                                    }
                                }
                            }
                        }
                        break;
                    case TokenType.VariableDeclaration:
                        {
                            var variables = GetVariablesInVariableDeclaration(token);
                            foreach (var variable in variables)
                            {
                                if (variable.Name == "<void>")
                                {
                                    var global = new Global();
                                    global.Name = variable.Name;
                                    global.SetVariableType(variable);
                                    global.Index = Globals.List.Count;
                                    global.Root = ainFile;
                                    Globals.List.Add(global);

                                }
                                else
                                {
                                    var global = Globals.Get(variable.Name);
                                    declarationOrder.Add(global);
                                    if (this.ArraySizeExpressions.ContainsKey(variable))
                                    {
                                        this.ArraySizeExpressions[global] = this.ArraySizeExpressions[variable];
                                    }
                                    global.SetVariableType(variable);
                                }
                            }
                        }
                        break;
                    case TokenType.ConstDeclaration:
                        {
                            CreateSymbolForConst(token);
                        }
                        break;
                    case TokenType.Eof:
                        {
                            string fileName = token.Subexpressions[0].Token.Value;
                            declarationOrder.Add(fileName);
                        }
                        break;
                    case TokenType.HllDeclaration:
                        {
                            string libraryName = token.Subexpressions[0].Token.Value;
                            CreateHllSymbols(token.Subexpressions, libraryName, libraryName);
                            declarationOrder.Add(Libraries.Get(libraryName));
                        }
                        break;
                }
            }
            Function zeroFunction = null;
            bool createZeroFunction = false;
            if (GenerateAdditionalFunctions)
            {
                createZeroFunction = !Functions.Contains("0");
                zeroFunction = null;

                if (createZeroFunction)
                {
                    zeroFunction = Functions.Get("0", false);
                    declarationOrder.Add(zeroFunction);
                }
            }

            var declaraedStructs = declarationOrder.OfType<Struct>().OrderByIndex().ToArray();
            foreach (var sturctInfo in declaraedStructs)
            {
                GenerateArrayInitializer(declarationOrder, sturctInfo, true, false);
            }

            if (GenerateAdditionalFunctions)
            {
                //create @2 functions for each constructor
                foreach (var structInfo in Structs.List)
                {
                    GenerateArrayInitializer(declarationOrder, structInfo, false, false);
                }

                if (createZeroFunction)
                {
                    //create 0 function
                    zeroFunction = Functions.Get("0");
                    
                    //wait until later to define it
                }

                this.FunctionDefinitionTokens[Functions.Get("NULL")] = new TokenExpression(TokenType.Block);
                declarationOrder.Add(ainFile.GetFunction("NULL"));
            }

            return declarationOrder;
        }

        private void DefineZeroFunction()
        {
            Function zeroFunction = Functions.Get("0");
            TokenExpression zeroFunctionBlock = new TokenExpression(TokenType.Block);
            foreach (var pair in this.ArraySizeExpressions)
            {
                var global = pair.Key as Global;
                if (global != null)
                {
                    AddArrayDeclarationToBlock(zeroFunctionBlock, global);
                }
            }
            this.FunctionDefinitionTokens[zeroFunction] = zeroFunctionBlock;
        }

        private void GenerateArrayInitializer(List<object> declarationOrder, Struct structInfo, bool forceOverwrite, bool createIfUnnecessary)
        {
            string constructorName = structInfo.Name + "@0";
            if (Functions.Contains(constructorName))
            {
                var function = Functions.Get(constructorName);
                string arrayInitializerName = structInfo.Name + "@2";
                bool anyMembersHaveArraySizes = AnyMembersHaveArraySizes(structInfo);

                //if (((anyMembersHaveArraySizes && ainFile.Version >= 6) || (ainFile.Version < 6)) &&
                //    (forceOverwrite || !Functions.Contains(arrayInitializerName)))
                if (anyMembersHaveArraySizes || createIfUnnecessary)
                {
                    var arrayInitializer = Functions.Get(arrayInitializerName);
                    declarationOrder.Add(arrayInitializer);
                    TokenExpression block = new TokenExpression(TokenType.Block);
                    foreach (var member in structInfo.Members)
                    {
                        if (member.DataType.IsNonRefArray() && this.ArraySizeExpressions.ContainsKey(member))
                        {
                            AddArrayDeclarationToBlock(block, member);
                        }
                    }

                    this.FunctionDefinitionTokens[arrayInitializer] = block;
                }
            }
        }

        private bool AnyMembersHaveArraySizes(Struct structInfo)
        {
            bool anyMembersHaveArraySizes = structInfo.Members.Any(m => this.ArraySizeExpressions.ContainsKey(m));
            return anyMembersHaveArraySizes;
        }

        private void AddArrayDeclarationToBlock(TokenExpression block, IVariable member)
        {
            block.Subexpressions.Add(new TokenExpression(member.Name));
            block.Subexpressions.Add(new TokenExpression("."));
            block.Subexpressions.Add(new TokenExpression("Alloc"));
            block.Subexpressions.Add(new TokenExpression("("));
            int count = 0;
            foreach (var numberFromForEach in this.ArraySizeExpressions[member].Subexpressions)
            {
                var number = numberFromForEach;
                if (count > 0)
                {
                    block.Subexpressions.Add(new TokenExpression(TokenType.Comma));
                }
                if (number.TokenType != TokenType.Number)
                {
                    var secondPass = new SecondPass(this);
                    var initialValue = secondPass.EvaluateConstExpression(number, DataType.Int, true);
                    if (initialValue != null && initialValue.DataType == DataType.Int)
                    {
                        number = new TokenExpression(initialValue.IntValue.ToString());
                    }
                    else
                    {
                        RaiseError("Unable to parse array size as a constant", number);
                    }
                }

                block.Subexpressions.Add(number.Clone());
                count++;
            }

            //block.Subexpressions.Add(this.ArraySizeExpressions[member].Clone());
            block.Subexpressions.Add(new TokenExpression(")"));
            block.Subexpressions.Add(new TokenExpression(";"));
        }

        private Variable[] GetVariablesInVariableDeclaration(TokenExpression variableDeclarationToken)
        {
            var dataTypeToken = variableDeclarationToken.Subexpressions[0];
            var variablesList = variableDeclarationToken.Subexpressions[1].Subexpressions;

            var dataType = GetDataType(dataTypeToken);

            List<Variable> variables = new List<Variable>();

            foreach (var entry in variablesList)
            {
                string variableName = entry.Subexpressions[0].Token.Value;
                var newVariable = new Variable(variableName, dataType.DataType, dataType.StructType, dataType.ArrayDimensions);
                newVariable.Root = ainFile;
                variables.Add(newVariable);
                if (entry.Subexpressions.Count > 1 && entry.Subexpressions[1].TokenType == TokenType.ArrayBoundsDeclaration)
                {
                    //due to bugs, it is treating expressions as separate dimension values, so parse it with the second pass first
                    var boundsDeclaration = entry.Subexpressions[1];
                    if (boundsDeclaration.Subexpressions.Count > 1)
                    {
                        var secondPass = new SecondPass(this);
                        var sizeTokens = secondPass.CompileTokens(boundsDeclaration.Subexpressions);
                        boundsDeclaration.Subexpressions.Clear();
                        boundsDeclaration.Subexpressions.AddRange(sizeTokens);
                    }

                    this.ArraySizeExpressions[newVariable] = boundsDeclaration;
                }
                if (newVariable.DataType.IsPrimitiveRefType())
                {
                    newVariable = new Variable("<void>", DataType.Void, 0, 0);
                    newVariable.Root = ainFile;
                    variables.Add(newVariable);
                }
            }

            return variables.ToArray();
        }

        private void CreateSymbolForConst(TokenExpression constDeclarationToken)
        {
            CreateSymbolForConst(constDeclarationToken, null);
        }

        private void CreateSymbolForConst(TokenExpression constDeclarationToken, string className)
        {
            var variables = GetVariablesInVariableDeclaration(constDeclarationToken.Subexpressions[1]);
            foreach (var variable in variables)
            {
                string constantName = variable.Name;
                if (!string.IsNullOrEmpty(className))
                {
                    constantName = className + "." + constantName;
                }

                var constant = Constants.Get(constantName);
                constant.SetVariableType(variable);
            }
        }

        private Function CreateSymbolForFunction(TokenExpression functionDeclarationToken, string className)
        {
            return CreateSymbolForFunction(functionDeclarationToken, className, true);
        }

        private Function CreateSymbolForFunction(TokenExpression functionDeclarationToken, string className, bool createSymbol)
        {
            var dataTypeToken = functionDeclarationToken.GetFirstSubexpression(TokenType.DataType);
            Variable dataType;
            if (dataTypeToken != null)
            {
                dataType = GetDataType(dataTypeToken);
            }
            else
            {
                dataType = new Variable("", DataType.Void, -1, 0);
            }
            var functionNameToken = functionDeclarationToken.GetFirstSubexpression(TokenType.FunctionName);
            string functionName = GetFunctionName(functionNameToken, className);

            var function = Functions.Get(functionName, createSymbol);
            function.SetVariableType(dataType);
            if (function.Parameters != null && function.Parameters.Count > 0)
            {
                function.Parameters.Clear();
            }


            var poundSignToken = functionDeclarationToken.GetFirstSubexpression(TokenType.Pound);
            if (poundSignToken != null)
            {
                function.IsLabel = 1;
            }


            //add parameters
            var functionParameterListToken = functionDeclarationToken.GetFirstSubexpression(TokenType.FunctionParameterList);
            if (functionParameterListToken != null)
            {
                var parameters = GetVariablesInFunctionParameters(functionParameterListToken);

                if (function.Parameters.Count < parameters.Length)
                {
                    function.Parameters.Clear();
                    foreach (var parameter in parameters)
                    {
                        int index = function.Parameters.Count;
                        parameter.Root = ainFile;
                        parameter.Parent = function;
                        parameter.Index = index;
                        function.Parameters.Add(parameter);
                    }
                    function.ParameterCount = function.Parameters.Count;
                }
            }

            return function;
        }

        private HllFunction CreateSymbolForLibraryFunction(TokenExpression functionDeclarationToken, string libraryName, string libraryAlias)
        {
            var dataTypeToken = functionDeclarationToken.GetFirstSubexpression(TokenType.DataType);
            Variable dataType;
            if (dataTypeToken != null)
            {
                dataType = GetDataType(dataTypeToken);
            }
            else
            {
                dataType = new Variable("", DataType.Void, -1, 0);
            }
            var functionNameToken = functionDeclarationToken.GetFirstSubexpression(TokenType.FunctionName);
            string functionName = GetFunctionName(functionNameToken);

            var library = Libraries.Get(libraryName);
            library.Root = ainFile;
            library.LibraryName = libraryName;
            string fullName = libraryAlias + "." + functionName;
            var libraryFunction = Symbols.GetOrNull(fullName) as HllFunction;
            if (libraryFunction == null)
            {
                libraryFunction = new HllFunction();
                libraryFunction.SetVariableType(dataType);
                libraryFunction.Name = functionName;
                libraryFunction.ParentLibrary = library;
                libraryFunction.Root = ainFile;
                libraryFunction.Index = library.Functions.Count;
                library.Functions.Add(libraryFunction);
                Symbols[fullName] = libraryFunction;
            }

            //add parameters
            var functionParameterListToken = functionDeclarationToken.GetFirstSubexpression(TokenType.FunctionParameterList);
            if (functionParameterListToken != null)
            {
                var parameters = GetVariablesInFunctionParameters(functionParameterListToken);

                if (libraryFunction.Parameters.Count < parameters.Length)
                {
                    libraryFunction.Parameters.Clear();
                    foreach (var parameter in parameters)
                    {
                        int index = libraryFunction.Parameters.Count;
                        if (parameter.DataType != DataType.Void)
                        {
                            var newParameter = new HllFunctionParameter();
                            newParameter.Parent = libraryFunction;
                            newParameter.Root = ainFile;
                            newParameter.Name = parameter.Name;
                            newParameter.Index = index;
                            newParameter.SetVariableType(parameter);
                            libraryFunction.Parameters.Add(newParameter);
                        }
                        else
                        {

                        }
                    }
                    libraryFunction.ParameterCount = libraryFunction.Parameters.Count;
                }
            }
            return libraryFunction;
        }

        private Variable[] GetVariablesInFunctionParameters(TokenExpression functionParameterListToken)
        {
            List<Variable> list = new List<Variable>();
            foreach (var entry in functionParameterListToken.Subexpressions)
            {
                var dataTypeToken = entry.Subexpressions[0];
                var parameterNameToken = entry.Subexpressions[1];

                var dataType = GetDataType(dataTypeToken);
                string parameterName = parameterNameToken.Token.Value;
                var newVariable = new Variable(parameterName, dataType.DataType, dataType.StructType, dataType.ArrayDimensions);
                list.Add(newVariable);

                if (dataType.DataType.IsPrimitiveRefType())
                {
                    var voidParameter = new Variable("<void>", DataType.Void, 0, 0);
                    list.Add(voidParameter);
                }
            }
            return list.ToArray();
        }

        private Function CreateSymbolForFunction(TokenExpression functionDeclarationToken)
        {
            return CreateSymbolForFunction(functionDeclarationToken, null);
        }
    }
}
