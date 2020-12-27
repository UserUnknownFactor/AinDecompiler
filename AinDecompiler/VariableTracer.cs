using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    [Flags]
    public enum VariableTraceMode
    {
        Invalid = -1,
        None = 0,
        DirectCopies = 1,
        Writes = 2,
        Reads = 4,
        Any = DirectCopies | Writes | Reads,
        LookInsideReferences = 8,
        LookInsideEverything = 16,
        Recursive = 32,
        DirectCopiesRecursive = DirectCopies | Recursive,
    }

    class VariableTracer
    {
        AinFile ainFile;
        bool recursive = false;
        VariableTraceMode mode = VariableTraceMode.None;
        IVariable lookFor;

        public VariableTracer(AinFile ainFile)
        {
            this.ainFile = ainFile;
        }

        HashSet<IVariable> matches = new HashSet<IVariable>();
        HashSet<IVariable> finished = new HashSet<IVariable>();

        public void EnqueueVariable(IVariable lookFor)
        {
            if (lookFor != null)
            {
                matches.Set(lookFor);
            }
        }

        public HashSet<IVariable> TraceVariable(IVariable lookFor, VariableTraceMode mode)
        {
            this.mode = mode;
            this.lookFor = lookFor;
            if (0 != (this.mode & VariableTraceMode.Recursive))
            {
                this.recursive = true;
            }

            EnqueueVariable(lookFor);
            RunQueue();
            matches.Remove(lookFor);
            return matches;
        }

        public void RunQueue()
        {
            do
            {
                var currentMatches = matches.ToArray();
                foreach (var variable in currentMatches)
                {
                    if (!finished.Contains(variable))
                    {
                        TraceVariableInternal(variable);
                    }
                }
                if (!recursive)
                {
                    break;
                }
            }
            while (finished.Count != matches.Count);
        }

        private void TraceVariableInternal(IVariable lookFor)
        {
            this.lookFor = lookFor;
            if (lookFor == null)
            {
                return;
            }

            var parentFunction = lookFor.Parent as Function;
            if (parentFunction != null)
            {
                var variable = lookFor as Variable;
                if (lookFor.Index >= parentFunction.ParameterCount)
                {
                    TraceLocalVariable(variable);
                }
                else
                {
                    TraceFunctionParameter(variable);
                }
            }
            var parentStruct = lookFor.Parent as Struct;
            if (parentStruct != null)
            {
                var variable = lookFor as Variable;
                TraceStructMember(variable);
            }
            var global = lookFor as Global;
            if (global != null)
            {
                TraceGlobal(global);
            }
            var function = lookFor as Function;
            if (function != null)
            {
                //this means we're looking for the return value of the function
                TraceFunction(function);
            }
            finished.Set(lookFor);
        }

        private void TraceFunction(Function function)
        {
            var usedBy = ainFile.CodeSearchCache.FunctionsCache.GetUsedBy(function);
            TraceInFunctions(usedBy, function);
            
            //also find "return" expressions too
            var rootExpression = ainFile.DecompiledCodeCache.GetDecompiledCode(function);
            foreach (var node in rootExpression.GetChildExpressions())
            {
                if (node.ExpressionType == Instruction.RETURN)
                {
                    TraceExpressionDown(node.Arg1);
                }
            }
        }

        private void TraceGlobal(Global global)
        {
            var usedBy = ainFile.CodeSearchCache.GlobalsCache.GetUsedBy(global);
            TraceInFunctions(usedBy, global);
        }

        private void TraceStructMember(Variable structMember)
        {
            var usedBy = ainFile.CodeSearchCache.GetFunctionsUsingStructMember(structMember);
            TraceInFunctions(usedBy, structMember);
        }

        private void TraceFunctionParameter(Variable parameter)
        {
            var parentFunction = (Function)parameter.Parent;
            //first trace it like a local variable
            TraceInFunction(parentFunction, parameter);

            //then trace calls to the function
            var usedBy = ainFile.CodeSearchCache.FunctionsCache.GetUsedBy(parentFunction);
            foreach (var function in usedBy)
            {
                TraceFunctionParameter(function, parentFunction, parameter);
            }
        }

        private void TraceLocalVariable(Variable localVariable)
        {
            var function = (Function)localVariable.Parent;
            TraceInFunction(function, localVariable);
        }

        void TraceInFunctions(IEnumerable<Function> functions, IVariable variable)
        {
            foreach (var function in functions)
            {
                TraceInFunction(function, variable);
            }
        }

        void TraceInFunction(Function function, IVariable variable)
        {
            var expressionMap = ainFile.DecompiledCodeCache.GetExpressionMap(function);
            var variableUsage = expressionMap.FindVariable(variable);

            foreach (var node in variableUsage)
            {
                TraceExpressionUp2(node.item);
            }
        }

        private void TraceExpressionUp2(Expression firstExpression)
        {
            var expression = firstExpression;
            if (expression == null)
            {
                return;
            }

            //Called on a instance of a variable expression in a function
            //Want to find:
            //  Look at operators and assignment, ? : operator, casting
            //  Copying to a function arguments
            //  Return values
            //Types of searches:
            //  Direct copies of variables, ignoring casting
            //  Other variables that appear in a variable read
            //  Other variables that appear in a variable write

            var lookForVariable = firstExpression.Variable;
            if (lookForVariable != null) lookForVariable = lookForVariable.Canonicalize();

            //IVariable functionParameter = null;
            IVariable lValue = null;
            IVariable rValue = null;

            //look for a parent expression by seeking through operators
            while (true)
            {
                var parent = expression.Parent;
                if (parent == null || parent.ExpressionType == Instruction.Statement)
                {
                    break;
                }
                //check for Return
                if (parent.ExpressionType == Instruction.RETURN)
                {
                    expression = parent;
                    lValue = parent.Root.Variable;
                    break;
                }
                //check for function call or comma
                if (parent.ExpressionType.IsFunctionCall() || parent.ExpressionType == Instruction.Comma)
                {
                    //functionParameter = expression.GetFunctionCallParameter();
                    lValue = expression.GetFunctionCallParameter();
                    break;
                }
                //check for cast operator or expression
                if (!ExpressionIsOperatorOrCast(parent))
                {
                    break;
                }
                expression = parent;
            }

            Expression e2 = SkipCastExpressions(expression);

            //is this an assignment expression?
            if (lValue == null)
            {
                if (expression.ExpressionType.IsAssignment())
                {
                    lValue = expression.Variable;
                    var lValueCanon = lValue.Canonicalize();
                    var arg1ExpressionType = expression.Arg1.ExpressionType;
                    if (lValueCanon == null && (arg1ExpressionType == Instruction.REF || arg1ExpressionType == Instruction.ArrayIndex))
                    {
                        lValue = expression.Arg1.Arg1.Variable;
                    }
                    var arg2 = SkipCastExpressions(expression.Arg2);
                    if (arg2 != null)
                    {
                        rValue = arg2.Variable;
                    }
                    if (rValue != null)
                    {
                        rValue = rValue.Canonicalize();
                    }
                }
            }
            else
            {
                rValue = e2.Variable;
                if (rValue != null)
                {
                    rValue = rValue.Canonicalize();
                }
                if (rValue == null)
                {
                    e2 = SkipCastExpressions(firstExpression);
                    rValue = e2.Variable;
                    if (rValue != null)
                    {
                        rValue = rValue.Canonicalize();
                    }
                }
            }

            //fixme, lvalue and rvalue stuff got weird
            //Are we directly pointing to the variable? (excluding cast operations)
            //Expression e2 = SkipCastExpressions(expression);
            bool isDirect = false;
            var v2 = e2.Variable;
            IVariable v2Canon = null;
            if (v2 != null)
            {
                v2Canon = v2.Canonicalize();
                if (v2Canon == v2 && !(expression.ExpressionType.IsAssignment() && !expression.ExpressionType.IsDirectAssignment()) && (lookForVariable != null && lookForVariable.DataType == v2.DataType))
                {
                    isDirect = true;
                }
                else if (v2Canon == v2 && (lookForVariable != null && lookForVariable.DataType == v2.DataType) && (expression.ExpressionType == Instruction.DG_PLUSA || expression.ExpressionType == Instruction.DG_MINUSA || expression.ExpressionType == Instruction.DG_ASSIGN))
                {
                    isDirect = true;
                }
            }

            if (0 != (mode & VariableTraceMode.DirectCopies))
            {
                if (isDirect && (lValue == lookFor || v2 == lookFor || rValue == lookFor))
                {
                    EnqueueVariable(v2);
                    if (!(lookFor.DataType == DataType.Functype && lValue != null && lValue.DataType != DataType.Functype))
                    {
                        EnqueueVariable(lValue);
                    }
                    if (!(lookFor.DataType == DataType.Functype && rValue != null && rValue.DataType != DataType.Functype))
                    {
                        EnqueueVariable(rValue);
                    }
                }
            }
            if (0 != (mode & VariableTraceMode.Writes))
            {
                if (lValue == lookFor)
                {
                    TraceExpressionDown(expression);
                }
            }
            if (0 != (mode & VariableTraceMode.Reads))
            {
                var lValueCanon = lValue.Canonicalize();
                if (lValueCanon != null)
                {
                    EnqueueVariable(lValueCanon);
                }
                TraceExpressionDown(expression);
            }
        }

        //private void TraceExpressionUp(Expression firstExpression)
        //{
        //    //Look For These operations:
        //    //operators and assignment
        //    // ? : operator (special kind of if-else operator)
        //    //casting

        //    //Not These:
        //    //array indexing
        //    //struct member operator
        //    //if-else, while, for

        //    var expression = firstExpression;
        //    if (expression == null)
        //    {
        //        return;
        //    }

        //    while (true)
        //    {
        //        var parent = expression.Parent;
        //        if (parent == null || parent.ExpressionType == Instruction.Statement)
        //        {
        //            break;
        //        }
        //        //check for a Return, this indicates that the variable influences the return value of the function.
        //        if (parent.ExpressionType == Instruction.RETURN)
        //        {
        //            var root = parent.Root;
        //            if (root != null)
        //            {
        //                var function = root.Variable as Function;
        //                if (function != null)
        //                {
        //                    EnqueueVariable(function);
        //                }
        //            }
        //        }
        //        if (!ExpressionIsOperatorOrCast(parent))
        //        {
        //            break;
        //        }
        //        expression = parent;
        //    }
        //    TraceExpressionDown(expression);
        //}

        private void TraceExpressionDown(Expression expression)
        {
            if (ExpressionIsOperatorOrCast(expression))
            {
                foreach (var child in expression.Args)
                {
                    if (child != null)
                    {
                        TraceExpressionDown(child);
                    }
                }
            }
            else
            {
                var variable = expression.Variable;
                if (variable != null)
                {
                    variable = variable.Canonicalize();
                }
                if (variable != null)
                {
                    if (variable.Index != -1)
                    {
                        EnqueueVariable(variable);
                    }
                }
            }
        }

        //private bool TraceExpressionDown2(Expression expression)
        //{
        //    if (ExpressionIsOperatorOrCast(expression))
        //    {
        //        foreach (var child in expression.Args)
        //        {
        //            if (child != null)
        //            {
        //                TraceExpressionDown(child);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //    return true;
        //}

        private static Expression SkipCastExpressions(Expression expression)
        {
            if (expression == null)
            {
                return null;
            }
            while (expression.ExpressionType.IsCast() && expression.Arg1 != null)
            {
                expression = expression.Arg1;
            }
            return expression;
        }

        private static bool ExpressionIsOperatorOrCast(Expression expression)
        {
            //accept cast operators and arithmetic operators, reject everything else
            if (ExpressionDisplayer.castOperations.Contains(expression.ExpressionType))
            {

            }
            else if (ExpressionDisplayer.OperatorTable.ContainsKey(expression.ExpressionType))
            {
                if (expression.ExpressionType == Instruction.Comma)
                {
                    return false;
                }
                if (expression.ExpressionType == Instruction.IfElse)
                {
                    var parent2 = expression.Parent;
                    if (parent2 == null || parent2.ExpressionType == Instruction.Statement)
                    {
                        return false;
                    }
                }
            }
            else if (expression.ExpressionType == Instruction.REF || expression.ExpressionType == Instruction.ArrayIndex)
            {
                //check for array index operator
                if (expression.Arg1 != null)
                {
                    var arg1Variable = expression.Arg1.Variable;
                    if (arg1Variable.DataType.IsArray())
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                switch (expression.ExpressionType)
                {
                    case Instruction.DG_MINUSA:
                    case Instruction.DG_PLUSA:
                    case Instruction.DG_ASSIGN:
                    case Instruction.DG_SET:
                    case Instruction.DG_STR_TO_METHOD:
                        //operators
                        return true;
                    case Instruction.DG_COPY:
                    case Instruction.DG_NEW_FROM_METHOD:
                        //like casting
                        return true;
                    //case Instruction.DG_ADD:  //???
                    case Instruction.DG_CALL:
                    case Instruction.DG_CALLBEGIN:
                    case Instruction.DG_CLEAR:
                    case Instruction.DG_NUMOF:
                        //like function calls
                        return false;
                        //case Instruction.DG_ERASE:  //???
                        //case Instruction.DG_EXIST:  //???
                        //case Instruction.DG_NEW:  //???
                        //case Instruction.DG_POP:
                        break;
                }

                return false;
            }
            return true;
        }

        private void TraceFunctionParameter(Function function, Function lookFor, Variable parameter)
        {
            int parameterIndex = parameter.Index;

            var expressionMap = ainFile.DecompiledCodeCache.GetExpressionMap(function);
            var variableUsage = expressionMap.FindVariable(lookFor);

            foreach (var node in variableUsage)
            {
                var expression = node.item;
                var argument = expression.GetFunctionCallArgument(parameterIndex);

                TraceExpressionUp2(argument);
            }
        }

        public void FindFunctionTypes()
        {
            FindFunctionTypesMain(false);
            FindFunctionTypesRemaining(false);
            FindFunctionTypesMain(true);
            FindFunctionTypesRemaining(true);

            SetMetadata(ainFile.Functions.Cast<IVariable>());
            SetMetadata(ainFile.Globals.Cast<IVariable>());
            SetMetadata(ainFile.Structs.Cast<IVariable>());
            ainFile.SaveMetadata();
        }

        private void SetMetadata(IEnumerable<IVariable> vars)
        {
            foreach (var variable in vars)
            {
                SetMetadata(variable);
                var func = variable as IFunction;
                if (func != null)
                {
                    SetMetadata(func.Parameters);
                }
            }
        }

        private void SetMetadata(IVariable variable)
        {
            if (variable.StructType != -1 && variable.DataType.IsFunction())
            {
                var metaData = variable.GetMetadata();
                if (metaData == null)
                {
                    metaData = ainFile.MetadataFile.Metadata.GetOrAddNew(variable);
                    metaData.FuncTypeIndex = variable.StructType;
                }
                variable.StructType = -1;
            }
        }

        private void FindFunctionTypesMain(bool makeNewStuffUp)
        {
            foreach (var function in ainFile.Functions)
            {
                if (function.Name.EndsWith("CADVCallFunction@ADV_CallFunction"))
                {
                    //specific blacklisted function
                    continue;
                }
                int address = function.Address;
                int endAddress = Decompiler.FindEndAddress(address, ainFile);
                while (address < endAddress)
                {
                    InstructionInfo ins = ainFile.Peek(address);
                    if (ins.instruction == Instruction.FUNC || ins.instruction == Instruction.ENDFUNC)
                    {
                        break;
                    }
                    if (ins.instruction == Instruction.CALLFUNC2 || ins.instruction == Instruction.FT_ASSIGNS)
                    {
                        FindFunctionTypes(function, makeNewStuffUp);
                        break;
                    }
                    if (ins.instruction == Instruction.DG_CALLBEGIN)
                    {
                        FindFunctionTypes(function, makeNewStuffUp);
                        break;
                    }
                    if (ins.instruction == Instruction.DG_STR_TO_METHOD)
                    {
                        FindFunctionTypes(function, makeNewStuffUp);
                        break;
                    }
                    if (ins.instruction == Instruction.DG_NEW_FROM_METHOD)
                    {
                        FindFunctionTypes(function, makeNewStuffUp);
                        break;
                    }
                    address = ins.nextAddress;
                }
            }
        }

        private void FindFunctionTypesRemaining(bool MakeNewStuffUp)
        {
            HashSet<Variable> failingStructMembers = new HashSet<Variable>();
            HashSet<IVariable> failingVariables = new HashSet<IVariable>();
            HashSet<Global> failingGlobals = new HashSet<Global>();

            //check for anything missed
            foreach (var structInfo in ainFile.Structs)
            {
                foreach (var member in structInfo.Members)
                {
                    if (member.DataType.IsFuncTypeOrDelegate() && member.StructType == -1)
                    {
                        bool success = FindFunctionTypeForStructMember(member, MakeNewStuffUp);
                        if (!success)
                        {
                            failingStructMembers.Add(member);
                        }
                    }
                }
            }

            foreach (var function in ainFile.Functions)
            {
                if (function.Name.EndsWith("CADVCallFunction@ADV_CallFunction"))
                {
                    //specific blacklisted function
                    continue;
                }
                if (function.DataType.IsFuncTypeOrDelegate() && function.StructType == -1)
                {
                    bool success = FindFunctionTypeForVariable(function, MakeNewStuffUp);
                    if (!success)
                    {
                        failingVariables.Add(function);
                    }
                }
                foreach (var variable in function.Parameters)
                {
                    if (variable.DataType.IsFuncTypeOrDelegate() && variable.StructType == -1)
                    {
                        bool success = FindFunctionTypeForVariable(variable, MakeNewStuffUp);
                        if (!success)
                        {
                            failingVariables.Add(variable);
                        }
                    }
                }
            }

            foreach (var global in ainFile.Globals)
            {
                if (global.DataType.IsFuncTypeOrDelegate() && global.StructType == -1)
                {
                    bool success = FindFunctionTypeForGlobal(global, MakeNewStuffUp);
                    if (!success)
                    {
                        failingGlobals.Add(global);
                    }
                }
            }


            int lastFailCount = 0;
            int totalFailCount = failingStructMembers.Count + failingVariables.Count + failingGlobals.Count;
            while (totalFailCount != lastFailCount && totalFailCount > 0)
            {
                lastFailCount = totalFailCount;
                var failingStructMembersCopy = failingStructMembers.ToArray();
                foreach (var member in failingStructMembersCopy)
                {
                    if (member.StructType == -1)
                    {
                        bool success = FindFunctionTypeForStructMember(member, MakeNewStuffUp);
                        if (success)
                        {
                            failingStructMembers.Remove(member);
                        }
                    }
                }
                var failingVariablesCopy = failingVariables.ToArray();
                foreach (var variable in failingVariablesCopy)
                {
                    if (variable.StructType == -1)
                    {
                        bool success = FindFunctionTypeForVariable(variable, MakeNewStuffUp);
                        if (success)
                        {
                            failingVariables.Remove(variable);
                        }
                    }
                }
                var failingGlobalsCopy = failingGlobals.ToArray();
                foreach (var global in failingGlobalsCopy)
                {
                    if (global.StructType == -1)
                    {
                        bool success = FindFunctionTypeForGlobal(global, MakeNewStuffUp);
                        if (success)
                        {
                            failingGlobals.Remove(global);
                        }
                    }
                }
                totalFailCount = failingStructMembers.Count + failingVariables.Count + failingGlobals.Count;
            }
        }

        private bool FindFunctionTypeForVariable(IVariable variable, bool MakeNewStuffUp)
        {
            var function = variable.Parent as Function;
            if (function == null)
            {
                function = variable as Function;
            }
            if (function != null)
            {
                var code = ainFile.DecompiledCodeCache.GetDecompiledCode(function);
                return FindFunctionTypeForVariable(variable, code, MakeNewStuffUp);
            }
            return false;
        }

        private bool FindFunctionTypeForStructMember(Variable member, bool MakeNewStuffUp)
        {
            var functionsUsingStructMember = ainFile.CodeSearchCache.GetFunctionsUsingStructMember(member);
            foreach (var function in functionsUsingStructMember)
            {
                var code = ainFile.DecompiledCodeCache.GetDecompiledCode(function);
                bool found = FindFunctionTypeForVariable(member, code, MakeNewStuffUp);
                if (found)
                {
                    return true;
                }
            }
            return false;
        }

        private bool FindFunctionTypeForGlobal(Global global, bool MakeNewStuffUp)
        {
            var functionsUsingGlobal = ainFile.CodeSearchCache.GlobalsCache.GetUsedBy(global);
            foreach (var function in functionsUsingGlobal)
            {
                var code = ainFile.DecompiledCodeCache.GetDecompiledCode(function);
                bool found = FindFunctionTypeForVariable(global, code, MakeNewStuffUp);
                if (found)
                {
                    return true;
                }
            }
            return false;
        }

        private bool FindFunctionTypeForVariable(IVariable variable, Expression code, bool MakeNewStuffUp)
        {
            bool variableIsFunction = variable as Function != null;
            bool variableIsFuncType = variable.DataType.IsFuncType();
            bool variableIsDelegate = variable.DataType.IsDelegate();

            foreach (var e in code.GetChildExpressions())
            {
                Expression otherExpression = null;
                IVariable expVariable = null;
                if (e.ExpressionType == Instruction.CALLFUNC2 && variableIsFuncType)
                {
                    bool isVoid = e.IsVoidContext();
                    if (!isVoid)
                    {
                        otherExpression = e.GetOtherSideOfBinaryExpression();
                    }
                    IVariable otherExpressionVariable = null;
                    if (otherExpression != null)
                    {
                        otherExpressionVariable = otherExpression.Variable;
                    }

                    DataType otherExpressionDataType = DataType.Void;
                    int otherExpressionStructType = -1;
                    if (otherExpressionVariable != null)
                    {
                        otherExpressionDataType = otherExpressionVariable.DataType;
                        otherExpressionStructType = otherExpressionVariable.StructType;
                    }
                    else
                    {
                        if (isVoid)
                        {
                            otherExpressionDataType = DataType.Void;
                        }
                        else
                        {
                            otherExpressionDataType = DataType.AnyNonVoidType;
                        }
                    }

                    var argExpressions = (e.Arg3 ?? Expression.Empty).FlattenContainerExpression(Instruction.Comma).ToArray();
                    var variables = GetVariablesFromExpressions(argExpressions);
                    var matchingFuncTypes = ainFile.MatchingFunctionTypes(otherExpressionDataType, otherExpressionStructType, variables, variables.Length).Distinct().ToArray();

                    var firstMatchingFuncType = matchingFuncTypes.FirstOrDefault();
                    if (firstMatchingFuncType != null)
                    {
                        if (MakeNewStuffUp || matchingFuncTypes.Skip(1).FirstOrDefault() == null)
                        {
                            variable.StructType = firstMatchingFuncType.Index;
                        }
                    }
                }
                if (variableIsFunction && e.ExpressionType == Instruction.RETURN)
                {
                    otherExpression = e.Arg1;
                    expVariable = variable;
                }
                else
                {
                    expVariable = e.Variable;
                    if (variable == expVariable)
                    {
                        otherExpression = e.GetOtherSideOfBinaryExpression();
                    }

                }

                if (otherExpression != null)
                {
                    Function otherFunction = null;
                    if (otherExpression.ExpressionType == Instruction.PUSH)
                    {
                        otherFunction = ainFile.GetFunction(otherExpression.Value);
                    }
                    else if (otherExpression.ExpressionType == Instruction.S_PUSH)
                    {
                        otherFunction = ainFile.GetFunction(ainFile.GetString(otherExpression.Value));
                    }
                    else
                    {
                        var otherVariable = otherExpression.Variable;
                        if (otherVariable != null && !variableIsDelegate && otherVariable.DataType.IsFuncType() && otherVariable.StructType != -1)
                        {
                            variable.StructType = otherVariable.StructType;
                            return true;
                        }
                        if (otherVariable != null && variableIsDelegate && otherVariable.DataType.IsDelegate() && otherVariable.StructType != -1)
                        {
                            variable.StructType = otherVariable.StructType;
                            return true;
                        }
                    }
                    if (otherFunction != null)
                    {
                        FunctionType matchingFuncType = null;
                        if (!variableIsDelegate)
                        {
                            if (!MakeNewStuffUp)
                            {
                                matchingFuncType = ainFile.GetFuncTypeUnique(otherFunction);
                            }
                            else
                            {
                                matchingFuncType = ainFile.GetFuncType(otherFunction);
                            }
                            if (matchingFuncType != null)
                            {
                                variable.StructType = matchingFuncType.Index;
                                return true;
                            }
                        }
                        else
                        {
                            if (!MakeNewStuffUp)
                            {
                                matchingFuncType = ainFile.GetDelegateUnique(otherFunction);
                            }
                            else
                            {
                                matchingFuncType = ainFile.GetDelegateType(otherFunction);
                            }
                            if (matchingFuncType != null)
                            {
                                variable.StructType = matchingFuncType.Index;
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    var functionParameter = e.GetFunctionCallParameter();
                    if (functionParameter != null)
                    {
                        if (functionParameter != null && !variableIsDelegate && functionParameter.DataType.IsFuncType() && functionParameter.StructType != -1)
                        {
                            variable.StructType = functionParameter.StructType;
                            return true;
                        }
                        if (functionParameter != null && variableIsDelegate && functionParameter.DataType.IsDelegate() && functionParameter.StructType != -1)
                        {
                            variable.StructType = functionParameter.StructType;
                            return true;
                        }
                    }
                    else if (variable.DataType.IsArray())
                    {
                        var parent = e.Parent;
                        if (parent != null && (parent.ExpressionType == Instruction.A_PUSHBACK || parent.ExpressionType == Instruction.A_INSERT))
                        {
                            var arg2 = parent.Arg2;
                            IVariable var2 = null;
                            if (arg2 != null) var2 = arg2.Variable;
                            if (var2 != null)
                            {
                                if (!variableIsDelegate && var2.DataType.IsFuncType() && var2.StructType != -1)
                                {
                                    variable.StructType = var2.StructType;
                                    return true;
                                }
                                if (variableIsDelegate && var2.DataType.IsDelegate() && var2.StructType != -1)
                                {
                                    variable.StructType = var2.StructType;
                                    return true;
                                }
                            }
                        }
                    }
                    else if (e.ExpressionType == Instruction.CALLFUNC2)
                    {

                    }
                }
            }
            return false;
        }

        private static IVariable[] GetVariablesFromExpressions(Expression[] argExpressions)
        {
            var variables = argExpressions.Select(ex => ex.Variable).ToArray();
            return variables;
        }

        private void FindFunctionTypes(Function function)
        {
            FindFunctionTypes(function, false);
        }

        private void FindFunctionTypes(Function function, bool makeNewStuffUp)
        {
            var code = ainFile.DecompiledCodeCache.GetDecompiledCode(function);

            foreach (var e in code.GetChildExpressions())
            {
                if (e.ExpressionType == Instruction.CALLFUNC2 || e.ExpressionType == Instruction.FT_ASSIGNS)
                {
                    int funcTypeIndex = -1;
                    IVariable funcTypeVariable = null;
                    if (e.ExpressionType == Instruction.CALLFUNC2)
                    {
                        funcTypeIndex = e.Arg2.Value;
                        funcTypeVariable = e.Arg1.Variable.Canonicalize();
                    }
                    else if (e.ExpressionType == Instruction.FT_ASSIGNS)
                    {
                        funcTypeIndex = e.Arg3.Value;
                        funcTypeVariable = e.Arg1.Variable.Canonicalize();
                    }
                    if (funcTypeVariable == null)
                    {
                        var arg1 = e.Arg1;
                        if (arg1 != null) arg1 = arg1.Arg1;
                        if (arg1 != null) funcTypeVariable = arg1.Variable;
                        if (funcTypeVariable != null) funcTypeVariable = funcTypeVariable.Canonicalize();
                    }
                    if (funcTypeVariable != null && funcTypeVariable.StructType == -1)
                    {
                        matches.Clear();
                        finished.Clear();

                        var traceResults = TraceVariable(funcTypeVariable, VariableTraceMode.DirectCopiesRecursive);

                        foreach (var res in traceResults)
                        {
                            if (res.DataType.IsFuncType())
                            {
                                res.StructType = funcTypeIndex;
                            }
                        }
                        funcTypeVariable.StructType = funcTypeIndex;
                    }
                }
                else if (e.ExpressionType == Instruction.DG_CALLBEGIN)
                {
                    int delegateIndex = -1;
                    IVariable delegateVariable = null;
                    if (e.ExpressionType == Instruction.DG_CALLBEGIN)
                    {
                        delegateIndex = e.Value;
                        delegateVariable = e.Arg2.Variable.Canonicalize();
                    }
                    if (delegateVariable == null)
                    {
                        var arg1 = e.Arg2;
                        if (arg1 != null) arg1 = arg1.Arg1;
                        if (arg1 != null) delegateVariable = arg1.Variable;
                        if (delegateVariable != null) delegateVariable = delegateVariable.Canonicalize();
                    }
                    if (delegateVariable != null && delegateVariable.StructType == -1)
                    {
                        matches.Clear();
                        finished.Clear();
                        var traceResults = TraceVariable(delegateVariable, VariableTraceMode.DirectCopiesRecursive);

                        foreach (var res in traceResults)
                        {
                            if (res.DataType.IsDelegate())
                            {
                                res.StructType = delegateIndex;
                            }
                        }
                        delegateVariable.StructType = delegateIndex;
                    }

                }
                else if (e.ExpressionType == Instruction.DG_STR_TO_METHOD)
                {
                    var otherExpression = e.GetOtherSideOfBinaryExpression();
                    int delegateIndex = -1;
                    if (e.Arg2.ExpressionType == Instruction.PUSH)
                    {
                        delegateIndex = e.Arg2.Value;
                    }
                    IVariable delegateVariable = null;
                    if (otherExpression == null)
                    {
                        delegateVariable = e.GetFunctionCallParameter();
                        if (e.Parent != null && e.Parent.ExpressionType == Instruction.DG_ADD)
                        {
                            delegateVariable = e.Parent.Arg1.Variable.Canonicalize();
                        }
                        else
                        {

                        }
                    }
                    else
                    {
                        delegateVariable = otherExpression.Variable;
                        if (delegateVariable != null)
                        {
                            delegateVariable = delegateVariable.Canonicalize();
                        }
                        else
                        {

                        }
                    }
                    if (delegateVariable != null && delegateVariable.StructType == -1 && delegateIndex != -1)
                    {
                        matches.Clear();
                        finished.Clear();
                        var traceResults = TraceVariable(delegateVariable, VariableTraceMode.DirectCopiesRecursive);

                        foreach (var res in traceResults)
                        {
                            if (res.DataType.IsDelegate())
                            {
                                res.StructType = delegateIndex;
                            }
                        }
                        delegateVariable.StructType = delegateIndex;
                    }
                }
                else if (e.ExpressionType == Instruction.DG_NEW_FROM_METHOD)
                {
                    var otherExpression = e.GetOtherSideOfBinaryExpression();
                    IVariable delegateVariable = null;
                    if (otherExpression == null)
                    {
                        delegateVariable = e.GetFunctionCallParameter();
                        if (e.Parent != null && e.Parent.ExpressionType == Instruction.DG_ADD)
                        {
                            delegateVariable = e.Parent.Arg1.Variable.Canonicalize();
                        }
                        else if (delegateVariable == null)
                        {

                        }
                    }
                    else
                    {
                        delegateVariable = otherExpression.Variable;
                    }
                    if (delegateVariable != null)
                    {
                        Function assignedFunction = null;
                        if (e.Arg2.ExpressionType == Instruction.PUSH)
                        {
                            assignedFunction = ainFile.GetFunction(e.Arg2.Value);
                        }
                        if (assignedFunction != null)
                        {
                            var matchingTypes = ainFile.MatchingDelegates(assignedFunction);
                            FunctionType firstMatch = null, secondMatch = null;
                            foreach (var m in matchingTypes)
                            {
                                if (firstMatch == null)
                                {
                                    firstMatch = m;
                                }
                                else
                                {
                                    secondMatch = m;
                                    break;
                                }
                            }
                            if (firstMatch != null && (secondMatch == null || makeNewStuffUp))
                            {
                                int delegateIndex = firstMatch.Index;

                                if (delegateVariable.StructType == -1)
                                {
                                    matches.Clear();
                                    finished.Clear();
                                    var traceResults = TraceVariable(delegateVariable, VariableTraceMode.DirectCopiesRecursive);

                                    foreach (var res in traceResults)
                                    {
                                        if (res.DataType.IsDelegate())
                                        {
                                            res.StructType = delegateIndex;
                                        }
                                    }
                                    delegateVariable.StructType = delegateIndex;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
