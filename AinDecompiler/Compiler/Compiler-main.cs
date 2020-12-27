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
        //VariableDictionaryWithParent scope = new DictionaryWithParent<string, IVariable>();
        //VariableMultiDictionary scope = new VariableMultiDictionary();

        VariableDictionaryWithParent localScope = null;
        VariableDictionaryWithParent firstLocalScope = null;
        Dictionary<string, Dictionary<string, IVariable>> ClassNameToMembers = new Dictionary<string, Dictionary<string, IVariable>>();

        Dictionary<string, IVariable> GetClassMembers(string className)
        {
            if (String.IsNullOrEmpty(className))
            {
                return null;
            }
            if (ClassNameToMembers.ContainsKey(className))
            {
                return ClassNameToMembers[className];
            }
            if (this.Structs.Contains(className))
            {
                Dictionary<string, IVariable> results = new Dictionary<string, IVariable>();
                var structInfo = this.Structs.Get(className);
                string classNameAt = className + "@";
                string classNameDot = className + ".";
                var methods = this.ainFile.Functions.Where(f => f.Name.StartsWith(classNameAt, StringComparison.Ordinal)).ToArray();
                var constants = this.Constants.List.Where(c => c.Name.StartsWith(classNameDot, StringComparison.Ordinal)).ToArray();

                foreach (var method in methods)
                {
                    results.Add(method.GetFunctionName(), method);
                }
                foreach (var member in structInfo.Members)
                {
                    results[member.Name] = member;
                }
                foreach (var constant in constants)
                {
                    string constantName = constant.Name.Substring(constant.Name.IndexOf('.') + 1);
                    results.Add(constantName, constant);
                }
                ClassNameToMembers[className] = results;
                return results;
            }
            else
            {
                ClassNameToMembers[className] = null;
                return null;
            }
        }

        int DummyNumber = 0;
        Function currentFunction;
        bool GenerateLockPeek = true;
        Dictionary<string, int> LabelNameToIndex = new Dictionary<string, int>();
        HashSet<string> UsedLabelNames = new HashSet<string>();
        List<string> LabelsIndexToName = new List<string>();

        Dictionary<Expression, VariableDictionaryWithParent> GotoScopes = new Dictionary<Expression, VariableDictionaryWithParent>();
        Dictionary<int, VariableDictionaryWithParent> LabelScopes = new Dictionary<int, VariableDictionaryWithParent>();

        private void GenerateExpressions()
        {
            //this.scope = new VariableMultiDictionary();

            foreach (var pair in this.FunctionDefinitionTokens)
            {
                //this.DummyNumber = 0;
                var function = pair.Key;
                var token = pair.Value;

                this.currentFunction = function;
                this.LabelNameToIndex.Clear();
                this.UsedLabelNames.Clear();
                this.LabelsIndexToName.Clear();
                this.GotoScopes.Clear();
                this.LabelScopes.Clear();

                string className = function.GetClassName();
                var classScope2 = GetClassMembers(className);
                var classScope = new VariableDictionaryWithParent(Symbols, classScope2);
                var localScope2 = new Dictionary<string, IVariable>();
                var localScope = new VariableDictionaryWithParent(classScope, localScope2);
                this.firstLocalScope = localScope;
                this.localScope = localScope;
                if (classScope2 == null)
                {
                    localScope.Parent = this.Symbols;
                }

                var localVars = function.Parameters;
                foreach (var localvar in localVars)
                {
                    localScope2[localvar.Name] = localvar;
                }

                if (classScope != null)
                {
                    //scope.PushDictionary(classScope);
                }
                //scope.PushDictionary(localScope);
                Expression.defaultAinFile = this.ainFile;
                Expression.defaultFunctionNumber = function.Index;

                List<Expression> expressionList = new List<Expression>();
                if (function.Name.EndsWith("@0"))
                {
                    string initializerFunctionName = function.Name.Substring(0, function.Name.Length - 2) + "@2";
                    if (Functions.Contains(initializerFunctionName))
                    {
                        var initializerFunction = Functions.Get(initializerFunctionName);
                        var initializerCallExpression = new Expression(Instruction.CALLMETHOD, -1, new Expression(Instruction.PUSHSTRUCTPAGE, -1), initializerFunction.Index);
                        expressionList.Add(initializerCallExpression);
                    }
                }
                GenerateBlockExpressionList(token, expressionList);

                if (GotoScopes.Count > 0)
                {
                    FixGotos();
                }

                Expression returnExpression;
                if (currentFunction.IsLabel == 1)
                {
                    returnExpression = null;
                }
                else if (expressionList.Count == 1 && expressionList[0].ExpressionType == Instruction.RETURN && expressionList[0].Arg1 == null)
                {
                    returnExpression = null;
                }
                else
                {
                    returnExpression = GetReturnExpression(function.DataType);
                }
                var endFuncExpression = new Expression(Instruction.ENDFUNC, -1, function.Index);

                if (returnExpression != null)
                {
                    expressionList.Add(returnExpression);
                }

                var expr = ConvertToStatements(expressionList);

                Peephole2(expr);

                //add a FUNC, Return, ENDFUNC to the function
                var funcExpression = new Expression(Instruction.FUNC, -1, function.Index);
                funcExpression.Arg1 = expr;
                funcExpression.Arg2 = endFuncExpression;

                this.FunctionDefinitions[function] = funcExpression;
                //scope.PopDictionary();
                if (classScope != null)
                {
                    //scope.PopDictionary();
                }
            }
            //scope.PopDictionary();
        }

        private static Expression GetReturnExpression(DataType dataType)
        {
            var returnInstruction = new Expression(Instruction.RETURN, -1);
            if (dataType == DataType.Int || dataType == DataType.Lint || dataType == DataType.Bool)
            {
                returnInstruction.Arg1 = new Expression(Instruction.PUSH, -1, 0);
            }
            else if (dataType == DataType.String)
            {
                returnInstruction.Arg1 = new Expression(Instruction.S_PUSH, -1, 0);
            }
            else if (dataType == DataType.Float)
            {
                returnInstruction.Arg1 = new Expression(Instruction.F_PUSH, -1, 0);
            }
            else if (dataType == DataType.Void)
            {

            }
            else if (dataType == DataType.RefString)
            {
                returnInstruction.Arg1 = new Expression(Instruction.PUSH, -1, -1);
            }
            else if (dataType == DataType.Struct)
            {
                returnInstruction.Arg1 = new Expression(Instruction.PUSH, -1, -1);
            }
            else if (dataType == DataType.RefStruct)
            {
                returnInstruction.Arg1 = new Expression(Instruction.PUSH, -1, -1);  //fixed up later by CodeGenerator class
            }
            else if (dataType.IsPrimitiveRefType())
            {
                returnInstruction.Arg1 = new Expression(Instruction.AssignmentRef, -1, new Expression(Instruction.PUSH, -1, -1), new Expression(Instruction.PUSH, -1, 0));
            }
            else if (dataType == DataType.Functype)
            {
                returnInstruction.Arg1 = new Expression(Instruction.PUSH, -1, 0);
            }
            else if (dataType == DataType.Delegate || dataType == DataType.RefDelegate)
            {
                returnInstruction.Arg1 = new Expression(Instruction.PUSH, -1, -1);
            }
            return returnInstruction;
        }

        bool peepholeDontShortenSRef = false;
        bool peepholeDontShortenSrRef = false;

        private void Peephole2(Expression baseExpression)
        {
            if (this.UseSimplifiedCode)
            {
                PeepholeSimplified(baseExpression);
                return;
            }

        restart:
            if (baseExpression == null) return;
            if (baseExpression.ExpressionType == Instruction.Statement &&
                baseExpression.Args.Count == 2 &&
                (baseExpression.Arg2 ?? Expression.Empty).ExpressionType == Instruction.Statement &&
                (baseExpression.Arg1 ?? Expression.Empty).ExpressionType != Instruction.Statement)
            {
                Peephole2(baseExpression.Arg1);
                if (ainFile.Version >= 6) { PeepHoleV6(baseExpression.Arg1); }
                baseExpression = baseExpression.Arg2;
                goto restart;
            }
            else
            {
                for (int i = 0; i < baseExpression.Args.Count; i++)
                {
                    var expr = baseExpression.Args[i];
                    Peephole2(expr);
                    if (ainFile.Version >= 6) { PeepHoleV6(expr); }
                }
            }

            if (baseExpression.ExpressionType == Instruction.Statement)
            {
                return;
            }

            {
                var expr = baseExpression;
                var e1 = expr.Arg1;
                var e2 = expr.Arg2;

                IVariable v1 = null, v2 = null;
                if (e1 != null && e2 != null)
                {
                    v1 = e1.Variable;
                    v2 = e2.Variable;
                }
                Instruction i1 = 0, i2 = 0;
                if (e1 != null && e2 != null)
                {
                    i1 = e1.ExpressionType;
                    i2 = e2.ExpressionType;
                }

                if (expr.ExpressionType == Instruction.ITOF)
                {
                    if (expr.Arg1 != null && expr.Arg1.ExpressionType == Instruction.PUSH)
                    {
                        float floatValue = (float)expr.Arg1.Value;
                        expr.ExpressionType = Instruction.F_PUSH;
                        expr.Value = floatValue.FloatToInt();
                        expr.Args.Clear();
                    }
                }
                if (expr.ExpressionType == Instruction.INV)
                {
                    if (expr.Arg1 != null && expr.Arg1.ExpressionType == Instruction.PUSH)
                    {
                        expr.ExpressionType = Instruction.PUSH;
                        expr.Value = -(expr.Arg1.Value);
                        expr.Args.Clear();
                    }
                }
                if (expr.ExpressionType == Instruction.F_INV)
                {
                    if (expr.Arg1 != null && expr.Arg1.ExpressionType == Instruction.F_PUSH)
                    {
                        expr.ExpressionType = Instruction.F_PUSH;
                        expr.FloatValue = -expr.Arg1.FloatValue;
                        expr.Args.Clear();
                    }
                }
                //if (expr.ExpressionType == Instruction.ITOB)
                //{
                //    if (expr.Arg1.ExpressionType == Instruction.PUSH && (expr.Arg1.Value == 0 || expr.Arg1.Value == 1))
                //    {
                //        var parameter = expr.GetFunctionCallParameter2();
                //        if (parameter != null && parameter.DataType == DataType.Bool)
                //        {
                //            expr.ExpressionType = Instruction.PUSH;
                //            expr.Value = expr.Arg1.Value;
                //            expr.Args.Clear();
                //        }
                //    }
                //}

                if (expr.ExpressionType == Instruction.ASSIGN || expr.ExpressionType == Instruction.LI_ASSIGN)
                {
                    //var e1 = expr.Arg1;
                    //var e2 = expr.Arg2;
                    if (e1 != null && e2 != null)
                    {
                        if (i1 == Instruction.AssignmentRef || i1 == Instruction.SH_LOCALREF)
                        {
                            var e2_ = e2;
                            if (e2_.ExpressionType == Instruction.ITOB || e2_.ExpressionType == Instruction.ITOLI)
                            {
                                e2_ = e2_.Arg1;
                            }
                            var i2_ = e2_.ExpressionType;
                            if (i1 == Instruction.AssignmentRef && i2_ == Instruction.PUSH)
                            {
                                if (e1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH)
                                {
                                    if (IsVoidContext(expr))  //prevent assignment chains from triggering this
                                    {
                                        expr.ExpressionType = Instruction.SH_LOCALASSIGN;
                                        expr.Value = e1.Arg2.Value;
                                        expr.Value2 = e2_.Value;
                                        expr.Args.Clear();
                                    }
                                    else
                                    {

                                    }
                                }
                            }
                            else if (i1 == Instruction.SH_LOCALREF && i2_ == Instruction.PUSH)
                            {
                                if (IsVoidContext(expr))  //prevent assignment chains from triggering this
                                {
                                    expr.ExpressionType = Instruction.SH_LOCALASSIGN;
                                    expr.Value = e1.Value;
                                    expr.Value2 = e2_.Value2;
                                    expr.Args.Clear();
                                }
                                else
                                {

                                }
                            }
                        }
                    }
                }

                if (expr.ExpressionType == Instruction.SR_ASSIGN)
                {
                    if (e2.ExpressionType == Instruction.REF)
                    {
                        if (v2 != null && v2.DataType == DataType.Struct)
                        {
                            e2.ExpressionType = Instruction.SR_REF;
                            e2.Value = v2.StructType;
                            return;
                        }
                    }
                }

                if (expr.ExpressionType == Instruction.S_REF)
                {
                    var arg1 = e1 ?? Expression.Empty;
                    var functionParameter = expr.GetFunctionCallParameter2();
                    var functionCallExpression = arg1.GetFunctionCallAncestor();
                    while (arg1.ExpressionType == Instruction.REF)
                    {
                        arg1 = arg1.Arg1 ?? Expression.Empty;
                    }
                    if (arg1.ExpressionType == Instruction.SH_LOCALREF ||
                        arg1.ExpressionType == Instruction.SH_STRUCTREF ||
                        arg1.ExpressionType == Instruction.SH_GLOBALREF)
                    {
                        if (functionParameter != null && functionParameter.DataType == DataType.RefString)
                        {
                            expr.ExpressionType = Instruction.REF;
                        }
                        if (v1 != null && v1.DataType.IsArray() && v1.ArrayDimensions > 1)
                        {
                            expr.ExpressionType = Instruction.REF;
                        }
                    }
                }
                if (expr.ExpressionType == Instruction.SR_REF)
                {
                    //var parent = expr.Parent ?? Expression.Empty;
                    //if (parent.ExpressionType == Instruction.SR_ASSIGN)
                    //{

                    //}
                    //else
                    {
                        var arg1 = e1 ?? Expression.Empty;
                        var functionParameter = expr.GetFunctionCallParameter();
                        var functionCallExpression = arg1.GetFunctionCallAncestor();
                        while (arg1.ExpressionType == Instruction.REF)
                        {
                            arg1 = arg1.Arg1 ?? Expression.Empty;
                        }
                        if (arg1.ExpressionType == Instruction.SH_LOCALREF ||
                            arg1.ExpressionType == Instruction.SH_STRUCTREF ||
                            arg1.ExpressionType == Instruction.SH_GLOBALREF)
                        {
                            var parent = expr.SkipParentCastOperations();
                            if (functionParameter != null && (functionParameter.StructType == expr.Value || (functionParameter.DataType.IsStruct() && functionParameter.StructType == -1)) && functionParameter.DataType != DataType.RefStruct && functionCallExpression != null /*&& functionCallExpression.ExpressionType == Instruction.CALLMETHOD*/)
                            {

                            }
                            else if (parent.ExpressionType == Instruction.RETURN && (v1.DataType == DataType.ArrayStruct || v1.DataType == DataType.RefArrayStruct) && currentFunction.DataType == DataType.Struct)
                            {
                                //???
                            }
                            else if (parent.ExpressionType == Instruction.A_INSERT || parent.ExpressionType == Instruction.A_PUSHBACK)
                            {

                            }
                            else
                            {
                                expr.ExpressionType = Instruction.REF;
                            }
                            return;
                        }
                    }

                }

                if (expr.ExpressionType == Instruction.S_PLUSA)
                {
                    var arg2 = expr.Arg2;
                    var var2 = arg2.Variable;
                    if (var2 != null) var2 = var2.Canonicalize();
                    bool shortenFirstArg = true;

                    //if (var2 == null || var2 is IFunction) shortenFirstArg = true;

                    if (shortenFirstArg)
                    {
                        if (expr.Arg1.ExpressionType == Instruction.AssignmentRef &&
                            expr.Arg1.Arg2.ExpressionType == Instruction.PUSH &&
                            (expr.Arg1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE ||
                            expr.Arg1.Arg1.ExpressionType == Instruction.PUSHGLOBALPAGE ||
                            expr.Arg1.Arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE
                            ))
                        {
                            expr.Arg1.ExpressionType = Instruction.REF;
                            Peephole2(expr.Arg1);
                        }
                        else if (expr.Arg1.ExpressionType == Instruction.AssignmentRef)
                        {
                            expr.Arg1.ExpressionType = Instruction.REF;
                        }

                        expr.ExpressionType = Instruction.S_PLUSA2;
                    }
                }


                if (expr.ExpressionType == Instruction.REF ||
                    expr.ExpressionType == Instruction.S_REF ||
                    expr.ExpressionType == Instruction.SR_REF ||
                    expr.ExpressionType == Instruction.SR_REFREF)
                {
                    //check for specific exceptions to the peephole rules
                    var variable = expr.Variable;
                    if (variable != null)
                    {
                        var dataType = variable.DataType;
                        if (dataType.IsString())
                        {
                            var parent = expr.SkipParentCastOperations() ?? Expression.Empty;
                            bool parentIsQuestionColon = false;
                            if (parent.ExpressionType == Instruction.IfElse && !parent.IsVoidContext())
                            {
                                parentIsQuestionColon = true;
                            }

                            if (parent.ExpressionType == Instruction.RETURN)
                            {
                                if (currentFunction.DataType == DataType.String)
                                {
                                    return;
                                }
                                else
                                {

                                }
                            }
                            if (parent.ExpressionType == Instruction.SWITCH) //for STRSWITCH instruction
                            {
                                return;
                            }

                            var functionParameter = expr.GetFunctionCallParameter2();

                            //if (functionParameter != null && functionParameter.DataType == DataType.RefString)
                            //{
                            //    return;
                            //}
                            if (functionParameter != null)
                            {
                                var calledFunction = functionParameter.Parent as IFunction;
                                var functionCallExpression = expr.GetFunctionCallAncestor();
                            checkAgain:
                                bool isVoidContext = false;
                                if (functionCallExpression != null)
                                {
                                    isVoidContext = IsVoidContext(functionCallExpression);
                                }

                                if (calledFunction != null && functionCallExpression != null)
                                {
                                    if (isVoidContext && functionParameter.DataType == DataType.String)
                                    {
                                        return;
                                    }
                                    bool hasBinaryOperator = !isVoidContext && functionCallExpression.GetOtherSideOfBinaryExpression() != null;
                                    bool hasUnaryOperator = !isVoidContext && Expression.IsUnary(functionCallExpression.SkipParentCastOperations().ExpressionType);

                                    if ((hasBinaryOperator || hasUnaryOperator) && functionParameter.DataType == DataType.String)
                                    {
                                        //???
                                        return;
                                    }
                                }
                                if (functionCallExpression.ExpressionType == Instruction.CALLSYS)
                                {
                                    return;
                                }
                                var parentOfFunctionCall = functionCallExpression.SkipParentCastOperations();
                                if (parentOfFunctionCall != null && parentOfFunctionCall.ExpressionType == Instruction.RETURN && functionParameter.DataType == DataType.String)
                                {
                                    //???
                                    return;
                                }
                                if (parentOfFunctionCall != null && parentOfFunctionCall.ExpressionType == Instruction.ForLoop && functionParameter.DataType == DataType.String && !isVoidContext)
                                {
                                    //Condition of a for loop is an exception for some strange reason
                                    var ee = expr;
                                    while (ee != null && ee.Parent != parentOfFunctionCall)
                                    {
                                        ee = ee.Parent;
                                    }

                                    if (ee == parentOfFunctionCall.Arg2)
                                    {
                                        return;
                                    }
                                }

                                //is this a nested function call?
                                if (functionCallExpression != null)
                                {
                                    var functionCallExpression2 = functionCallExpression.GetFunctionCallAncestor();
                                    if (functionCallExpression2 != null)
                                    {
                                        var functionParameter2 = functionCallExpression.GetFunctionCallParameter();
                                        if (functionParameter2 != null)
                                        {
                                            return;
                                            calledFunction = functionCallExpression2.Variable as IFunction;
                                            functionCallExpression = functionCallExpression2;
                                            functionParameter = functionParameter2;


                                            goto checkAgain;
                                        }
                                    }


                                }

                                if (parentOfFunctionCall != null)
                                {
                                    if (parentOfFunctionCall.ExpressionType == Instruction.A_PUSHBACK ||
                                        parentOfFunctionCall.ExpressionType == Instruction.A_INSERT)
                                    {
                                        return;
                                    }
                                }
                            }

                            //if (functionParameter != null && functionParameterParent != null && functionParameterParent.DataType == DataType.Void /*(variable.Parent == null || variable.Index < variable.Parent.ParameterCount)*/)
                            //{
                            //    return;
                            //}

                            if (functionParameter != null && (variable.Parent == null || variable.Index < variable.Parent.ParameterCount) && functionParameter.DataType != DataType.RefString/* && functionParameterParent != null && functionParameterParent.DataType == DataType.Void*/)
                            {
                                return;
                            }

                            //if (functionParameter != null && /*functionParameter.Index == 0 && */ functionParameter.Parent is IFunction && functionParameter.Parent.DataType == DataType.Void)
                            //{
                            //    //???
                            //    return;
                            //}

                            int exprIndex = expr.GetIndex();
                            if (parent.ExpressionType.IsFunctionCall())
                            {
                                if (parent.Parent != null && parent.Parent.ExpressionType != Instruction.Statement)
                                {
                                    exprIndex = parent.GetIndex();
                                    if (exprIndex > 0)
                                    {
                                        //???  removed - breaks Daibanchou
                                        //return;
                                    }
                                    //???
                                    parent = parent.Parent;
                                }
                            }

                            if ((parent.ExpressionType == Instruction.S_ASSIGN || parent.ExpressionType == Instruction.S_PLUSA) && exprIndex != 0)
                            {
                                return;
                            }
                            if (parent.ExpressionType == Instruction.S_PLUSA2 && exprIndex == 1)
                            {
                                return;
                            }
                            if (parent.ExpressionType == Instruction.S_MOD)
                            {
                                if (exprIndex == 1)
                                {
                                    return;
                                }
                                else if (exprIndex == 0)
                                {
                                    var parent2b = parent.SkipParentCastOperations();
                                    if (parent2b != null && parent2b.ExpressionType == Instruction.RETURN)
                                    {
                                        return;
                                    }
                                }
                            }

                            if (parent.ExpressionType.IsBinary())
                            {
                                return;
                            }

                            if (parentIsQuestionColon)
                            {
                                return;
                            }

                            if (parent.ExpressionType == Instruction.S_EMPTY ||
                                parent.ExpressionType == Instruction.S_ERASE ||
                                parent.ExpressionType == Instruction.S_FIND ||
                                parent.ExpressionType == Instruction.S_GETPART ||
                                parent.ExpressionType == Instruction.S_LENGTH ||
                                parent.ExpressionType == Instruction.S_LENGTHBYTE ||
                                parent.ExpressionType == Instruction.S_POPBACK ||
                                parent.ExpressionType == Instruction.S_PUSHBACK ||
                                parent.ExpressionType == Instruction.STOI ||
                                parent.ExpressionType == Instruction.FT_ASSIGNS)
                            {
                                return;
                            }

                            if (parent.ExpressionType == Instruction.A_PUSHBACK ||
                                parent.ExpressionType == Instruction.A_INSERT)
                            {
                                return;
                            }

                            if (parent.ExpressionType == Instruction.DG_STR_TO_METHOD)
                            {
                                return;
                            }

                            var parent2 = expr.SkipParentCastOperations();
                            while (parent2 != null && parent2.ExpressionType == Instruction.Comma)
                            {
                                parent2 = parent2.Parent;
                            }

                            if (parent2 != null && (parent2.ExpressionType == Instruction.A_FIND || parent2.ExpressionType == Instruction.A_FILL))
                            {
                                return;
                            }

                            //if (parent.ExpressionType == Instruction.A_PUSHBACK && exprIndex == 1)
                            //{
                            //    return;
                            //}
                        }
                        if (dataType.IsStruct())
                        {

                            var parent = expr.SkipParentCastOperations() ?? Expression.Empty;
                            if (parent.ExpressionType == Instruction.RETURN)
                            {
                                if (currentFunction.DataType == DataType.Struct)
                                {
                                    return;
                                }
                                else
                                {

                                }
                            }
                            if (parent.ExpressionType == Instruction.SR_ASSIGN && expr.GetIndex() != 0)
                            {
                                return;
                            }
                            var functionParameter = expr.GetFunctionCallParameter2();
                            Expression functionCallExpression = null;
                            IFunction parentFunction = null;
                            bool isVoidContext = false;
                            if (functionParameter != null)
                            {
                                parentFunction = functionParameter.Parent;
                                functionCallExpression = expr.GetFunctionCallAncestor();
                                isVoidContext = IsVoidContext(functionCallExpression);

                                if (functionParameter.DataType == DataType.Struct && dataType == DataType.Struct)
                                {
                                    if (functionCallExpression != null && functionCallExpression.ExpressionType == Instruction.CALLHLL)
                                    {
                                        //???
                                        return;
                                    }
                                }

                                if (dataType == DataType.Struct && functionParameter.DataType != DataType.RefStruct && (variable.Parent == null || variable.Index < variable.Parent.ParameterCount))
                                {
                                    return;
                                }
                                if (dataType == DataType.RefStruct && functionParameter.DataType == DataType.Struct && (variable.Parent == null || variable.Index < variable.Parent.ParameterCount))
                                {
                                    return;
                                }

                                if (dataType == DataType.Struct && functionParameter.DataType == DataType.Struct && (functionParameter.Parent != null && functionParameter.Parent.DataType == DataType.Void))
                                {
                                    return;
                                }

                                if (!isVoidContext && dataType == DataType.Struct && functionParameter.DataType == DataType.Struct)
                                {
                                    return;
                                }
                                if (isVoidContext && dataType == DataType.Struct && functionParameter.DataType == DataType.Struct && expr.ExpressionType == Instruction.SR_REF)
                                {
                                    //???
                                    return;
                                }
                            }

                            if (parent.ExpressionType == Instruction.Comma && parent.Parent.ExpressionType == Instruction.A_INSERT)
                            {
                                return;
                            }

                            if (parent.ExpressionType == Instruction.A_PUSHBACK || parent.ExpressionType == Instruction.A_INSERT)
                            {
                                if (expr.ExpressionType == Instruction.SR_REF)
                                {
                                    if (expr.GetIndex() == 1)
                                    {
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    ////specific rules:
                    //var correspondingLValue = expr.GetCorrespondingLValue();
                    //if (correspondingLValue != null)
                    //{
                    //    if (!(correspondingLValue.Parent is Struct))
                    //    {
                    //        return;
                    //    }

                    //}
                    //var otherSide = expr.GetOtherSideOfBinaryExpression();
                    //if (otherSide != null)
                    //{
                    //    if (expr.Parent.ExpressionType == Instruction.S_MOD && expr.GetIndex() == 2)
                    //    {
                    //        return;
                    //    }
                    //}
                    if (!MakeShRef(expr) && expr.ExpressionType != Instruction.REF)
                    {
                        expr.ExpressionType = Instruction.REF;
                    }
                }
                else if (expr.ExpressionType == Instruction.INC || expr.ExpressionType == Instruction.DEC || expr.ExpressionType == Instruction.PreIncrement || expr.ExpressionType == Instruction.PreDecrement)
                {
                    if (e1.ExpressionType == Instruction.AssignmentRef)
                    {
                        e2 = e1.Arg2;
                        e1 = e1.Arg1;
                        if (e1.ExpressionType == Instruction.PUSHLOCALPAGE && e2.ExpressionType == Instruction.PUSH)
                        {
                            if (IsVoidContext(expr))
                            {
                                var v = expr.Arg1.Variable.Canonicalize();
                                if (v != null && v.DataType == DataType.Int)
                                {
                                    if (expr.ExpressionType == Instruction.PreIncrement || expr.ExpressionType == Instruction.INC)
                                    {
                                        expr.ExpressionType = Instruction.SH_LOCALINC;
                                    }
                                    else
                                    {
                                        expr.ExpressionType = Instruction.SH_LOCALDEC;
                                    }
                                    expr.Value = e2.Value;
                                    expr.Args.Clear();
                                }
                                else if (v != null && v.DataType == DataType.Lint)
                                {

                                }
                            }
                        }
                    }
                }
                else if (expr.ExpressionType == Instruction.PUSHSTRUCTPAGE && expr.Parent.ExpressionType == Instruction.CALLHLL)
                {
                    //this condition is not used anymore
                    var parameter = expr.GetFunctionCallParameter2();
                    if (parameter != null && parameter.DataType == DataType.Struct)
                    {
                        var variable = expr.Variable;
                        expr.Arg1 = new Expression(Instruction.PUSHSTRUCTPAGE, -1);
                        expr.ExpressionType = Instruction.SR_REF2;
                        expr.Value = variable.StructType;
                    }
                }
                //else if (expr.ExpressionType.IsFunctionCall() && IsVoidContext(expr))
                //{
                //    //if (expr.Variable != 
                //}
                if (expr.ExpressionType == Instruction.A_FREE)
                {
                    var nextStatement = expr.GetNextStatement(false, false);
                    if (nextStatement != null)
                    {
                        //if (nextStatement.ExpressionType == Instruction.A_ALLOC && AinVersion >= 6)
                        //{
                        //    expr.ExpressionType = Instruction.Nop;
                        //    expr.Args.Clear();
                        //}
                        if (nextStatement.ExpressionType == Instruction.A_FREE)
                        {
                            HashSet<IVariable> seenFrees = new HashSet<IVariable>();
                            seenFrees.Add(e1.Variable);
                            while (nextStatement != null && nextStatement.ExpressionType == Instruction.A_FREE)
                            {
                                var v = nextStatement.Arg1.Variable;
                                if (v != null)
                                {
                                    if (seenFrees.Contains(v))
                                    {
                                        nextStatement.ExpressionType = Instruction.Nop;
                                        nextStatement.Args.Clear();
                                    }
                                    else
                                    {
                                        seenFrees.Add(v);
                                    }
                                }
                                nextStatement = nextStatement.GetNextStatement(false, false);
                            }
                        }
                    }
                }
            }

        }

        private void PeepholeSimplified(Expression baseExpression)
        {
        restart:
            if (baseExpression == null) return;
            if (baseExpression.ExpressionType == Instruction.Statement &&
                baseExpression.Args.Count == 2 &&
                (baseExpression.Arg2 ?? Expression.Empty).ExpressionType == Instruction.Statement &&
                (baseExpression.Arg1 ?? Expression.Empty).ExpressionType != Instruction.Statement)
            {
                PeepholeSimplified(baseExpression.Arg1);
                baseExpression = baseExpression.Arg2;
                goto restart;
            }
            else
            {
                for (int i = 0; i < baseExpression.Args.Count; i++)
                {
                    var expr = baseExpression.Args[i];
                    PeepholeSimplified(expr);
                    if (ainFile.Version >= 6) { PeepHoleV6(expr); }
                }
            }

            if (baseExpression.ExpressionType == Instruction.Statement)
            {
                return;
            }

            {
                var expr = baseExpression;
                var e1 = expr.Arg1;
                var e2 = expr.Arg2;

                IVariable v1 = null, v2 = null;
                if (e1 != null && e2 != null)
                {
                    v1 = e1.Variable;
                    v2 = e2.Variable;
                }
                Instruction i1 = 0, i2 = 0;
                if (e1 != null && e2 != null)
                {
                    i1 = e1.ExpressionType;
                    i2 = e2.ExpressionType;
                }

                if (expr.ExpressionType == Instruction.ITOF)
                {
                    if (expr.Arg1 != null && expr.Arg1.ExpressionType == Instruction.PUSH)
                    {
                        float floatValue = (float)expr.Arg1.Value;
                        expr.ExpressionType = Instruction.F_PUSH;
                        expr.Value = floatValue.FloatToInt();
                        expr.Args.Clear();
                    }
                }
                if (expr.ExpressionType == Instruction.INV)
                {
                    if (expr.Arg1 != null && expr.Arg1.ExpressionType == Instruction.PUSH)
                    {
                        expr.ExpressionType = Instruction.PUSH;
                        expr.Value = -(expr.Arg1.Value);
                        expr.Args.Clear();
                    }
                }
                if (expr.ExpressionType == Instruction.F_INV)
                {
                    if (expr.Arg1 != null && expr.Arg1.ExpressionType == Instruction.F_PUSH)
                    {
                        expr.ExpressionType = Instruction.F_PUSH;
                        expr.FloatValue = -expr.Arg1.FloatValue;
                        expr.Args.Clear();
                    }
                }
                //if (expr.ExpressionType == Instruction.ITOB)
                //{
                //    if (expr.Arg1.ExpressionType == Instruction.PUSH && (expr.Arg1.Value == 0 || expr.Arg1.Value == 1))
                //    {
                //        var parameter = expr.GetFunctionCallParameter2();
                //        if (parameter != null && parameter.DataType == DataType.Bool)
                //        {
                //            expr.ExpressionType = Instruction.PUSH;
                //            expr.Value = expr.Arg1.Value;
                //            expr.Args.Clear();
                //        }
                //    }
                //}

                if (expr.ExpressionType == Instruction.SR_ASSIGN)
                {
                    if (e2.ExpressionType == Instruction.REF)
                    {
                        if (v2 != null && v2.DataType == DataType.Struct)
                        {
                            e2.ExpressionType = Instruction.SR_REF;
                            e2.Value = v2.StructType;
                            return;
                        }
                    }
                }

                if (expr.ExpressionType == Instruction.SR_REF)
                {
                    {
                        Instruction parentInstruction = Instruction.Nop;
                        var parent = expr.Parent;
                        if (parent != null)
                        {
                            parent = expr.SkipParentCastOperations();
                        }
                        while (parent != null && parent.ExpressionType == Instruction.Comma)
                        {
                            parent = parent.Parent;
                        }
                        if (parent != null)
                        {
                            parentInstruction = parent.ExpressionType;
                        }
                        //var arg1 = e1 ?? Expression.Empty;
                        var functionParameter = expr.GetFunctionCallParameter();
                        var functionCallExpression = expr.GetFunctionCallAncestor();
                        //while (arg1.ExpressionType == Instruction.REF)
                        //{
                        //    arg1 = arg1.Arg1 ?? Expression.Empty;
                        //}
                        //if (arg1.ExpressionType == Instruction.PUSHLOCALPAGE ||
                        //    arg1.ExpressionType == Instruction.PUSHGLOBALPAGE ||
                        //    arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                        {
                            if (functionParameter != null && functionParameter.DataType == DataType.RefStruct && functionCallExpression != null)
                            {
                                expr.ExpressionType = Instruction.REF;
                                return;
                            }
                            else if (functionParameter != null && functionParameter.DataType == DataType.Struct && functionCallExpression != null)
                            {

                            }
                            else if (parentInstruction == Instruction.RETURN && (v1.DataType == DataType.ArrayStruct || v1.DataType == DataType.RefArrayStruct) && currentFunction.DataType == DataType.Struct)
                            {
                                //???
                            }
                            else if (parentInstruction == Instruction.A_INSERT || parentInstruction == Instruction.A_PUSHBACK)
                            {

                            }
                            else if (parentInstruction == Instruction.SR_ASSIGN && parent.Arg2 == expr)
                            {

                            }
                            else if (parentInstruction == Instruction.RETURN && currentFunction.DataType != DataType.RefStruct)
                            {

                            }
                            else
                            {
                                expr.ExpressionType = Instruction.REF;
                                return;
                            }
                        }
                    }
                }

                if (expr.ExpressionType == Instruction.S_REF)
                {
                    Instruction parentInstruction = Instruction.Nop;
                    var parent = expr.Parent;
                    if (parent != null)
                    {
                        parent = expr.SkipParentCastOperations();
                    }
                    while (parent != null && parent.ExpressionType == Instruction.Comma)
                    {
                        parent = parent.Parent;
                    }
                    if (parent != null)
                    {
                        parentInstruction = parent.ExpressionType;
                    }

                    //var arg1 = e1 ?? Expression.Empty;
                    var functionParameter = expr.GetFunctionCallParameter2();
                    var functionCallExpression = expr.GetFunctionCallAncestor();
                    //while (arg1.ExpressionType == Instruction.REF)
                    //{
                    //    arg1 = arg1.Arg1 ?? Expression.Empty;
                    //}
                    //if (arg1.ExpressionType == Instruction.PUSHLOCALPAGE ||
                    //    arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE ||
                    //    arg1.ExpressionType == Instruction.PUSHGLOBALPAGE)
                    {
                        if (functionParameter != null && functionParameter.DataType == DataType.RefString)
                        {
                            expr.ExpressionType = Instruction.REF;
                        }
                        if (v1 != null && v1.DataType.IsArray() && v1.ArrayDimensions > 1)
                        {
                            expr.ExpressionType = Instruction.REF;
                        }
                        if (parentInstruction == Instruction.S_POPBACK2 || parentInstruction == Instruction.S_ERASE2 || parentInstruction == Instruction.C_REF)
                        {
                            expr.ExpressionType = Instruction.REF;
                        }
                        if (parentInstruction == Instruction.RETURN && currentFunction.DataType == DataType.RefString)
                        {
                            expr.ExpressionType = Instruction.REF;
                        }
                    }
                }

                if (expr.ExpressionType == Instruction.PUSHSTRUCTPAGE && expr.Parent.ExpressionType == Instruction.CALLHLL)
                {
                    //this condition is not used anymore
                    var parameter = expr.GetFunctionCallParameter2();
                    if (parameter != null && parameter.DataType == DataType.Struct)
                    {
                        var variable = expr.Variable;
                        expr.Arg1 = new Expression(Instruction.PUSHSTRUCTPAGE, -1);
                        expr.ExpressionType = Instruction.SR_REF2;
                        expr.Value = variable.StructType;
                    }
                }
                if (expr.ExpressionType == Instruction.A_FREE)
                {
                    var nextStatement = expr.GetNextStatement(false, false);
                    if (nextStatement != null)
                    {
                        //if (nextStatement.ExpressionType == Instruction.A_ALLOC && AinVersion >= 6)
                        //{
                        //    expr.ExpressionType = Instruction.Nop;
                        //    expr.Args.Clear();
                        //}
                        if (nextStatement.ExpressionType == Instruction.A_FREE)
                        {
                            HashSet<IVariable> seenFrees = new HashSet<IVariable>();
                            seenFrees.Add(e1.Variable);
                            while (nextStatement != null && nextStatement.ExpressionType == Instruction.A_FREE)
                            {
                                var v = nextStatement.Arg1.Variable;
                                if (v != null)
                                {
                                    if (seenFrees.Contains(v))
                                    {
                                        nextStatement.ExpressionType = Instruction.Nop;
                                        nextStatement.Args.Clear();
                                    }
                                    else
                                    {
                                        seenFrees.Add(v);
                                    }
                                }
                                nextStatement = nextStatement.GetNextStatement(false, false);
                            }
                        }
                    }
                }
                if (expr.ExpressionType == Instruction.S_PLUSA)
                {
                    if (expr.Arg1.ExpressionType == Instruction.AssignmentRef)
                    {
                        expr.Arg1.ExpressionType = Instruction.REF;
                    }
                    expr.ExpressionType = Instruction.S_PLUSA2;
                }
            }
        }

        private bool MakeShRef(Expression expr)
        {
            if (this.UseSimplifiedCode)
            {
                return true;
            }

            var e1 = expr.Arg1;
            var e2 = expr.Arg2;
            Instruction i1 = Instruction.Nop, i2 = Instruction.Nop;
            if (e1 != null) i1 = e1.ExpressionType;
            if (e2 != null) i2 = e2.ExpressionType;

            if (e1 != null && e2 != null)
            {
                if (i1 == Instruction.PUSHLOCALPAGE && i2 == Instruction.PUSH)
                {
                    expr.ExpressionType = Instruction.SH_LOCALREF;
                    expr.Value = e2.Value;
                    expr.Args.Clear();
                    return true;
                }
                else if (i1 == Instruction.PUSHGLOBALPAGE && i2 == Instruction.PUSH)
                {
                    expr.ExpressionType = Instruction.SH_GLOBALREF;
                    expr.Value = e2.Value;
                    expr.Args.Clear();
                    return true;
                }
                else if (i1 == Instruction.PUSHSTRUCTPAGE && i2 == Instruction.PUSH)
                {
                    expr.ExpressionType = Instruction.SH_STRUCTREF;
                    expr.Value = e2.Value;
                    expr.Args.Clear();
                    return true;
                }
            }
            else if (e1 != null && e1.ExpressionType == Instruction.SR_REFREF)
            {
                e2 = e1.Arg2;
                e1 = e1.Arg1;

                if (i1 == Instruction.PUSHLOCALPAGE && i2 == Instruction.PUSH)
                {
                    expr.ExpressionType = Instruction.SH_LOCALREF;
                    expr.Value = e2.Value;
                    expr.Args.Clear();
                    return true;
                }
            }
            return false;
        }

        public static void PeepholeV6Function(Expression functionExpr)
        {
            var compiler = new Compiler(functionExpr.ainFile, 0);
            compiler.currentFunction = compiler.ainFile.Functions[functionExpr.Value];
            compiler._PeepholeV6Recursive(functionExpr);
        }

        private void _PeepholeV6Recursive(Expression baseExpression)
        {
        restart:
            if (baseExpression == null) return;
            if (baseExpression.ExpressionType == Instruction.Statement &&
                baseExpression.Args.Count == 2 &&
                (baseExpression.Arg2 ?? Expression.Empty).ExpressionType == Instruction.Statement &&
                (baseExpression.Arg1 ?? Expression.Empty).ExpressionType != Instruction.Statement)
            {
                _PeepholeV6Recursive(baseExpression.Arg1);
                if (ainFile.Version >= 6) { PeepHoleV6(baseExpression.Arg1); }
                baseExpression = baseExpression.Arg2;
                goto restart;
            }
            else
            {
                for (int i = 0; i < baseExpression.Args.Count; i++)
                {
                    var expr = baseExpression.Args[i];
                    _PeepholeV6Recursive(expr);
                    if (ainFile.Version >= 6) { PeepHoleV6(expr); }
                }
            }

            if (baseExpression.ExpressionType == Instruction.Statement)
            {
                return;
            }

            //if (ainFile.Version >= 6) { PeepHoleV6(baseExpression); }
        }

        private bool PeepHoleV6(Expression expr)
        {
            if (expr == null) return false;
            var e1 = expr.Arg1;
            var e2 = expr.Arg2;
            Instruction i1 = Instruction.Nop, i2 = Instruction.Nop;
            if (e1 != null) i1 = e1.ExpressionType;
            if (e2 != null) i2 = e2.ExpressionType;

            if (ainFile.Version >= 6 && !UseSimplifiedCode)
            {
                if (!ainFile.UsesMsg1)
                {
                    if (expr.ExpressionType == Instruction.CALLMETHOD && i1 == Instruction.SH_STRUCTREF && expr.Args.Count == 1)
                    {
                        expr.ExpressionType = Instruction.SH_STRUCTREF_CALLMETHOD_NO_PARAM;
                        expr.Value2 = expr.Value;
                        expr.Value = expr.Arg1.Value;
                        expr.Args.RemoveAt(0);
                        return true;
                    }
                    if (expr.ExpressionType == Instruction.CALLMETHOD && i1 == Instruction.SH_STRUCTREF2 && expr.Args.Count == 1)
                    {
                        expr.ExpressionType = Instruction.SH_STRUCTREF2_CALLMETHOD_NO_PARAM;
                        expr.Value3 = expr.Value;
                        expr.Value2 = expr.Arg1.Value2;
                        expr.Value = expr.Arg1.Value;
                        expr.Args.RemoveAt(0);
                        return true;
                    }
                    if (expr.ExpressionType == Instruction.CALLMETHOD && i1 == Instruction.PUSHSTRUCTPAGE && expr.Args.Count == 1)
                    {
                        expr.ExpressionType = Instruction.THISCALLMETHOD_NOPARAM;
                        expr.Args.RemoveAt(0);
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.S_REF && (i1 == Instruction.PUSHSTRUCTPAGE && i2 == Instruction.PUSH))
                {
                    expr.ExpressionType = Instruction.SH_STRUCT_S_REF;
                    expr.Value = expr.Arg2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.S_REF && (i1 == Instruction.PUSHLOCALPAGE && i2 == Instruction.PUSH))
                {
                    expr.ExpressionType = Instruction.SH_LOCAL_S_REF;
                    expr.Value = expr.Arg2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.S_REF && (i1 == Instruction.PUSHGLOBALPAGE && i2 == Instruction.PUSH))
                {
                    expr.ExpressionType = Instruction.SH_GLOBAL_S_REF;
                    expr.Value = expr.Arg2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.REFREF && (i1 == Instruction.PUSHLOCALPAGE && i2 == Instruction.PUSH))
                {
                    expr.ExpressionType = Instruction.SH_LOCALREFREF;
                    expr.Value = expr.Arg2.Value;
                    expr.Args.Clear();
                    return true;
                }

                if (expr.ExpressionType == Instruction.S_ASSIGN && i1 == Instruction.SH_STRUCTREF && (i2 == Instruction.S_REF || i2 == Instruction.SH_LOCAL_S_REF))
                {
                    var e3 = e2.Arg1;
                    var e4 = e2.Arg2;
                    Instruction i3 = Instruction.Nop, i4 = Instruction.Nop;
                    if (e3 != null) { i3 = e3.ExpressionType; }
                    if (e4 != null) { i4 = e4.ExpressionType; }
                    if (e4 == null) { e4 = e2; }
                    if (i2 == Instruction.SH_LOCAL_S_REF || (i3 == Instruction.PUSHLOCALPAGE && i4 == Instruction.PUSH && expr.IsVoidContext()))
                    {
                        expr.ExpressionType = Instruction.SH_STRUCTREF_SASSIGN_LOCALSREF;
                        expr.Value = e1.Value;
                        expr.Value2 = e4.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.ASSIGN && i1 == Instruction.AssignmentRef && i2 == Instruction.SH_LOCALREF)
                {
                    var e3 = e1.Arg1;
                    var e4 = e1.Arg2;
                    Instruction i3 = Instruction.Nop, i4 = Instruction.Nop;
                    if (e3 != null) { i3 = e3.ExpressionType; }
                    if (e4 != null) { i4 = e4.ExpressionType; }
                    if (i3 == Instruction.PUSHSTRUCTPAGE && i4 == Instruction.PUSH && e2.Variable.DataType.IsPrimitiveType() && e1.Variable.DataType.IsPrimitiveType()
                        && expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_MEM_ASSIGN_LOCAL;
                        expr.Value = e4.Value;
                        expr.Value2 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.MINUSA && i1 == Instruction.AssignmentRef && i2 == Instruction.PUSH)
                {
                    var e3 = e1.Arg1;
                    var e4 = e1.Arg2;
                    Instruction i3 = Instruction.Nop, i4 = Instruction.Nop;
                    if (e3 != null) { i3 = e3.ExpressionType; }
                    if (e4 != null) { i4 = e4.ExpressionType; }
                    if (i3 == Instruction.PUSHLOCALPAGE && i4 == Instruction.PUSH && e1.Variable.DataType.IsPrimitiveType()
                        && expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_LOCALASSIGN_SUB_IMM;
                        expr.Value = e4.Value;
                        expr.Value2 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.ASSIGN && i1 == Instruction.AssignmentRef && i2 == Instruction.SH_STRUCTREF)
                {
                    if (e1.ExpressionType == Instruction.AssignmentRef && e1.Arg1.ExpressionType == Instruction.SH_LOCALREFREF && e1.Arg1.Variable.DataType.IsPrimitiveRefType() && expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_LOCREF_ASSIGN_MEM;
                        expr.Value = e1.Arg1.Value;
                        expr.Value2 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.ASSIGN && i1 == Instruction.AssignmentRef && i2 == Instruction.PUSH)
                {
                    if (e1.Arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE && e1.Arg2.ExpressionType == Instruction.PUSH && e1.Variable.DataType.IsPrimitiveType() /* && e2.Value != -1 */
                        && expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_MEM_ASSIGN_IMM;
                        expr.Value = e1.Arg2.Value;
                        expr.Value2 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.ASSIGN && i1 == Instruction.AssignmentRef && i2 == Instruction.ITOB && e2.Arg1.ExpressionType == Instruction.PUSH)
                {
                    if (e1.Arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE && e1.Arg2.ExpressionType == Instruction.PUSH && e1.Variable.DataType.IsPrimitiveType()
                        && expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_MEM_ASSIGN_IMM;
                        expr.Value = e1.Arg2.Value;
                        expr.Value2 = e2.Arg1.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.ASSIGN && i1 == Instruction.AssignmentRef && i2 == Instruction.ITOB && e2.Arg1.ExpressionType == Instruction.PUSH)
                {
                    if (e1.Arg1.ExpressionType == Instruction.PUSHGLOBALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH && e1.Variable.DataType.IsPrimitiveType() /* && e2.Value != -1 */
                        && expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_GLOBAL_ASSIGN_IMM;
                        expr.Value = e1.Arg2.Value;
                        expr.Value2 = e2.Arg1.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.ASSIGN && i1 == Instruction.AssignmentRef && i2 == Instruction.PUSH)
                {
                    if (e1.Arg1.ExpressionType == Instruction.PUSHGLOBALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH && e1.Variable.DataType.IsPrimitiveType() /* && e2.Value != -1 */
                        && expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_GLOBAL_ASSIGN_IMM;
                        expr.Value = e1.Arg2.Value;
                        expr.Value2 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.ASSIGN && i1 == Instruction.AssignmentRef && i2 == Instruction.SH_STRUCTREF)
                {
                    if (e1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH && e1.Variable.DataType.IsPrimitiveType()
                        && expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_LOCAL_ASSIGN_STRUCTREF;
                        expr.Value = e1.Arg2.Value;
                        expr.Value2 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.S_ASSIGN && (i1 == Instruction.SH_LOCALREF || i1 == Instruction.SH_GLOBALREF || i1 == Instruction.SH_STRUCTREF || i1 == Instruction.REF) && i2 == Instruction.S_PUSH)
                {
                    if (expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_S_ASSIGN_STR0;
                        expr.Value = e2.Value;
                        expr.Args.RemoveAt(1);
                        return true;
                    }
                }

                if (expr.ExpressionType == Instruction.A_NUMOF && i1 == Instruction.AssignmentRef && i2 == Instruction.PUSH && e2.Value == 1 && e1.Arg1.ExpressionType == Instruction.PUSHGLOBALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH)
                {
                    expr.ExpressionType = Instruction.A_NUMOF_GLOB_1;
                    expr.Value = e1.Arg2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.A_NUMOF && i1 == Instruction.AssignmentRef && i2 == Instruction.PUSH && e2.Value == 1 && e1.Arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE && e1.Arg2.ExpressionType == Instruction.PUSH)
                {
                    expr.ExpressionType = Instruction.A_NUMOF_STRUCT_1;
                    expr.Value = e1.Arg2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.SR_ASSIGN && i2 == Instruction.SR_REF && expr.Arg3.ExpressionType == Instruction.PUSH && e2.Value == expr.Arg3.Value && e2.Value == e2.Variable.StructType)
                {
                    expr.ExpressionType = Instruction.SH_SR_ASSIGN;
                    expr.Arg2.ExpressionType = Instruction.AssignmentRef;
                    expr.Args.RemoveAt(2);
                    return true;
                }
                if (expr.ExpressionType == Instruction.SR_REF && i1 == Instruction.PUSHSTRUCTPAGE && i2 == Instruction.PUSH)
                {
                    expr.ExpressionType = Instruction.SH_STRUCT_SR_REF;
                    expr.Value2 = expr.Value;
                    expr.Value = e2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.S_EQUALE && i1 == Instruction.SH_STRUCT_S_REF && i2 == Instruction.SH_LOCAL_S_REF)
                {
                    expr.ExpressionType = Instruction.SH_STRUCTSREF_EQ_LOCALSREF;
                    expr.Value = e1.Value;
                    expr.Value2 = e2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.S_EMPTY && i1 == Instruction.SH_STRUCT_S_REF)
                {
                    expr.ExpressionType = Instruction.SH_STRUCTSREF_EMPTY;
                    expr.Value = e1.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.S_EMPTY && i1 == Instruction.SH_LOCAL_S_REF)
                {
                    expr.ExpressionType = Instruction.SH_LOCALSREF_EMPTY;
                    expr.Value = e1.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.S_EQUALE && i1 == Instruction.SH_LOCAL_S_REF && i2 == Instruction.S_PUSH)
                {
                    expr.ExpressionType = Instruction.SH_LOCALSREF_EQ_STR0;
                    expr.Value = e1.Value;
                    expr.Value2 = e2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.S_NOTE && i1 == Instruction.SH_LOCAL_S_REF && i2 == Instruction.S_PUSH)
                {
                    expr.ExpressionType = Instruction.SH_LOCALSREF_NE_STR0;
                    expr.Value = e1.Value;
                    expr.Value2 = e2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.S_NOTE && i1 == Instruction.SH_STRUCT_S_REF && i2 == Instruction.S_PUSH)
                {
                    expr.ExpressionType = Instruction.SH_STRUCTSREF_NE_STR0;
                    expr.Value = e1.Value;
                    expr.Value2 = e2.Value;
                    expr.Args.Clear();
                    return true;
                }
                if (expr.ExpressionType == Instruction.A_PUSHBACK && i1 == Instruction.AssignmentRef && i2 == Instruction.SH_LOCAL_S_REF && e1.Arg1.ExpressionType == Instruction.PUSHGLOBALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH && e1.Variable.DataType == DataType.ArrayString)
                {
                    expr.ExpressionType = Instruction.SH_GLOBAL_APUSHBACK_LOCALSREF;
                    expr.Value = e1.Arg2.Value;
                    expr.Value2 = e2.Value;
                    expr.Args.Clear();
                    return true;
                }

                if (i2 == Instruction.CALLSYS && e2.Value == 19)
                {
                    if (expr.ExpressionType == Instruction.S_ASSIGN && expr.IsVoidContext())
                    {
                        expr.ExpressionType = Instruction.SH_S_ASSIGN_CALLSYS19;
                        expr.Arg2 = expr.Arg2.Arg1 ?? expr.Arg2.Arg2;
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.ASSIGN && i1 == Instruction.AssignmentRef && i2 == Instruction.SH_LOCALREF && e1.Arg1.ExpressionType == Instruction.PUSHGLOBALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH && expr.IsVoidContext() && e1.Variable.DataType.IsPrimitiveType() && e2.Variable.DataType.IsPrimitiveType())
                {
                    expr.ExpressionType = Instruction.SH_GLOBAL_ASSIGN_LOCAL;
                    expr.Value = e1.Arg2.Value;
                    expr.Value2 = e2.Value;
                    expr.Args.Clear();
                    return true;
                }

                if (expr.ExpressionType == Instruction.A_PUSHBACK && i1 == Instruction.AssignmentRef && i2 == Instruction.SH_LOCAL_S_REF && e1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH)
                {
                    var v1 = e1.Variable;
                    if ((v1.DataType == DataType.ArrayString || v1.DataType == DataType.RefArrayString) && v1.ArrayDimensions == 1)
                    {
                        expr.ExpressionType = Instruction.SH_LOCAL_APUSHBACK_LOCALSREF;
                        expr.Value = e1.Arg2.Value;
                        expr.Value2 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }

                if (expr.ExpressionType == Instruction.A_PUSHBACK && i1 == Instruction.AssignmentRef && i2 == Instruction.SH_LOCAL_S_REF && e1.Arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE && e1.Arg2.ExpressionType == Instruction.PUSH)
                {
                    var v1 = e1.Variable;
                    if ((v1.DataType == DataType.ArrayString || v1.DataType == DataType.RefArrayString) && v1.ArrayDimensions == 1)
                    {
                        expr.ExpressionType = Instruction.SH_STRUCT_APUSHBACK_LOCALSREF;
                        expr.Value = e1.Arg2.Value;
                        expr.Value2 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                if (expr.ExpressionType == Instruction.S_ASSIGN && (i1 == Instruction.SH_LOCALREF || i1 == Instruction.SH_LOCAL_S_REF) && i2 == Instruction.SH_LOCAL_S_REF && expr.IsVoidContext())
                {
                    expr.ExpressionType = Instruction.SH_LOCALREF_SASSIGN_LOCALSREF;
                    expr.Value = e1.Value;
                    expr.Value2 = e2.Value;
                    expr.Args.Clear();
                    return true;
                }

                if (expr.ExpressionType == Instruction.S_ASSIGN && i2 == Instruction.SH_LOCAL_S_REF && expr.IsVoidContext())
                {
                    expr.ExpressionType = Instruction.SH_SASSIGN_LOCALSREF;
                    expr.Value = e2.Value;
                    expr.Args.RemoveAt(1);
                    return true;
                }

                if (expr.ExpressionType == Instruction.A_PUSHBACK && i1 == Instruction.AssignmentRef && i2 == Instruction.SR_REF && e1.Arg1.ExpressionType == Instruction.PUSHGLOBALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH)
                {
                    if (e2.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && e2.Arg2.ExpressionType == Instruction.PUSH)
                    {
                        expr.ExpressionType = Instruction.SH_GLOBAL_A_PUSHBACK_LOCAL_STRUCT;
                        expr.Value = e1.Arg2.Value;
                        expr.Value2 = e2.Arg2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }

                if (expr.ExpressionType == Instruction.A_PUSHBACK && i1 == Instruction.AssignmentRef && i2 == Instruction.SR_REF && e1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH)
                {
                    if (e2.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && e2.Arg2.ExpressionType == Instruction.PUSH)
                    {
                        expr.ExpressionType = Instruction.SH_LOCAL_A_PUSHBACK_LOCAL_STRUCT;
                        expr.Value = e1.Arg2.Value;
                        expr.Value2 = e2.Arg2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }

                if (expr.ExpressionType == Instruction.S_EMPTY && e1.ExpressionType == Instruction.S_REF2)
                {
                    if (e1.Arg1.Variable.DataType.IsStruct())
                    {
                        var arg1 = e1.Arg1;
                        var arg2 = new Expression(Instruction.PUSH, -1, e1.Value);
                        expr.Args.Clear();
                        expr.Args.Add(arg1);
                        expr.Args.Add(arg2);
                        expr.ExpressionType = Instruction.SH_SREF_EMPTY;
                        return true;
                    }
                }

                if (expr.ExpressionType == Instruction.A_PUSHBACK && i1 == Instruction.AssignmentRef && i2 == Instruction.SR_REF && e1.Arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE && e1.Arg2.ExpressionType == Instruction.PUSH)
                {
                    if (e1.Variable.DataType == DataType.ArrayStruct)
                    {
                        if (e2.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && e2.Arg2.ExpressionType == Instruction.PUSH)
                        {
                            expr.Value = e1.Arg2.Value;
                            expr.Value2 = e2.Arg2.Value;
                            expr.Args.Clear();
                            expr.ExpressionType = Instruction.SH_STRUCT_A_PUSHBACK_LOCAL_STRUCT;
                            return true;
                        }
                    }
                    //&& e1.ExpressionType == Instruction.AssignmentRef && e1.Variable.DataType == DataType.ArrayStruct && 
                }

                if (expr.ExpressionType == Instruction.S_NOTE && i1 == Instruction.SH_STRUCT_S_REF && i2 == Instruction.SH_LOCAL_S_REF)
                {
                    expr.Value = e1.Value;
                    expr.Value2 = e2.Value;
                    expr.Args.Clear();
                    expr.ExpressionType = Instruction.SH_STRUCTSREF_NE_LOCALSREF;
                    return true;
                }

                if (expr.ExpressionType == Instruction.S_EQUALE && i1 == Instruction.SH_STRUCT_S_REF && i2 == Instruction.SH_LOCAL_S_REF)
                {
                    expr.Value = e1.Value;
                    expr.Value2 = e2.Value;
                    expr.Args.Clear();
                    expr.ExpressionType = Instruction.SH_STRUCTSREF_EQ_LOCALSREF;
                    return true;
                }

                if (!UsesMsg1)
                {
                    if (expr.ExpressionType == Instruction.ASSIGN && i1 == Instruction.AssignmentRef && e1.Arg1.ExpressionType == Instruction.SH_LOCALREF && e1.Arg2.ExpressionType == Instruction.PUSH && e1.Arg1.Variable.DataType.IsStruct() && expr.IsVoidContext())
                    {
                        if (i2 == Instruction.PUSH)
                        {
                            expr.Value = e1.Arg1.Value;
                            expr.Value2 = e1.Arg2.Value;
                            expr.Value3 = e2.Value;
                            expr.ExpressionType = Instruction.SH_LOCALSTRUCT_ASSIGN_IMM;
                            expr.Args.Clear();
                            return true;
                        }
                        else if (i2 == Instruction.ITOB && e2.Arg1.ExpressionType == Instruction.PUSH)
                        {
                            expr.Value = e1.Arg1.Value;
                            expr.Value2 = e1.Arg2.Value;
                            expr.Value3 = e2.Arg1.Value;
                            expr.ExpressionType = Instruction.SH_LOCALSTRUCT_ASSIGN_IMM;
                            expr.Args.Clear();
                            return true;
                        }
                    }
                }

                if (expr.ExpressionType == Instruction.S_ASSIGN && e2.ExpressionType == Instruction.S_REF)
                {
                    var v1 = e2.Arg1.Variable;
                    if (v1 != null /*&& v1.DataType.IsArray()*/)
                    {
                        var v2 = e2.Arg2.Variable;
                        if (v2 != null && v2.DataType == DataType.Int)
                        {
                            e2.ExpressionType = Instruction.AssignmentRef;
                            expr.ExpressionType = Instruction.SH_S_ASSIGN_REF;
                            return true;
                        }
                    }
                }

                if (expr.ExpressionType == Instruction.S_ASSIGN && e2.ExpressionType == Instruction.SH_STRUCT_S_REF)
                {
                    expr.ExpressionType = Instruction.SH_SASSIGN_STRUCTSREF;
                    expr.Value = expr.Arg2.Value;
                    expr.Args.RemoveAt(1);
                    return true;
                }

                if (expr.ExpressionType == Instruction.ASSIGN && e1.ExpressionType == Instruction.REFREF && e2.ExpressionType == Instruction.REF)
                {
                    if (e1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH &&
                        e2.Arg1.ExpressionType == Instruction.SH_STRUCTREF && e2.Arg2.ExpressionType == Instruction.PUSH)
                    {
                        expr.ExpressionType = Instruction.SH_REF_LOCAL_ASSIGN_STRUCTREF2;
                        expr.Value = e2.Arg1.Value;
                        expr.Value2 = e1.Arg2.Value;
                        expr.Value3 = e2.Arg2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }

                if (expr.ExpressionType == Instruction.ASSIGN && e1.ExpressionType == Instruction.SH_LOCALREFREF && e2.ExpressionType == Instruction.REF)
                {
                    if (e2.Arg1.ExpressionType == Instruction.SH_STRUCTREF && e2.Arg2.ExpressionType == Instruction.PUSH)
                    {
                        expr.ExpressionType = Instruction.SH_REF_LOCAL_ASSIGN_STRUCTREF2;
                        expr.Value = e2.Arg1.Value;
                        expr.Value2 = e1.Value;
                        expr.Value3 = e2.Arg2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }


                //TODO: SH_REF_STRUCTREF2
                //SH_STRUCT_APUSHBACK_LOCALSREF
                //SH_LOCALREF_SASSIGN_LOCALSREF
                //SH_S_ASSIGN_REF
                //SH_STRUCTREF_GT_IMM

                if (expr.ExpressionType == Instruction.GT && e1.ExpressionType == Instruction.SH_STRUCTREF && e2.ExpressionType == Instruction.PUSH)
                {
                    expr.ExpressionType = Instruction.SH_STRUCTREF_GT_IMM;
                    expr.Value = expr.Arg1.Value;
                    expr.Value2 = expr.Arg2.Value;
                    expr.Args.Clear();
                    return true;
                }

                if (expr.ExpressionType == Instruction.S_EMPTY && e1.ExpressionType == Instruction.S_REF)
                {
                    expr.ExpressionType = Instruction.SH_SREF_EMPTY;
                    expr.Arg1.ExpressionType = Instruction.AssignmentRef;
                    return true;
                }

                return false;

                //Needs additional checks
                if (expr.ExpressionType == Instruction.S_REF && (i1 == Instruction.SH_LOCALREF || i1 == Instruction.ASSIGN) && i2 == Instruction.PUSH && e1.Variable.DataType == DataType.RefStruct)
                {
                    if (RefOkayToShorten(expr))
                    {
                        expr.ExpressionType = Instruction.S_REF2;
                        expr.Value = e2.Value;
                        expr.Args.RemoveAt(1);
                        return true;
                    }
                }
                //needs additional checks
                if (expr.ExpressionType == Instruction.REF && i1 == Instruction.SH_STRUCTREF && i2 == Instruction.PUSH && e1.Variable.DataType == DataType.Struct)
                {
                    if (RefOkayToShorten(expr))
                    {
                        expr.ExpressionType = Instruction.SH_STRUCTREF2;
                        expr.Value = e1.Value;
                        expr.Value2 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                //needs additional checks
                if (expr.ExpressionType == Instruction.REF && i1 == Instruction.SH_STRUCTREF2 && i2 == Instruction.PUSH && e1.Variable.DataType == DataType.Struct)
                {
                    if (RefOkayToShorten(expr))
                    {
                        expr.ExpressionType = Instruction.SH_STRUCTREF3;
                        expr.Value = e1.Value;
                        expr.Value2 = e1.Value2;
                        expr.Value3 = e2.Value;
                        expr.Args.Clear();
                        return true;
                    }
                }
                //Needs additional checks
                if (expr.ExpressionType == Instruction.REF && (i1 == Instruction.SH_LOCALREF || i1 == Instruction.SH_STRUCTREF || i1 == Instruction.SH_GLOBALREF || i1 == Instruction.ASSIGN || i1 == Instruction.REF) && i2 == Instruction.PUSH && (e1.Variable.DataType == DataType.RefStruct || e1.Variable.DataType == DataType.Struct) &&
                    expr.Variable.DataType.IsPrimitiveType())
                {
                    if (RefOkayToShorten(expr))
                    {
                        expr.ExpressionType = Instruction.PAGE_REF;
                        expr.Value = e2.Value;
                        expr.Args.RemoveAt(1);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool RefOkayToShorten(Expression expr)
        {
            if (expr.ExpressionType == Instruction.REF ||
                expr.ExpressionType == Instruction.S_REF ||
                expr.ExpressionType == Instruction.SR_REF ||
                expr.ExpressionType == Instruction.SR_REFREF)
            {
                //none of these are confirmed, so it's just a bunch of wild guesses

                //check for specific exceptions to the peephole rules
                var variable = expr.Variable;
                if (variable != null)
                {
                    //var dataType = variable.DataType;
                    //if (dataType.IsString())
                    {
                        var parent = expr.SkipParentCastOperations() ?? Expression.Empty;
                        //if (parent.ExpressionType == Instruction.RETURN)
                        //{
                        //    if (currentFunction.DataType == variable.DataType.GetTypeOfRef())
                        //    {
                        //        return false;
                        //    }
                        //    else
                        //    {

                        //    }
                        //}
                        //if (parent.ExpressionType == Instruction.SWITCH)
                        //{
                        //    return false;
                        //}

                        var functionParameter = expr.GetFunctionCallParameter2();

                        if (functionParameter != null)
                        {
                            var calledFunction = functionParameter.Parent as IFunction;
                            var functionCallExpression = expr.GetFunctionCallAncestor();
                            bool isVoidContext = false;
                            if (functionCallExpression != null)
                            {
                                isVoidContext = IsVoidContext(functionCallExpression);
                            }

                            //if (calledFunction != null && functionCallExpression != null)
                            //{
                            //    //if (isVoidContext && functionParameter.DataType.GetTypeOfRef() == variable.DataType.GetTypeOfRef())
                            //    //{
                            //    //    return false;
                            //    //}
                            //    bool hasBinaryOperator = !isVoidContext && functionCallExpression.GetOtherSideOfBinaryExpression() != null;
                            //    bool hasUnaryOperator = !isVoidContext && Expression.IsUnary(functionCallExpression.SkipParentCastOperations().ExpressionType);

                            //    if ((hasBinaryOperator || hasUnaryOperator) && functionParameter.DataType.GetTypeOfRef() == variable.DataType.GetTypeOfRef())
                            //    {
                            //        //???
                            //        return false;
                            //    }
                            //}
                            if (functionCallExpression.ExpressionType == Instruction.CALLSYS)
                            {
                                return false;
                            }
                            var parentOfFunctionCall = functionCallExpression.SkipParentCastOperations();
                            if (parentOfFunctionCall != null && parentOfFunctionCall.ExpressionType == Instruction.RETURN)
                            {
                                //???
                                return false;
                            }
                        }

                        //if (functionParameter != null && (variable.Parent == null || variable.Index < variable.Parent.ParameterCount) && !functionParameter.DataType.IsRef() /* && functionParameterParent != null && functionParameterParent.DataType == DataType.Void*/)
                        //{
                        //    return false;
                        //}
                        //if (functionParameter != null && /*functionParameter.Index == 0 && */ functionParameter.Parent is IFunction && functionParameter.Parent.DataType == DataType.Void)
                        //{
                        //    //???
                        //    return false;
                        //}

                        int exprIndex = expr.GetIndex();
                        if (parent.ExpressionType.IsFunctionCall())
                        {
                            if (parent.Parent != null && parent.Parent.ExpressionType != Instruction.Statement)
                            {
                                exprIndex = parent.GetIndex();
                                if (exprIndex > 0)
                                {
                                    //???  removed - breaks Daibanchou
                                    //return false;
                                }
                                //???
                                parent = parent.Parent;
                            }
                        }

                        if (parent.ExpressionType.IsAssignment() && exprIndex != 0)
                        {
                            return false;
                        }

                        if (parent.ExpressionType.IsBinary())
                        {
                            if (exprIndex != 0)
                            {
                                return false;
                            }
                            //if (parent.ExpressionType == Instruction.S_NOTE)
                            //{

                            //}
                            //else
                            //{
                            //    return false;
                            //}
                        }

                        if (parent.ExpressionType.IsCast())
                        {
                            return false;
                        }
                        //if (parent.ExpressionType == Instruction.S_EMPTY ||
                        //    parent.ExpressionType == Instruction.S_ERASE ||
                        //    parent.ExpressionType == Instruction.S_FIND ||
                        //    parent.ExpressionType == Instruction.S_GETPART ||
                        //    parent.ExpressionType == Instruction.S_LENGTH ||
                        //    parent.ExpressionType == Instruction.S_LENGTHBYTE ||
                        //    parent.ExpressionType == Instruction.S_POPBACK ||
                        //    parent.ExpressionType == Instruction.S_PUSHBACK ||
                        //    parent.ExpressionType == Instruction.STOI ||
                        //    parent.ExpressionType == Instruction.FT_ASSIGNS)
                        //{
                        //    return false;
                        //}

                        //if (parent.ExpressionType == Instruction.A_PUSHBACK ||
                        //    parent.ExpressionType == Instruction.A_INSERT)
                        //{
                        //    return false;
                        //}

                        var parent2 = expr.SkipParentCastOperations();
                        while (parent2 != null && parent2.ExpressionType == Instruction.Comma)
                        {
                            parent2 = parent2.Parent;
                        }

                        if (parent2 != null && parent2.ExpressionType == Instruction.A_FIND)
                        {
                            return false;
                        }

                        //if (parent.ExpressionType == Instruction.A_PUSHBACK && exprIndex == 1)
                        //{
                        //    return false;
                        //}
                    }
                }
            }
            return true;
        }

        private bool IsVoidContext(Expression expr)
        {
            var parent = expr.Parent;
            int childIndex = expr.GetIndex();
            while (parent != null && parent.ExpressionType.IsCast())
            {
                childIndex = expr.GetIndex();
                expr = parent;
                parent = expr.Parent;
            }
            if (parent == null)
            {
                return true;
            }
            //var childExpression = parent.Args.GetOrNull(childIndex);

            var instruction = parent.ExpressionType;

            if (instruction == Instruction.Statement ||
                (instruction == Instruction.ForLoop && (childIndex == 0 || childIndex >= 2)) ||
                (instruction == Instruction.WhileLoop && childIndex >= 1) ||
                (instruction == Instruction.DoWhileLoop && childIndex >= 1))
            {
                return true;
            }
            if (instruction == Instruction.IfElse || instruction == Instruction.Comma)
            {
                if (IsVoidContext(parent))
                {
                    return true;
                }
            }
            return false;
        }

        private void Peephole(Expression baseExpression)
        {
            if (baseExpression == null) return;
            for (int i = 0; i < baseExpression.Args.Count; i++)
            {
                var expr = baseExpression.Args[i];
                if (expr != null && (expr.ExpressionType == Instruction.CALLFUNC ||
                    expr.ExpressionType == Instruction.CALLFUNC2 ||
                    expr.ExpressionType == Instruction.DG_CALLBEGIN ||
                    expr.ExpressionType == Instruction.CALLMETHOD ||
                    expr.ExpressionType == Instruction.CALLSYS ||
                    expr.ExpressionType == Instruction.CALLHLL ||
                    expr.ExpressionType == Instruction.RETURN ||
                    StringInstructionsSet.Contains(expr.ExpressionType)))
                {
                    bool oldPeepholeDontShortenSrRef = peepholeDontShortenSrRef;
                    if (expr.ExpressionType == Instruction.RETURN /* || expr.ExpressionType == Instruction.CALLHLL */)
                    {
                        peepholeDontShortenSrRef = true;
                    }
                    else
                    {
                        peepholeDontShortenSrRef = false;
                    }
                    bool oldPeepholeDontShortenSRef = peepholeDontShortenSRef;
                    peepholeDontShortenSRef = true;
                    Peephole(expr);
                    peepholeDontShortenSRef = oldPeepholeDontShortenSRef;
                    peepholeDontShortenSrRef = oldPeepholeDontShortenSrRef;
                }
                else
                {
                    Peephole(expr);
                }
            }

            {
                var expr = baseExpression;
                var e1 = expr.Arg1;
                var e2 = expr.Arg2;
                IVariable v1 = null, v2 = null;
                if (e1 != null && e2 != null)
                {
                    v1 = e1.Variable;
                    v2 = e2.Variable;
                }
                Instruction i1 = 0, i2 = 0;
                if (e1 != null && e2 != null)
                {
                    i1 = e1.ExpressionType;
                    i2 = e2.ExpressionType;
                }

                if (expr.ExpressionType == Instruction.ITOF)
                {
                    if (expr.Arg1 != null && expr.Arg1.ExpressionType == Instruction.PUSH)
                    {
                        float floatValue = (float)expr.Arg1.Value;
                        expr.ExpressionType = Instruction.F_PUSH;
                        expr.Value = floatValue.FloatToInt();
                        expr.Args.Clear();
                    }
                }
                if (expr.ExpressionType == Instruction.INV)
                {
                    if (expr.Arg1 != null && expr.Arg1.ExpressionType == Instruction.PUSH)
                    {
                        expr.ExpressionType = Instruction.PUSH;
                        expr.Value = -(expr.Arg1.Value);
                        expr.Args.Clear();
                    }
                }
                if (expr.ExpressionType == Instruction.ASSIGN)
                {
                    //var e1 = expr.Arg1;
                    //var e2 = expr.Arg2;
                    if (e1 != null && e2 != null)
                    {
                        if (i1 == Instruction.AssignmentRef && i2 == Instruction.PUSH)
                        {
                            if (e1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && e1.Arg2.ExpressionType == Instruction.PUSH)
                            {
                                expr.ExpressionType = Instruction.SH_LOCALASSIGN;
                                expr.Value = e1.Arg2.Value;
                                expr.Value2 = e2.Value;
                                expr.Args.Clear();
                            }
                        }
                        else if (i1 == Instruction.SH_LOCALREF && i2 == Instruction.PUSH)
                        {
                            expr.ExpressionType = Instruction.SH_LOCALASSIGN;
                            expr.Value = e1.Value;
                            expr.Value2 = e2.Value2;
                            expr.Args.Clear();
                        }
                    }
                }
                if (expr.ExpressionType == Instruction.REF ||
                    (expr.ExpressionType == Instruction.S_REF && peepholeDontShortenSRef == false) ||
                    (expr.ExpressionType == Instruction.SR_REF && peepholeDontShortenSrRef == false) ||
                    expr.ExpressionType == Instruction.SR_REFREF)
                {
                    if (e1 != null && e2 != null)
                    {
                        if (i1 == Instruction.PUSHLOCALPAGE && i2 == Instruction.PUSH)
                        {
                            expr.ExpressionType = Instruction.SH_LOCALREF;
                            expr.Value = e2.Value;
                            expr.Args.Clear();
                        }
                        else if (i1 == Instruction.PUSHGLOBALPAGE && i2 == Instruction.PUSH)
                        {
                            expr.ExpressionType = Instruction.SH_GLOBALREF;
                            expr.Value = e2.Value;
                            expr.Args.Clear();
                        }
                        else if (i1 == Instruction.PUSHSTRUCTPAGE && i2 == Instruction.PUSH)
                        {
                            expr.ExpressionType = Instruction.SH_STRUCTREF;
                            expr.Value = e2.Value;
                            expr.Args.Clear();
                        }
                    }
                    else if (e1 != null && e1.ExpressionType == Instruction.SR_REFREF)
                    {
                        e2 = e1.Arg2;
                        e1 = e1.Arg1;

                        if (i1 == Instruction.PUSHLOCALPAGE && i2 == Instruction.PUSH)
                        {
                            expr.ExpressionType = Instruction.SH_LOCALREF;
                            expr.Value = e2.Value;
                            expr.Args.Clear();
                        }
                    }
                }
                else if (expr.ExpressionType == Instruction.INC || expr.ExpressionType == Instruction.DEC || expr.ExpressionType == Instruction.PreIncrement || expr.ExpressionType == Instruction.PreDecrement)
                {
                    if (e1.ExpressionType == Instruction.AssignmentRef)
                    {
                        e2 = e1.Arg2;
                        e1 = e1.Arg1;
                        if (e1.ExpressionType == Instruction.PUSHLOCALPAGE && e2.ExpressionType == Instruction.PUSH)
                        {
                            if (expr.Parent.ExpressionType == Instruction.Statement || expr.Parent.ExpressionType == Instruction.ForLoop || (expr.Parent.ExpressionType == Instruction.IfElse && expr.Parent.Parent.ExpressionType == Instruction.Statement))
                            {
                                if (expr.ExpressionType == Instruction.PreIncrement || expr.ExpressionType == Instruction.INC)
                                {
                                    expr.ExpressionType = Instruction.SH_LOCALINC;
                                }
                                else
                                {
                                    expr.ExpressionType = Instruction.SH_LOCALDEC;
                                }
                                expr.Value = e2.Value;
                                expr.Args.Clear();
                            }
                        }
                    }
                }
            }
        }

        #region Operator Tables
        internal static readonly Dictionary<TokenType, Instruction> IntegerAssignmentOperators = GetIntegerAssignmentOperators();
        internal static readonly Dictionary<TokenType, Instruction> IntegerBinaryOperators = GetIntegerBinaryOperators();
        internal static readonly Dictionary<TokenType, Instruction> IntegerUnaryOperators = GetIntegerUnaryOperators();
        internal static readonly Dictionary<TokenType, Instruction> IntegerUnaryAssignmentOperators = GetIntegerUnaryAssignmentOperators();
        protected static readonly Dictionary<Instruction, Instruction> FloatInstructions = GetFloatInstructions();
        protected static readonly Dictionary<Instruction, Instruction> LintInstructions = GetLintInstructions();
        protected static readonly Dictionary<Instruction, Instruction> StringInstructions = GetStringInstructions();
        protected static readonly Dictionary<Instruction, Instruction> DelegateInstructions = GetDelegateInstructions();
        protected static readonly HashSet<Instruction> StringInstructionsSet = new HashSet<Instruction>(StringInstructions.Values.Concat(new Instruction[] { Instruction.S_EMPTY, Instruction.S_ERASE, Instruction.S_ERASE2, Instruction.S_FIND, Instruction.S_GETPART, Instruction.S_LENGTH, Instruction.S_LENGTH2, Instruction.S_LENGTHBYTE, Instruction.S_LENGTHBYTE2, Instruction.S_POPBACK, Instruction.S_POPBACK2, Instruction.S_PUSHBACK2, Instruction.S_PUSHBACK }));

        private static Dictionary<T1, T2> BuildDictionary<T1, T2>(IList<T1> keys, IList<T2> values)
        {
            if (keys.Count != values.Count)
            {
                throw new ArgumentException("Sizes of arrays don't match!");
            }
            Dictionary<T1, T2> dic = new Dictionary<T1, T2>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var value = values[i];
                dic.Add(key, value);
            }
            return dic;
        }

        private static Dictionary<TokenType, Instruction> GetIntegerAssignmentOperators()
        {
            TokenType[] tokens = new TokenType[]
            {
                TokenType.AndAssign, TokenType.Assign, TokenType.DivideAssign, TokenType.LeftShiftAssign, 
                TokenType.MinusAssign, TokenType.ModuloAssign, TokenType.MultiplyAssign, TokenType.PlusAssign, 
                TokenType.RightShiftAssign, TokenType.XorAssign, TokenType.OrAssign,
            };
            Instruction[] instructions = new Instruction[]
            {
                Instruction.ANDA, Instruction.ASSIGN, Instruction.DIVA, Instruction.LSHIFTA,
                Instruction.MINUSA, Instruction.MODA, Instruction.MULA, Instruction.PLUSA,
                Instruction.RSHIFTA, Instruction.XORA, Instruction.ORA
            };
            return BuildDictionary(tokens, instructions);
        }

        private static Dictionary<TokenType, Instruction> GetIntegerBinaryOperators()
        {
            TokenType[] tokens = new TokenType[]
            {
                TokenType.And, TokenType.Divide, TokenType.EqualTo, TokenType.GreaterThan,
                TokenType.GreaterThanOrEqualTo, TokenType.LeftShift, TokenType.LessThan, TokenType.LessThanOrEqualTo,
                TokenType.LogicalAnd, TokenType.LogicalOr, TokenType.Minus, TokenType.Modulo,
                TokenType.Multiply, TokenType.NotEqualTo, TokenType.Or, TokenType.Plus,
                TokenType.RightShift, TokenType.Xor
            };
            Instruction[] instructions = new Instruction[]
            {
                Instruction.AND, Instruction.DIV, Instruction.EQUALE, Instruction.GT,
                Instruction.GTE, Instruction.LSHIFT, Instruction.LT, Instruction.LTE,
                Instruction.LogicalAnd, Instruction.LogicalOr, Instruction.SUB, Instruction.MOD,
                Instruction.MUL, Instruction.NOTE, Instruction.OR, Instruction.ADD,
                Instruction.RSHIFT, Instruction.XOR
            };
            return BuildDictionary(tokens, instructions);
        }

        private static Dictionary<TokenType, Instruction> GetIntegerUnaryOperators()
        {
            TokenType[] tokens = new TokenType[]
            {
                TokenType.Complement, TokenType.Negative, TokenType.Not, TokenType.Positive
            };
            Instruction[] instructions = new Instruction[]
            {
                Instruction.COMPL, Instruction.INV, Instruction.NOT, Instruction.Nop
            };
            return BuildDictionary(tokens, instructions);
        }

        private static Dictionary<TokenType, Instruction> GetIntegerUnaryAssignmentOperators()
        {
            TokenType[] tokens = new TokenType[]
            {
                TokenType.PostDecrement, TokenType.PostIncrement, TokenType.PreDecrement, TokenType.PreIncrement
            };
            Instruction[] instructions = new Instruction[]
            {
                Instruction.DEC, Instruction.INC, Instruction.PreDecrement, Instruction.PreIncrement
            };
            return BuildDictionary(tokens, instructions);
        }

        private static Dictionary<Instruction, Instruction> GetFloatInstructions()
        {
            Instruction[] floatInstructions = new Instruction[]
            {
                Instruction.F_ADD, Instruction.F_ASSIGN, Instruction.F_DIV, Instruction.F_DIVA,
                Instruction.F_EQUALE, Instruction.F_GT, Instruction.F_GTE, Instruction.F_INV,
                Instruction.F_LT, Instruction.F_LTE, Instruction.F_MINUSA, Instruction.F_MUL,
                Instruction.F_MULA, Instruction.F_NOTE, Instruction.F_PLUSA, Instruction.F_PUSH,
                Instruction.F_SUB,
            };
            Instruction[] intInstructions = new Instruction[]
            {
                Instruction.ADD, Instruction.ASSIGN, Instruction.DIV, Instruction.DIVA,
                Instruction.EQUALE, Instruction.GT, Instruction.GTE, Instruction.INV,
                Instruction.LT, Instruction.LTE, Instruction.MINUSA, Instruction.MUL,
                Instruction.MULA, Instruction.NOTE, Instruction.PLUSA, Instruction.PUSH,
                Instruction.SUB,
            };
            return BuildDictionary(intInstructions, floatInstructions);
        }

        private static Dictionary<Instruction, Instruction> GetLintInstructions()
        {
            Instruction[] lintInstructions = new Instruction[]
            {
                Instruction.LI_ADD, Instruction.LI_ANDA, Instruction.LI_ASSIGN, Instruction.LI_DEC,
                Instruction.LI_DIV, Instruction.LI_DIVA, Instruction.LI_INC, Instruction.LI_LSHIFTA,
                Instruction.LI_MINUSA, Instruction.LI_MOD, Instruction.LI_MODA, Instruction.LI_MUL,
                Instruction.LI_MULA, Instruction.LI_ORA, Instruction.LI_PLUSA, Instruction.LI_RSHIFTA,
                Instruction.LI_SUB, Instruction.LI_XORA
            };
            Instruction[] intInstructions = new Instruction[]
            {
                Instruction.ADD, Instruction.ANDA, Instruction.ASSIGN, Instruction.DEC,
                Instruction.DIV, Instruction.DIVA, Instruction.INC, Instruction.LSHIFTA,
                Instruction.MINUSA, Instruction.MOD, Instruction.MODA, Instruction.MUL,
                Instruction.MULA, Instruction.ORA, Instruction.PLUSA, Instruction.RSHIFTA,
                Instruction.SUB, Instruction.XORA
            };
            return BuildDictionary(intInstructions, lintInstructions);
        }

        private static Dictionary<Instruction, Instruction> GetStringInstructions()
        {
            Instruction[] stringInstructions = new Instruction[]
            {
                Instruction.S_ADD, Instruction.S_ASSIGN, Instruction.S_EQUALE, Instruction.S_GT,
                Instruction.S_GTE, Instruction.S_LT, Instruction.S_LTE, Instruction.S_MOD,
                Instruction.S_NOTE, Instruction.S_PLUSA, Instruction.S_PUSH
            };
            Instruction[] intInstructions = new Instruction[]
            {
                Instruction.ADD, Instruction.ASSIGN, Instruction.EQUALE, Instruction.GT,
                Instruction.GTE, Instruction.LT, Instruction.LTE, Instruction.MOD,
                Instruction.NOTE, Instruction.PLUSA, Instruction.PUSH
            };
            return BuildDictionary(intInstructions, stringInstructions);
        }

        private static Dictionary<Instruction, Instruction> GetDelegateInstructions()
        {
            Instruction[] delegateInstructions = new Instruction[]
            {
                Instruction.DG_PLUSA, Instruction.DG_MINUSA, Instruction.DG_ASSIGN,
            };
            Instruction[] intInstructions = new Instruction[]
            {
                Instruction.PLUSA, Instruction.MINUSA, Instruction.ASSIGN,
            };
            return BuildDictionary(intInstructions, delegateInstructions);
        }
        #endregion

        bool _unknownSymbolOkay = false;

        private Expression GenerateExpression(TokenExpression token)
        {
            if (token == null) return null;
            var tokenType = token.TokenType;
            switch (tokenType)
            {
                case TokenType.Block:
                    {
                        return GenerateBlockExpression(token, false, false);
                    }
                    break;
                case TokenType.Identifier:
                    {
                        string symbolName = token.Token.Value;
                        var symbol = localScope.GetOrNull(symbolName);
                        var expression = SymbolToExpression(symbol, symbolName);
                        if (expression == null && _unknownSymbolOkay == false)
                        {
                            RaiseError2("Unknown symbol", token);
                        }
                        return expression;
                    }
                case TokenType.Dot:
                    {
                        return GenerateDot(token);
                    }
                    break;

                case TokenType.FunctionCall:
                    {
                        return GenerateFunctionCallExpression(token);
                    }
                    break;
                case TokenType.Const:
                    {
                        var variableDeclarationToken = token.Subexpressions.GetOrNull(0);
                        var dataTypeToken = variableDeclarationToken.Subexpressions.GetOrNull(0);
                        var dataType = GetDataType(dataTypeToken);
                        InitialValue lastValue = null;

                        var secondPass = new SecondPass(this);
                        for (int i = 1; i < variableDeclarationToken.Subexpressions.Count; i++)
                        {
                            var variableToken = variableDeclarationToken.Subexpressions[i];
                            TokenExpression assignToken = null;
                            if (variableToken.TokenType == TokenType.Assign)
                            {
                                assignToken = variableToken;
                                variableToken = variableToken.Subexpressions[0];
                            }
                            string variableName = variableToken.Token.Value;
                            if (localScope.Dic.ContainsKey(variableName))
                            {
                                RaiseError2("Duplicate variable name", variableDeclarationToken);
                                return Expression.Empty;
                                //error: duplicate variable name
                            }
                            var variable = new Variable();
                            variable.Name = variableName;
                            variable.SetVariableType(dataType);
                            localScope.Add(variable.Name, variable);
                            variable.Root = ainFile;
                            variable.Parent = null;
                            variable.Index = 0;

                            if (assignToken != null)
                            {
                                var initialValue = secondPass.EvaluateConstExpression(assignToken.Subexpressions[1], dataType.DataType);
                                this.InitialValues[variable] = initialValue;
                                lastValue = initialValue;
                            }
                            else
                            {
                                if (lastValue != null)
                                {
                                    //const with no value - add 1 to last value
                                    lastValue = lastValue.Clone();
                                    if (lastValue.DataType != DataType.Int)
                                    {
                                        RaiseError2("No value for constant", variableDeclarationToken);

                                        //error?
                                    }
                                    else
                                    {
                                        lastValue.IntValue++;
                                    }
                                    this.InitialValues[variable] = lastValue;
                                }
                            }
                        }
                        return null;
                    }
                    break;
                case TokenType.VariableDeclaration:
                    {
                        var function = currentFunction;
                        var dataTypeToken = token.Subexpressions.GetOrNull(0);
                        var dataType = GetDataType(dataTypeToken);
                        if (dataType.DataType == DataType.Void)
                        {

                        }

                        List<Expression> assignStatements = new List<Expression>();

                        for (int i = 1; i < token.Subexpressions.Count; i++)
                        {
                            var variableToken = token.Subexpressions[i];
                            TokenExpression assignToken = null;
                            if (variableToken.TokenType == TokenType.Assign)
                            {
                                assignToken = variableToken;
                                variableToken = variableToken.Subexpressions[0];
                            }
                            if (variableToken.Subexpressions.Count > 0 && variableToken.Subexpressions[0].TokenType == TokenType.ArrayBoundsDeclaration)
                            {
                                assignToken = variableToken.Subexpressions[0];
                            }

                            var variable = dataType.Clone();
                            variable.Name = variableToken.Token.Value;
                            if (localScope.Dic.ContainsKey(variable.Name))
                            {
                                //error: duplicate variable name
                                RaiseError("Duplicate variable name: " + variable.Name + ", new variable will replace the old variable", token);
                                //variable = localScope.Dic[variable.Name] as Variable;
                            }
                            //else
                            {
                                //create the new variable
                                localScope.Dic[variable.Name] = variable;
                                int newIndex = function.Parameters.Count;
                                function.Parameters.Add(variable);
                                variable.Index = newIndex;
                                variable.Parent = function;
                                variable.Root = ainFile;
                                if (variable.DataType.IsPrimitiveRefType())
                                {
                                    var voidParameter = new Variable();
                                    voidParameter.DataType = DataType.Void;
                                    voidParameter.Name = "<void>";
                                    voidParameter.Index = function.Parameters.Count;
                                    function.Parameters.Add(voidParameter);
                                }
                            }

                            //TODO: generate code for creating structs or arrays
                            if (variable.DataType == DataType.Struct)
                            {
                                assignStatements.Add(new Expression(Instruction.SH_LOCALDELETE, -1, variable.Index));
                                assignStatements.Add(new Expression(Instruction.SH_LOCALCREATE, -1, variable.Index, variable.StructType));
                            }

                            Expression assignmentExpression = null;
                            if (assignToken != null && assignToken.TokenType == TokenType.Assign)
                            {
                                if (variable.DataType.IsRef())
                                {
                                    var newToken = assignToken.Clone();
                                    newToken.TokenType = TokenType.ReferenceAssign;

                                    bool oldGenerateLockPeek = this.GenerateLockPeek;
                                    this.GenerateLockPeek = false;
                                    assignmentExpression = GenerateExpression(newToken);
                                    this.GenerateLockPeek = oldGenerateLockPeek;

                                    if (variable.DataType.IsPrimitiveRefType())
                                    {
                                        assignmentExpression.ExpressionType = Instruction.RefIntInitializer;
                                    }
                                    else
                                    {
                                        assignmentExpression.ExpressionType = Instruction.RefInitializer;
                                        var arg1Variable = assignmentExpression.Arg1.Variable;
                                        var arg2Variable = assignmentExpression.Arg2.Variable;
                                        bool isArrayExpression = arg2Variable.Name.EndsWith("]");

                                        if (arg2Variable.DataType != DataType.RefStruct)
                                        {
                                            if (isArrayExpression)
                                            {
                                                assignmentExpression.Value = 1;
                                            }
                                            else
                                            {
                                                assignmentExpression.Value = 1;
                                            }
                                        }
                                        if (arg1Variable.DataType == DataType.RefStruct && arg2Variable.DataType == DataType.RefStruct)
                                        {
                                            if (assignmentExpression.Arg2.ExpressionType == Instruction.R_ASSIGN)
                                            {
                                                assignmentExpression.Arg2.ExpressionType = Instruction.ASSIGN;
                                            }
                                            assignmentExpression.Value = 1;
                                        }
                                        if (arg1Variable.DataType == DataType.RefString && arg2Variable.DataType == DataType.RefString)
                                        {
                                            if (assignmentExpression.Arg2.ExpressionType == Instruction.R_ASSIGN)
                                            {
                                                //breaking here just to see if/when this actually happens, probably stays here, but needs testing.
                                                assignmentExpression.Arg2.ExpressionType = Instruction.ASSIGN;
                                            }
                                            assignmentExpression.Value = 1;
                                        }
                                        if (assignmentExpression.Arg2.ExpressionType == Instruction.PUSH && assignmentExpression.Arg2.Value == -1)
                                        {
                                            //explicit null assignment
                                            assignmentExpression.Value = 2;
                                        }
                                    }
                                }
                                else
                                {
                                    assignmentExpression = GenerateExpression(assignToken);
                                }
                                if (assignmentExpression != null && assignmentExpression.ExpressionType == Instruction.DG_ASSIGN)
                                {
                                    if (assignmentExpression.Arg1.ExpressionType == Instruction.REF)
                                    {
                                        assignmentExpression.Arg1.ExpressionType = Instruction.AssignmentRef;
                                    }
                                }
                            }
                            else if (assignToken != null && assignToken.TokenType == TokenType.ArrayBoundsDeclaration)
                            {
                                List<int> sizes = new List<int>();
                                foreach (var number in assignToken.Subexpressions)
                                {
                                    int value = number.Token.ToInt();
                                    sizes.Add(value);
                                }
                                Expression alloc = new Expression(Instruction.A_ALLOC, -1, variable.ArrayDimensions);
                                alloc.Args.Add(SymbolToExpression(variable));
                                foreach (var size in sizes)
                                {
                                    alloc.Args.Add(new Expression(Instruction.PUSH, -1, size));
                                }
                                assignmentExpression = alloc;
                            }
                            if (assignToken == null)
                            {
                                Expression variableExpression = GenerateExpression(variableToken);
                                Expression initialValue = new Expression(Instruction.PUSH, -1, -1);
                                variableExpression.ExpressionType = Instruction.AssignmentRef;
                                assignmentExpression = new Expression(Instruction.ASSIGN, -1, variableExpression, initialValue);
                                if (variable.DataType == DataType.Int || variable.DataType == DataType.Bool)
                                {
                                    initialValue.Value = 0;
                                }
                                else if (variable.DataType == DataType.String)
                                {
                                    initialValue.Value = 0;
                                    initialValue.ExpressionType = Instruction.S_PUSH;
                                    assignmentExpression.ExpressionType = Instruction.S_ASSIGN;
                                    if (!this.UseSimplifiedCode)
                                    {
                                        variableExpression.ExpressionType = Instruction.SH_LOCALREF;
                                        variableExpression.Value = variable.Index;
                                        variableExpression.Args.Clear();
                                    }
                                }
                                else if (variable.DataType == DataType.Float)
                                {
                                    initialValue.Value = ((float)0).FloatToInt();
                                    initialValue.ExpressionType = Instruction.F_PUSH;
                                    assignmentExpression.ExpressionType = Instruction.F_ASSIGN;
                                }
                                else if (variable.DataType.IsNonRefArray())
                                {
                                    assignmentExpression.Arg1.ExpressionType = Instruction.AssignmentRef;
                                    assignmentExpression.ExpressionType = Instruction.A_FREE;
                                    assignmentExpression.Args.RemoveAt(1);
                                }
                                else if (variable.DataType == DataType.Struct)
                                {
                                    //already created above
                                    assignmentExpression = null;
                                }
                                else if (variable.DataType.IsRef())
                                {
                                    if (variableExpression.ExpressionType == Instruction.AssignmentRef)
                                    {

                                    }
                                    else if (variableExpression.ExpressionType == Instruction.REFREF)
                                    {
                                        variableExpression = variableExpression.Arg1;
                                    }
                                    else
                                    {

                                    }
                                    if (variableExpression.ExpressionType == Instruction.AssignmentRef && variableExpression.Arg1.ExpressionType == Instruction.SR_REFREF)
                                    {
                                        variableExpression = variableExpression.Arg1;
                                        variableExpression.ExpressionType = Instruction.AssignmentRef;
                                    }
                                    if (variableExpression.ExpressionType == Instruction.AssignmentRef && variableExpression.Arg1.ExpressionType == Instruction.REFREF)
                                    {
                                        variableExpression = variableExpression.Arg1;
                                        variableExpression.ExpressionType = Instruction.AssignmentRef;
                                    }

                                    if (variable.DataType.IsPrimitiveRefType())
                                    {
                                        assignmentExpression = new Expression(Instruction.RefIntInitializer, -1, variableExpression);
                                    }
                                    else
                                    {
                                        assignmentExpression = new Expression(Instruction.RefInitializer, -1, variableExpression);
                                    }
                                }
                                else if (variable.DataType == DataType.Delegate)
                                {
                                    assignmentExpression = new Expression(Instruction.DG_CLEAR, -1, assignmentExpression.Arg1);
                                    assignmentExpression.Arg1.ExpressionType = Instruction.REF;
                                }
                            }
                            if (assignmentExpression != null)
                            {
                                assignStatements.Add(assignmentExpression);
                            }

                        }
                        return ConvertToStatements(assignStatements);
                    }
                    break;
                case TokenType.For:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var t2 = token.Subexpressions.GetOrNull(1);
                        var t3 = token.Subexpressions.GetOrNull(2);

                        Expression e1 = null, e2 = null, e3 = null;

                        if (t1 != null)
                        {
                            e1 = GenerateExpression(t1);
                        }
                        if (t2 != null)
                        {
                            e2 = GenerateExpression(t2);
                        }
                        if (t3 != null)
                        {
                            e3 = GenerateExpression(t3);
                        }
                        var blockToken = token.Subexpressions.GetOrNull(3);
                        var blockExpression = GenerateBlockExpression(blockToken, true, true);

                        Expression forLoop = new Expression(Instruction.ForLoop, -1, e1, e2, e3, blockExpression);
                        return forLoop;
                    }
                    break;
                case TokenType.Assign:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var t2 = token.Subexpressions.GetOrNull(1);

                        var e1 = GenerateExpression(t1);
                        var e2 = GenerateExpression(t2);

                        if (e1 == null)
                        {
                            RaiseError2("Could not evaluate left side of assignment operator", t1);
                            return Expression.Empty;
                        }
                        if (e2 == null)
                        {
                            RaiseError2("Could not evaluate right side of assignment operator", t2);
                            return Expression.Empty;
                        }

                        if (e1.ExpressionType == Instruction.AssignmentRef && e1.Arg1.ExpressionType == Instruction.SR_REFREF)
                        {
                            e1 = e1.Arg1;
                        }

                        var v1 = e1.Variable;
                        var v2 = e2.Variable;

                        if (e2.ExpressionType == Instruction.CALLFUNC && e2.Value == 0)
                        {
                            if (v1 != null && v1.DataType == DataType.Functype)
                            {
                                e2.ExpressionType = Instruction.PUSH;
                                v2 = e2.Variable;
                            }
                            else
                            {
                                RaiseError("Cannot assign the value NULL outside of variable declarations.  Use the Reference Assign operator <- instead.", token);
                                var tokenCopy = token.Clone();
                                tokenCopy.TokenType = TokenType.ReferenceAssign;
                                tokenCopy.Token = new Token("<-");
                                return GenerateExpression(tokenCopy);
                            }
                        }

                        if (v2 != null && v1 != null)
                        {
                            if (v1.DataType.IsFloat() && v2.DataType.IsInteger())
                            {
                                e2 = new Expression(Instruction.ITOF, -1, e2);
                            }
                            else if (v1.DataType.IsInteger() && v2.DataType.IsFloat())
                            {
                                e2 = new Expression(Instruction.FTOI, -1, e2);
                            }
                            else if (v1.DataType == DataType.Bool && v2.DataType.IsInteger())
                            {
                                e2 = new Expression(Instruction.ITOB, -1, e2);
                            }
                            else if (v1.DataType == DataType.Bool && v2.DataType == DataType.Bool && v2.Canonicalize() == null)
                            {
                                e2 = new Expression(Instruction.ITOB, -1, e2);
                            }
                            else if (v1.DataType == DataType.Functype && e2.ExpressionType == Instruction.CALLFUNC && e2.Value == 0)
                            {
                                //never executed because we already did the check above?
                                e2.ExpressionType = Instruction.PUSH;
                                v2 = new Variable("NULL", v1.DataType, v1.StructType, v1.ArrayDimensions);
                            }
                            else if (v1.DataType == DataType.Functype && v2.DataType.IsString())
                            {
                                e1.ExpressionType = Instruction.AssignmentRef;
                                return new Expression(Instruction.FT_ASSIGNS, -1, e1, e2, new Expression(Instruction.PUSH, -1, v1.StructType));
                            }
                            else if ((v1.DataType == DataType.Lint || v1.DataType == DataType.RefLint) && (v2.DataType == DataType.Int || v2.DataType == DataType.RefInt))
                            {
                                e2 = new Expression(Instruction.ITOLI, -1, e2);
                            }
                            else if (v1.DataType == DataType.Delegate)
                            {
                                if (e2.ExpressionType == Instruction.DG_NEW_FROM_METHOD)
                                {
                                    e2.ExpressionType = Instruction.PassThroughExpression;
                                    return new Expression(Instruction.DG_SET, -1, e1, e2);
                                }
                                else if (v2.DataType.IsDelegate())
                                {
                                    if (e2.ExpressionType == Instruction.REF)
                                    {
                                        e2 = new Expression(Instruction.DG_COPY, -1, e2);
                                    }
                                    else if (e2.ExpressionType.IsFunctionCall())
                                    {
                                        //do nothing
                                    }
                                    else
                                    {

                                    }
                                    return new Expression(Instruction.DG_ASSIGN, -1, e1, e2);
                                }
                                else
                                {

                                }
                            }
                            else if (v2.DataType == DataType.Void)
                            {
                                RaiseError("Cannot assign Void to a variable.", t2);
                            }
                        }

                        Expression assignmentExpression = null;

                        //TODO, type checking and conversion
                        if (e1.ExpressionType == Instruction.REF || e1.ExpressionType == Instruction.S_REF)
                        {
                            if (v1.DataType == DataType.Struct || v1.DataType == DataType.RefStruct)
                            {
                                var newPushExpression = new Expression(Instruction.PUSH, -1, v1.StructType);
                                assignmentExpression = new Expression(Instruction.SR_ASSIGN, -1, e1, e2, newPushExpression);
                            }
                            else
                            {
                                if (v1.DataType.IsString())
                                {
                                    e1.ExpressionType = Instruction.REF;
                                }
                                else
                                {
                                    e1.ExpressionType = Instruction.AssignmentRef;
                                }
                                assignmentExpression = new Expression(Instruction.ASSIGN, -1, e1, e2);
                                if (v1.DataType.IsFloat())
                                {
                                    v2 = e2.Variable;
                                    assignmentExpression.ExpressionType = Instruction.F_ASSIGN;
                                    if (v2.DataType.IsInteger() || v2.DataType == DataType.Bool)
                                    {
                                        assignmentExpression.Arg2 = new Expression(Instruction.ITOF, -1, assignmentExpression.Arg2);
                                    }
                                }
                                else if (v1.DataType.IsString())
                                {
                                    assignmentExpression.ExpressionType = Instruction.S_ASSIGN;
                                }
                                else if (v1.DataType == DataType.Lint || v1.DataType == DataType.RefLint)
                                {
                                    assignmentExpression.ExpressionType = Instruction.LI_ASSIGN;
                                }
                            }
                        }
                        else if (e1.ExpressionType == Instruction.S_REF)
                        {
                            //if (!(v1.Parent is Struct))
                            //{
                            //    e1.ExpressionType = Instruction.AssignmentRef;
                            //}
                            //else
                            //{
                            //    e1.ExpressionType = Instruction.REF;
                            //}
                            assignmentExpression = new Expression(Instruction.S_ASSIGN, -1, e1, e2);
                        }
                        else if (e1.ExpressionType == Instruction.SR_REF)
                        {
                            //verify me!
                            var newPushExpression = new Expression(Instruction.PUSH, -1, e1.Value);
                            assignmentExpression = new Expression(Instruction.SR_ASSIGN, -1, e1, e2, newPushExpression);
                        }
                        else if (e1.ExpressionType == Instruction.C_REF)
                        {
                            assignmentExpression = e1;
                            assignmentExpression.ExpressionType = Instruction.C_ASSIGN;
                            assignmentExpression.Arg3 = e2;
                        }
                        else if (e1.ExpressionType == Instruction.AssignmentRef)
                        {
                            if (e2.ExpressionType == Instruction.AssignmentRef && v1.DataType.IsNonPrimitiveRefType() && !v2.DataType.IsRef())
                            {
                                assignmentExpression = new Expression(Instruction.RefAssignment, -1, e1, e2);
                            }
                            else
                            {

                            }
                        }
                        else if (e1.ExpressionType == Instruction.SR_REFREF)
                        {
                            assignmentExpression = new Expression(Instruction.RefAssignment, -1, e1, e2);
                        }
                        else if (e1.ExpressionType == Instruction.INC || e1.ExpressionType == Instruction.DEC || e1.ExpressionType == Instruction.PreDecrement || e1.ExpressionType == Instruction.PreIncrement || e1.ExpressionType == Instruction.LI_INC || e1.ExpressionType == Instruction.LI_DEC)
                        {
                            //Assigning to Inc/Dec (done by Way2)
                            e1 = new Expression(Instruction.AssignmentRef, -1, e1);
                            assignmentExpression = new Expression(Instruction.ASSIGN, -1, e1, e2);
                            v1 = e1.Arg1.Arg1.Variable;
                            if (v1.DataType == DataType.Lint)
                            {
                                assignmentExpression.ExpressionType = Instruction.LI_ASSIGN;
                            }
                            else if (v1.DataType == DataType.Float)
                            {
                                assignmentExpression.ExpressionType = Instruction.F_ASSIGN;
                            }
                        }
                        else
                        {

                        }

                        if (assignmentExpression.ExpressionType == Instruction.ASSIGN && assignmentExpression.Arg1.ExpressionType == Instruction.AssignmentRef && assignmentExpression.Arg1.Arg1.ExpressionType == Instruction.SR_REF)
                        {
                            assignmentExpression.Arg1.Arg1.ExpressionType = Instruction.REF;
                        }

                        if (assignmentExpression.ExpressionType == Instruction.SR_ASSIGN && v1.DataType == DataType.Struct && ((v2.DataType == DataType.RefStruct && e2.ExpressionType == Instruction.ASSIGN) || e2.ExpressionType == Instruction.PUSHSTRUCTPAGE))
                        {
                            if (ainFile.TargetVMVersion > 216)
                            {
                                assignmentExpression.Arg2 = new Expression(Instruction.SR_REF2, -1, assignmentExpression.Arg2, v2.StructType);
                            }
                            //if (ainFile.TargetVMVersion <= 216)
                            //{
                            //    RaiseError("Cannot assign 'this' or 'returned ref structs from function calls' to another struct in VM version <= 216", token);
                            //}
                        }

                        return assignmentExpression;
                    }
                    break;
                case TokenType.Number:
                    {
                        if (token.Token.IsFloat())
                        {
                            return new Expression(Instruction.F_PUSH, -1, token.Token.ToFloat().FloatToInt());
                        }
                        else
                        {
                            return new Expression(Instruction.PUSH, -1, token.Token.ToInt());
                        }
                    }
                    break;
                case TokenType.If:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0); //expression
                        var t2 = token.Subexpressions.GetOrNull(1); //ifbody
                        var t3 = token.Subexpressions.GetOrNull(2); //else

                        var e1 = GenerateExpression(t1);
                        var e2 = GenerateExpression(t2);
                        var e3 = GenerateExpression(t3);

                        return new Expression(Instruction.IfElse, -1, e1, e2, e3);
                    }
                    break;
                case TokenType.Break:
                    var breakExpression = new Expression(Instruction.Break, -1);
                    if (breakScope != null)
                    {
                        DestroyLocalVariables(breakScope, true, ref breakExpression);
                    }
                    return breakExpression;
                    break;
                case TokenType.Continue:
                    var continueExpression = new Expression(Instruction.Continue, -1);
                    if (continueScope != null)
                    {
                        DestroyLocalVariables(continueScope, true, ref continueExpression);
                    }
                    return continueExpression;
                    break;
                case TokenType.Return:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var e1 = GenerateExpression(t1);

                        if (e1 != null)
                        {
                            var v1 = e1.Variable;
                            var function = currentFunction;
                            if (v1 != null)
                            {
                                if (function.DataType.IsFloat() && v1.DataType.IsInteger())
                                {
                                    e1 = new Expression(Instruction.ITOF, -1, e1);
                                }
                                else if (function.DataType.IsInteger() && v1.DataType.IsFloat())
                                {
                                    e1 = new Expression(Instruction.FTOI, -1, e1);
                                }
                                else if (function.DataType == DataType.Bool && v1.DataType.IsInteger())
                                {
                                    e1 = new Expression(Instruction.ITOB, -1, e1);
                                }
                                else if (function.DataType == DataType.Bool && v1.DataType == DataType.Bool && v1.Canonicalize() == null)
                                {
                                    e1 = new Expression(Instruction.ITOB, -1, e1);
                                }
                                else if (function.DataType == DataType.Lint && v1.DataType == DataType.Int)
                                {
                                    e1 = new Expression(Instruction.ITOLI, -1, e1);
                                }
                                else if (function.DataType.IsPrimitiveRefType())
                                {
                                    if (e1.ExpressionType == Instruction.REF)
                                    {
                                        e1.ExpressionType = Instruction.AssignmentRef;
                                    }
                                    else if (e1.ExpressionType == Instruction.CALLFUNC && e1.Value == 0)
                                    {
                                        e1 = new Expression(Instruction.AssignmentRef, -1, new Expression(Instruction.PUSH, -1, -1), new Expression(Instruction.PUSH, -1, 0));
                                        v1 = new Variable("NULL", function.DataType, function.StructType, 0);
                                    }
                                }
                                else if (function.DataType.IsRef())
                                {
                                    if (e1.ExpressionType == Instruction.CALLFUNC && e1.Value == 0)
                                    {
                                        e1 = new Expression(Instruction.PUSH, -1, -1);
                                        v1 = new Variable("NULL", function.DataType, function.StructType, 0);
                                    }
                                }
                                else if (function.DataType.IsFuncType())
                                {
                                    if (e1.ExpressionType == Instruction.CALLFUNC && e1.Value == 0)
                                    {
                                        e1.ExpressionType = Instruction.PUSH;
                                        v1 = new Variable("NULL", function.DataType, function.StructType, 0);
                                    }
                                }
                                else if (function.DataType.IsDelegate())
                                {
                                    if (e1.ExpressionType == Instruction.CALLFUNC && e1.Value == 0)
                                    {
                                        e1.ExpressionType = Instruction.DG_NEW;
                                        v1 = new Variable("NULL", function.DataType, function.StructType, 0);
                                    }
                                    else if (e1.ExpressionType == Instruction.REF)
                                    {
                                        e1 = new Expression(Instruction.DG_COPY, -1, e1);
                                    }
                                    else if (e1.ExpressionType.IsFunctionCall())
                                    {
                                        //do nothing
                                    }
                                    else
                                    {

                                    }
                                }
                                else if (function.DataType == DataType.Struct && (e1.ExpressionType == Instruction.PUSHSTRUCTPAGE || (e1.ExpressionType == Instruction.ASSIGN && v1.DataType == DataType.RefStruct)))
                                {
                                    if (ainFile.TargetVMVersion > 216)
                                    {
                                        e1 = new Expression(Instruction.SR_REF2, -1, e1, v1.StructType);
                                    }
                                    //if (ainFile.TargetVMVersion <= 216)
                                    //{
                                    //    RaiseError("Cannot return 'this' or 'returned ref structs from function calls' in VM version <= 216", token);
                                    //}
                                }
                                if (v1.DataType == DataType.Void && function.DataType != DataType.Void)
                                {
                                    RaiseError("Cannot return a void expression if the function has a return type", token);
                                    return null;
                                }
                                else if (function.DataType == DataType.Void && v1.DataType != DataType.Void)
                                {
                                    RaiseError("Cannot return a value if the function has a void return type", token);
                                    return null;
                                }
                            }
                        }

                        return new Expression(Instruction.RETURN, -1, e1);
                    }
                    break;

                case TokenType.StringLiteral:
                    {
                        return new Expression(Instruction.S_PUSH, -1, Strings.Add(token.Token.Value));
                    }
                case TokenType.CharLiteral:
                    {
                        int charValue = token.Token.Value[0];
                        if (charValue < 128)
                        {
                            return new Expression(Instruction.PUSH, -1, charValue);
                        }
                        else
                        {
                            byte[] bytes = Extensions.BinaryEncoding.GetBytes(token.Token.Value);
                            if (bytes.Length >= 2)
                            {
                                charValue = bytes[0] + (bytes[1] << 8);
                            }
                            else
                            {
                                charValue = bytes[0];
                            }
                            return new Expression(Instruction.PUSH, -1, charValue);
                        }
                    }
                    break;
                case TokenType.Switch:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var t2 = token.Subexpressions.GetOrNull(1);

                        var e1 = GenerateExpression(t1);
                        var e2 = GenerateBlockExpression(t2, true, false);

                        return new Expression(Instruction.SWITCH, -1, e1, e2);
                    }
                    break;
                case TokenType.Case:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        if (t1.TokenType == TokenType.StringLiteral)
                        {
                            return new Expression(Instruction.SwitchCase, -1, Strings.Add(t1.Token.Value));
                        }
                        else
                        {
                            var e1 = GenerateExpression(t1);
                            //TODO: evaluate const expressions
                            if (e1.ExpressionType != Instruction.PUSH)
                            {
                                if (e1.ExpressionType == Instruction.INV && e1.Arg1.ExpressionType == Instruction.PUSH)
                                {
                                    return new Expression(Instruction.SwitchCase, -1, -e1.Arg1.Value);
                                }
                                else
                                {

                                }
                            }
                            return new Expression(Instruction.SwitchCase, -1, e1.Value);
                        }
                    }
                    break;
                case TokenType.Jump:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var e1 = GenerateExpression(t1);
                        if (e1 != null)
                        {
                            var f1 = e1.Variable as Function;
                            if (f1 != null && f1.IsLabel != 0)
                            {
                                var e = new Expression(Instruction.S_PUSH, -1, Strings.Add(f1.Name));
                                var e2 = new Expression(Instruction.CALLONJUMP, -1, e);
                                var e3 = new Expression(Instruction.SJUMP, -1, e2);
                                return e3;
                            }
                            else
                            {

                            }
                        }
                        else
                        {

                        }
                        return null;
                    }
                    break;
                case TokenType.Default:
                    {
                        return new Expression(Instruction.DefaultSwitchCase, -1);
                    }
                    break;
                /*
            case TokenType.PreIncrement:
            case TokenType.PreDecrement:
            case TokenType.PostIncrement:
            case TokenType.PostDecrement:
                {
                    throw new NotImplementedException();
                }
                */
                case TokenType.While:
                    {
                        var tExpression = token.Subexpressions.GetOrNull(0);
                        var tBody = token.Subexpressions.GetOrNull(1);

                        var eExpression = GenerateExpression(tExpression);

                        var eBody = GenerateBlockExpression(tBody, true, true);

                        var whileExpression = new Expression(Instruction.WhileLoop, -1, eExpression, eBody);
                        return whileExpression;
                    }
                    break;
                case TokenType.Do:
                    {
                        var tExpression = token.Subexpressions.GetOrNull(0);
                        var tBody = token.Subexpressions.GetOrNull(1);

                        var eExpression = GenerateExpression(tExpression);

                        var eBody = GenerateBlockExpression(tBody, true, true);

                        var whileExpression = new Expression(Instruction.DoWhileLoop, -1, eExpression, eBody);
                        return whileExpression;
                    }
                    break;
                case TokenType.AddressOf:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        if (t1.TokenType != TokenType.Dot)
                        {
                            var e1 = GenerateExpression(t1);
                            if (e1 != null && (e1.ExpressionType == Instruction.CALLFUNC || e1.ExpressionType == Instruction.CALLMETHOD))
                            {
                                if (e1.ExpressionType == Instruction.CALLMETHOD && t1.TokenType == TokenType.Identifier)
                                {
                                    //is there also a global function that matches?  Pick that instead.
                                    if (Functions.Contains(t1.Token.Value))
                                    {
                                        return new Expression(Instruction.PUSH, -1, Functions.Get(t1.Token.Value).Index);
                                    }
                                }

                                if (e1.ExpressionType == Instruction.CALLMETHOD && ainFile.Delegates.Count > 0)
                                {
                                    var e1_arg1 = e1.Arg1;
                                    e1.Args.Clear();
                                    e1.Arg1 = e1_arg1;
                                    e1.Arg2 = new Expression(Instruction.PUSH, -1, e1.Value);
                                    e1.ExpressionType = Instruction.DG_NEW_FROM_METHOD;
                                    return e1;
                                }

                                e1.ExpressionType = Instruction.PUSH;
                                return e1;
                            }
                            else
                            {

                            }
                        }
                        else
                        {
                            var structTypeToken = t1.Subexpressions.GetOrNull(0);
                            var structMemberToken = t1.Subexpressions.GetOrNull(1);

                            var structInfo = localScope.GetOrNull(structTypeToken.Token.Value) as Struct;
                            if (structInfo != null)
                            {
                                foreach (var member in structInfo.Members)
                                {
                                    if (member.Name == structMemberToken.Token.Value)
                                    {
                                        return new Expression(Instruction.PUSH, -1, member.Index);
                                    }
                                }
                            }

                            var e = GenerateDot(t1);
                            if (e != null)
                            {
                                if (e.ExpressionType == Instruction.CALLMETHOD && ainFile.Delegates.Count > 0)
                                {
                                    var e1_arg1 = e.Arg1;
                                    e.Args.Clear();
                                    e.Arg1 = e1_arg1;
                                    e.Arg2 = new Expression(Instruction.PUSH, -1, e.Value);
                                    e.ExpressionType = Instruction.DG_NEW_FROM_METHOD;
                                    return e;
                                }

                                var v = e.Variable;
                                if (v != null)
                                {
                                    v = v.Canonicalize();
                                    if (v != null)
                                    {
                                        if (v is Function)
                                        {
                                            return new Expression(Instruction.PUSH, -1, v.Index);
                                        }
                                        if (v is Variable && v.Parent is Struct)
                                        {
                                            return new Expression(Instruction.PUSH, -1, v.Index);
                                        }
                                    }
                                }
                            }


                            //check for struct member

                        }

                    }
                    break;
                case TokenType.Message:
                    {
                        return new Expression(Instruction.MSG, -1, Messages.Add(token.Token.Value));
                    }
                    break;
                case TokenType.ArrayIndex:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var t2 = token.Subexpressions.GetOrNull(1);
                        var e1 = GenerateExpression(t1);
                        var e2 = GenerateExpression(t2);

                        var v1 = (e1 != null) ? e1.Variable : null;
                        var v2 = (e2 != null) ? e2.Variable : null;

                        if (v1 == null || v2 == null)
                        {
                            RaiseError2("Could not evalute array index", token);
                            return null;
                        }

                        if (v2.DataType.IsInteger())
                        {
                            if (v1.DataType.IsArray())
                            {
                                if (e1.ExpressionType == Instruction.AssignmentRef)
                                {
                                    e1.ExpressionType = Instruction.REF;
                                }
                                var e = new Expression(Instruction.REF, -1, e1, e2);
                                if (v1.DataType.GetTypeOfArrayElement() == DataType.String)
                                {
                                    e.ExpressionType = Instruction.S_REF;
                                }
                                if (v1.DataType.GetTypeOfArrayElement().IsStruct())
                                {
                                    e.ExpressionType = Instruction.SR_REF;
                                    e.Value = v1.StructType;
                                }
                                //GetRefType(e.Variable, ref e);
                                return e;
                            }
                            else if (v1.DataType.IsString())
                            {
                                var e = new Expression(Instruction.C_REF, -1, e1, e2);
                                return e;
                            }
                            else
                            {
                                RaiseError2("Could not evaluate left side of array index", t1);
                                //error?
                            }
                        }
                        else
                        {
                            RaiseError2("Array index is not an integer", t2);
                            //error?
                        }
                        return null;

                    }
                    break;
                case TokenType.Comma:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var t2 = token.Subexpressions.GetOrNull(1);
                        var e1 = GenerateExpression(t1);
                        var e2 = GenerateExpression(t2);

                        return new Expression(Instruction.Comma, -1, e1, e2);

                    }
                case TokenType.QuestionMark:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var t2 = token.Subexpressions.GetOrNull(1);
                        if (t2.TokenType != TokenType.Colon)
                        {
                            RaiseError("Question mark operator without colon", token);
                            //error - question mark without colon
                            return null;
                        }
                        var t21 = t2.Subexpressions.GetOrNull(0);
                        var t22 = t2.Subexpressions.GetOrNull(1);
                        var e1 = GenerateExpression(t1);
                        var e21 = GenerateExpression(t21);
                        var e22 = GenerateExpression(t22);

                        var v1 = e1.Variable;
                        var v21 = e21.Variable;
                        var v22 = e22.Variable;

                        if (v1.DataType.IsFloat())
                        {
                            e1 = new Expression(Instruction.FTOI, -1, e1);
                        }
                        if (v21.DataType.IsFloat() && !v22.DataType.IsFloat())
                        {
                            e22 = new Expression(Instruction.ITOF, -1, e22);
                        }
                        if (v22.DataType.IsFloat() && !v21.DataType.IsFloat())
                        {
                            e21 = new Expression(Instruction.ITOF, -1, e21);
                        }

                        return new Expression(Instruction.IfElse, -1, e1, e21, e22);
                    }
                    break;
                case TokenType.Assert:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var e1 = GenerateExpression(t1);

                        var str = e1.ToString(false);
                        int strNumber = Strings.Add(str);

                        string fileName = t1.FileName ?? "";
                        fileName = GetFilename(fileName);
                        int fileNameNumber = Strings.Add(fileName);

                        int lineNumber = t1.Row + 1;

                        return new Expression(Instruction.ASSERT, -1, e1, new Expression(Instruction.S_PUSH, -1, strNumber), new Expression(Instruction.S_PUSH, -1, fileNameNumber), new Expression(Instruction.PUSH, -1, lineNumber));
                    }
                    break;
                case TokenType.ReferenceAssign:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var e1 = GenerateExpression(t1);

                        if (e1 == null)
                        {
                            RaiseError2("Unable to generate left side for reference assign", t1);
                            return null;
                        }

                        if ((e1.ExpressionType == Instruction.REF && e1.Arg1.ExpressionType == Instruction.REFREF) || (e1.ExpressionType == Instruction.AssignmentRef && e1.Arg1.ExpressionType == Instruction.SR_REFREF))
                        {
                            e1 = e1.Arg1;
                        }
                        e1.ExpressionType = Instruction.AssignmentRef;
                        var v1 = e1.Variable;
                        if (e1.IsReference() && !v1.DataType.IsPrimitiveRefType())
                        {

                        }
                        //if (e1.ExpressionType == Instruction.SR_REFREF

                        var t2 = token.Subexpressions.GetOrNull(1);
                        var e2 = GenerateExpression(t2);

                        if (e2 == null)
                        {
                            RaiseError2("Unable to generate right side for reference assign", t2);
                            return null;
                        }

                        if (e2.ExpressionType == Instruction.CALLFUNC && e2.Value == 0)
                        {
                            e2.ExpressionType = Instruction.PUSH;
                            e2.Value = -1;
                            //no 'v2' to fix here
                        }
                        if (e2.ExpressionType == Instruction.REF)
                        {
                            bool dontChange = false;
                            if (e2.Arg1.ExpressionType == Instruction.REF)
                            {
                                var _v2 = e2.Arg1.Variable.Canonicalize();
                                if (_v2 != null && _v2.DataType.IsArray())
                                {
                                    var d = _v2.DataType.GetTypeOfArrayElement();
                                    if (!d.IsPrimitiveType())
                                    {
                                        dontChange = true;
                                    }
                                }
                            }
                            if (!dontChange)
                            {
                                e2.ExpressionType = Instruction.AssignmentRef;
                            }
                        }
                        else if (e2.ExpressionType == Instruction.AssignmentRef && v1.DataType.IsNonPrimitiveRefType())
                        {
                            var _v2 = e2.Variable;
                            e2.ExpressionType = Instruction.REF;
                            //if (_v2 != null && _v2.DataType.IsRef())
                            //{
                            //    e2.ExpressionType = Instruction.REF;
                            //}
                            //else
                            //{

                            //}
                        }
                        var v2 = e2.Variable;
                        if (v2 is IFunction && !(v2 is Struct)) //???
                        {
                            //make a dummy
                            e2 = CreateDummyVariableExpression(v1, e2);
                        }
                        else if (e2.ExpressionType == Instruction.NEW)
                        {
                            e2 = CreateDummyVariableExpression(v1, e2);
                        }

                        if (v1.DataType == DataType.RefString && v2.DataType == DataType.String && e2.ExpressionType == Instruction.S_REF && e1.ExpressionType == Instruction.AssignmentRef && v1.Parent != null)
                        {
                            //this was a recent change, verify that this is okay!  (Yep, it is)
                            e2.ExpressionType = Instruction.REF;
                        }

                        Expression e;
                        if (v1.DataType.IsPrimitiveRefType())
                        {
                            e = new Expression(Instruction.RefIntAssignment, -1, e1, e2);
                        }
                        else if (v1.DataType.IsPrimitiveType())
                        {
                            RaiseError("Cannot reference assign to variable " + v1.Name + " which is type " + v1.GetDataTypeName(), token);
                            return null;
                        }
                        else
                        {
                            e = new Expression(Instruction.RefAssignment, -1, e1, e2);
                        }
                        return e;
                    }
                    break;
                case TokenType.ReferenceSwap:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var t2 = token.Subexpressions.GetOrNull(1);

                        var e1 = GenerateExpression(t1);
                        if (e1.ExpressionType != Instruction.AssignmentRef)
                        {
                            if (e1.Args.Count == 2)
                            {
                                e1.ExpressionType = Instruction.AssignmentRef;
                            }
                            else
                            {

                            }
                        }
                        else
                        {

                        }
                        var e2 = GenerateExpression(t2);
                        if (e2.ExpressionType != Instruction.AssignmentRef)
                        {
                            if (e2.Args.Count == 2)
                            {
                                e2.ExpressionType = Instruction.AssignmentRef;
                            }
                            else
                            {

                            }
                        }
                        else
                        {

                        }

                        var v1 = e1.Variable;
                        Expression push = new Expression(Instruction.PUSH, -1, (int)v1.DataType);

                        var e = new Expression(Instruction.OBJSWAP, -1, e1, e2, push);
                        return e;
                    }
                    break;
                case TokenType.ReferenceEqualTo:
                case TokenType.ReferenceNotEqualTo:
                    {
                        var rEquale = Instruction.R_EQUALE;
                        var equale = Instruction.EQUALE;
                        if (tokenType == TokenType.ReferenceNotEqualTo)
                        {
                            rEquale = Instruction.R_NOTE;
                            equale = Instruction.NOTE;
                        }

                        var t1 = token.Subexpressions.GetOrNull(0);
                        var t2 = token.Subexpressions.GetOrNull(1);

                        bool eitherNull = false;

                        var e1 = GenerateExpression(t1) ?? Expression.Empty;
                        var v1 = e1.Variable;
                        IVariable v1Canon = null;
                        if (e1.ExpressionType == Instruction.REF)
                        {
                            e1.ExpressionType = Instruction.AssignmentRef;
                            v1 = e1.Variable;
                        }
                        else if (e1.ExpressionType == Instruction.SR_REF)
                        {
                            //???
                            e1.ExpressionType = Instruction.AssignmentRef;
                            v1 = e1.Variable;
                        }
                        else if (e1.ExpressionType == Instruction.CALLFUNC && e1.Value == 0)
                        {
                            eitherNull = true;
                            e1 = new Expression(Instruction.PUSH, -1, -1);
                            //v1 = new Variable("NULL", DataType.Int, -1, 0);  //not needed
                        }
                        else
                        {

                        }
                        if (v1 != null) { v1Canon = v1.Canonicalize(); }
                        var e2 = GenerateExpression(t2) ?? Expression.Empty;
                        var v2 = e2.Variable;
                        IVariable v2Canon = null;
                        if (e2.ExpressionType == Instruction.REF)
                        {
                            e2.ExpressionType = Instruction.AssignmentRef;
                            v2 = e2.Variable;
                        }
                        else if (e2.ExpressionType == Instruction.SR_REF) // not sure about second part
                        {
                            //???
                            e2.ExpressionType = Instruction.AssignmentRef;
                            v2 = e2.Variable;
                        }
                        else if (e2.ExpressionType == Instruction.CALLFUNC && e2.Value == 0)
                        {
                            eitherNull = true;
                            e2 = new Expression(Instruction.PUSH, -1, -1);
                            //v2 = new Variable("NULL", DataType.Int, -1, 0);  //not needed
                        }
                        else
                        {

                        }
                        if (v2 != null) { v2Canon = v2.Canonicalize(); }
                        if (e2.ExpressionType == Instruction.INV && e2.Arg1.ExpressionType == Instruction.PUSH && e2.Arg1.Value == 1)
                        {
                            eitherNull = true;
                            e2 = new Expression(Instruction.PUSH, -1, -1);
                        }
                        if (eitherNull)
                        {
                            if (e1.ExpressionType == Instruction.AssignmentRef && !(v1Canon != null && v1Canon.DataType.IsPrimitiveRefType())) { e1.ExpressionType = Instruction.REF; }
                            if (e2.ExpressionType == Instruction.AssignmentRef && !(v2Canon != null && v2Canon.DataType.IsPrimitiveRefType())) { e2.ExpressionType = Instruction.REF; }
                            if ((v1 != null && e1.ExpressionType != Instruction.PUSH && v1.DataType.IsPrimitiveType()) || (v2 != null && e2.ExpressionType != Instruction.PUSH && v2.DataType.IsPrimitiveType()))
                            {
                                if (e1.ExpressionType == Instruction.PUSH)
                                {
                                    e1 = new Expression(Instruction.PUSH, -1, e1, 0);
                                }
                                if (e2.ExpressionType == Instruction.PUSH)
                                {
                                    e2 = new Expression(Instruction.PUSH, -1, e2, 0);
                                }
                                return new Expression(rEquale, -1, e1, e2);
                            }
                            if (v1Canon != null && v1Canon.DataType.IsNonPrimitiveRefType() && (e1.ExpressionType == Instruction.REF || e1.ExpressionType == Instruction.S_REF))
                            {
                                MakeShRef(e1);
                            }
                            if (v2Canon != null && v2Canon.DataType.IsNonPrimitiveRefType() && (e2.ExpressionType == Instruction.REF || e2.ExpressionType == Instruction.S_REF))
                            {
                                MakeShRef(e2);
                            }

                            var e = new Expression(equale, -1, e1, e2);
                            return e;
                        }
                        else if ((v1.DataType == DataType.RefStruct && v2.DataType == DataType.RefStruct) || (v1.DataType.IsRefArray() && v2.DataType.IsRefArray()) || (v1.DataType == DataType.RefString && v2.DataType == DataType.RefString))
                        {
                            if (e1.ExpressionType == Instruction.AssignmentRef) e1.ExpressionType = Instruction.REF;
                            if (e2.ExpressionType == Instruction.AssignmentRef) e2.ExpressionType = Instruction.REF;
                            MakeShRef(e1);
                            MakeShRef(e2);
                            var e = new Expression(equale, -1, e1, e2);
                            return e;
                        }
                        else if ((v1.DataType == DataType.RefStruct && v2.DataType == DataType.Struct) || (v1.DataType.IsRefArray() && v2.DataType.IsNonRefArray()) || (v1.DataType == DataType.RefString && v2.DataType == DataType.String))
                        {
                            //evenicle compares ref array against array, and ref struct against struct, and it uses this code
                            if (e1.ExpressionType == Instruction.AssignmentRef) e1.ExpressionType = Instruction.REF;
                            if (e2.ExpressionType == Instruction.AssignmentRef) e2.ExpressionType = Instruction.REF;
                            MakeShRef(e1);
                            MakeShRef(e2);
                            var e = new Expression(equale, -1, e1, e2);
                            return e;
                        }
                        else if (e1.ExpressionType == Instruction.PUSHSTRUCTPAGE || e2.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                        {
                            if (e1.ExpressionType == Instruction.AssignmentRef) e1.ExpressionType = Instruction.REF;
                            if (e2.ExpressionType == Instruction.AssignmentRef) e2.ExpressionType = Instruction.REF;
                            MakeShRef(e1);
                            MakeShRef(e2);
                            var e = new Expression(equale, -1, e1, e2);
                            return e;
                        }
                        else
                        {
                            var e = new Expression(rEquale, -1, e1, e2);
                            return e;
                        }
                    }
                    break;
                case TokenType.Label:
                case TokenType.Goto:
                    {
                        int labelNumber;
                        string labelName = token.Subexpressions[0].Token.Value;
                        if (this.LabelNameToIndex.ContainsKey(labelName))
                        {
                            labelNumber = LabelNameToIndex[labelName];
                        }
                        else
                        {
                            labelNumber = LabelsIndexToName.Count;
                            LabelNameToIndex[labelName] = labelNumber;
                            LabelsIndexToName.Add(labelName);
                        }
                        if (tokenType == TokenType.Label)
                        {
                            if (!this.UsedLabelNames.Add(labelName))
                            {
                                RaiseError("Duplicate label: " + labelName, token);

                                //error - duplicate label
                            }
                        }

                        var e = new Expression(tokenType == TokenType.Label ? Instruction.Label : Instruction.Goto, -1, labelNumber);
                        if (tokenType == TokenType.Goto)
                        {
                            this.GotoScopes[e] = localScope;
                        }
                        else
                        {
                            this.LabelScopes[labelNumber] = localScope;
                        }
                        return e;
                    }
                    break;
                case TokenType.Jumps:
                    {
                        var t1 = token.Subexpressions.GetOrNull(0);
                        var e1 = GenerateExpression(t1);
                        if (e1 != null)
                        {
                            var v1 = e1.Variable;
                            if (v1 != null && v1.DataType.IsString())
                            {
                                var e2 = new Expression(Instruction.CALLONJUMP, -1, e1);
                                var e3 = new Expression(Instruction.SJUMP, -1, e2);
                                return e3;
                            }
                        }
                        //error
                        return null;
                    }
                    break;
                case TokenType.New:
                    {
                        string structName = token.Subexpressions[0].Token.Value;
                        if (Structs.Contains(structName))
                        {
                            var structInfo = Structs.Get(structName);
                            var newExpression = new Expression(Instruction.NEW, -1, new Expression(Instruction.PUSH, -1, structInfo.Index));
                            return newExpression;
                        }
                        return null;
                    }
                    break;
                default:
                    {
                        if (IntegerAssignmentOperators.ContainsKey(tokenType))
                        {
                            var t1 = token.Subexpressions.GetOrNull(0);
                            var t2 = token.Subexpressions.GetOrNull(1);

                            var e1 = GenerateExpression(t1);
                            var e2 = GenerateExpression(t2);

                            if (e1 == null || e2 == null)
                            {
                                return null;
                            }

                            var v1 = e1.Variable;
                            var v2 = e2.Variable;

                            var instruction = IntegerAssignmentOperators[tokenType];

                            if (v2 != null && v1 != null)
                            {
                                if (v1.DataType.IsFloat() && v2.DataType.IsInteger())
                                {
                                    e2 = new Expression(Instruction.ITOF, -1, e2);
                                    v2 = e2.Variable;
                                }
                                else if (v1.DataType.IsInteger() && v2.DataType.IsFloat())
                                {
                                    //new: for multiplication and division operators, rewrite as
                                    //e1 = int(float(e1) / e2)    or     e1 = int(float(e1) * e2)

                                    if (token.TokenType == TokenType.DivideAssign || token.TokenType == TokenType.MultiplyAssign)
                                    {
                                        var e1Copy = e1.Clone();
                                        e1Copy = new Expression(Instruction.ITOF, -1, e1Copy);
                                        if (e1.ExpressionType == Instruction.REF)
                                        {
                                            e1.ExpressionType = Instruction.AssignmentRef;
                                        }
                                        var newExpression = new Expression(Instruction.F_MUL, -1, e1Copy, e2);
                                        if (token.TokenType == TokenType.DivideAssign)
                                        {
                                            newExpression.ExpressionType = Instruction.F_DIV;
                                        }
                                        newExpression = new Expression(Instruction.FTOI, -1, newExpression);
                                        return new Expression(Instruction.ASSIGN, -1, e1, newExpression);
                                    }
                                    else
                                    {
                                        e2 = new Expression(Instruction.FTOI, -1, e2);
                                        v2 = e2.Variable;
                                    }
                                }
                                else if (v1.DataType == DataType.Bool && v2.DataType.IsInteger())
                                {
                                    e2 = new Expression(Instruction.ITOB, -1, e2);
                                    v2 = e2.Variable;
                                }
                            }

                            if (v1.DataType != DataType.Int || v2.DataType != DataType.Int)
                            {
                                if (v1.DataType.IsString() && v2.DataType.IsString())
                                {
                                    instruction = StringInstructions[instruction];
                                }
                                else if (v1.DataType.IsFloat() && v2.DataType.IsFloat())
                                {
                                    instruction = FloatInstructions[instruction];
                                }
                                else if ((v1.DataType == DataType.Lint || v1.DataType == DataType.RefLint))
                                {
                                    if (LintInstructions.ContainsKey(instruction))
                                    {
                                        if (v2.DataType == DataType.Int || v2.DataType == DataType.RefInt)
                                        {
                                            e2 = new Expression(Instruction.ITOLI, -1, e2);
                                            v2 = e2.Variable;
                                        }
                                        instruction = LintInstructions[instruction];
                                    }
                                }
                                else if (v1.DataType.IsDelegate())
                                {
                                    if (DelegateInstructions.ContainsKey(instruction))
                                    {
                                        instruction = DelegateInstructions[instruction];
                                        if (e2.ExpressionType == Instruction.DG_NEW_FROM_METHOD)
                                        {

                                        }
                                        else if (v2.DataType.IsDelegate())
                                        {
                                            e2 = new Expression(Instruction.DG_COPY, -1, e2);
                                        }
                                        else
                                        {

                                        }
                                        if (e1.ExpressionType == Instruction.AssignmentRef)
                                        {
                                            e1.ExpressionType = Instruction.REF;
                                        }
                                        return new Expression(instruction, -1, e1, e2);
                                    }
                                    else
                                    {
                                        RaiseError2("Delegates do not support assignment operator", token);
                                        return null;
                                    }
                                }
                            }

                            if (e1.ExpressionType == Instruction.REF || e1.ExpressionType == Instruction.S_REF)
                            {
                                e1.ExpressionType = Instruction.AssignmentRef;
                            }
                            else
                            {

                            }

                            var e = new Expression(instruction, -1, e1, e2);
                            return e;
                        }
                        else if (IntegerBinaryOperators.ContainsKey(tokenType))
                        {
                            var e = GenerateBinaryExpression(token);
                            return e;
                        }
                        else if (IntegerUnaryOperators.ContainsKey(tokenType))
                        {
                            var t1 = token.Subexpressions.GetOrNull(0);
                            var e1 = GenerateExpression(t1);
                            if (e1 == null)
                            {
                                RaiseError("Symbol not found: " + t1.ToStringReal(), t1);
                                return null;
                            }


                            var v1 = e1.Variable;
                            switch (v1.DataType)
                            {
                                case DataType.Int:
                                case DataType.RefInt:
                                case DataType.Lint:
                                case DataType.RefLint:
                                case DataType.Bool:
                                case DataType.RefBool:
                                case DataType.Float:
                                case DataType.RefFloat:
                                    break;
                                default:
                                    RaiseError("Data type " + v1.GetDataTypeName() + " does not support unary operator " + token.Token.Value, token);
                                    return null;
                            }

                            var instruction = IntegerUnaryOperators[tokenType];
                            if (instruction == Instruction.Nop)
                            {
                                return e1;
                            }

                            if (v1.DataType.IsFloat())
                            {
                                if (FloatInstructions.ContainsKey(instruction))
                                {
                                    instruction = FloatInstructions[instruction];
                                }
                                else
                                {
                                    e1 = new Expression(Instruction.FTOI, -1, e1);
                                    v1 = e1.Variable;
                                }
                            }
                            if (v1.DataType == DataType.Lint || v1.DataType == DataType.RefLint)
                            {
                                if (LintInstructions.ContainsKey(instruction))
                                {
                                    instruction = LintInstructions[instruction];
                                }
                            }

                            var e = new Expression(instruction, -1, e1);
                            return e;
                        }
                        else if (IntegerUnaryAssignmentOperators.ContainsKey(tokenType))
                        {
                            var t1 = token.Subexpressions.GetOrNull(0);
                            var e1 = GenerateExpression(t1);
                            var instruction = IntegerUnaryAssignmentOperators[tokenType];

                            var v1 = e1.Variable;
                            if (v1.DataType != DataType.Int)
                            {
                                if (v1.DataType == DataType.Lint || v1.DataType == DataType.RefLint)
                                {
                                    if (LintInstructions.ContainsKey(instruction))
                                    {
                                        instruction = LintInstructions[instruction];
                                    }
                                }
                            }

                            if (e1.ExpressionType == Instruction.REF || e1.ExpressionType == Instruction.S_REF)
                            {
                                e1.ExpressionType = Instruction.AssignmentRef;
                            }
                            else
                            {

                            }
                            var e = new Expression(instruction, -1, e1);
                            return e;
                        }
                        else if (tokenType == TokenType.Statement && token.Subexpressions.Count == 0)
                        {
                            //empty statement - do nothing
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                    break;
            }
            return null;
        }

        private Expression CreateDummyVariableExpression(IVariable dataTypeVariable, Expression e2)
        {
            string functionName = e2.Variable.Name;
            var dummyVariable = CreateDummyVariable(dataTypeVariable, functionName);

            Expression dummyExpression = SymbolToExpression(dummyVariable);
            if (dummyExpression.ExpressionType == Instruction.REF && dummyExpression.Arg1.ExpressionType == Instruction.REFREF)
            {
                dummyExpression = dummyExpression.Arg1;
            }
            dummyExpression.ExpressionType = Instruction.AssignmentRef;
            e2 = new Expression(Instruction.R_ASSIGN, -1, dummyExpression, e2);
            if (dataTypeVariable.DataType == DataType.RefStruct && e2.Variable.DataType == DataType.RefStruct)
            {
                e2.ExpressionType = Instruction.ASSIGN;
            }
            else if (dataTypeVariable.DataType == DataType.RefString && e2.Variable.DataType == DataType.RefString)
            {
                e2.ExpressionType = Instruction.ASSIGN;
            }
            return e2;
        }

        private Variable CreateDummyVariable(IVariable dataTypeVariable, string functionName)
        {
            var dummyVariable = new Variable();
            dummyVariable.SetVariableType(dataTypeVariable);
            if (dummyVariable.DataType == DataType.Struct)
            {
                dummyVariable.DataType = DataType.RefStruct;
            }
            dummyVariable.Name = "<dummy : " + functionName + " : " + this.DummyNumber.ToString() + ">";
            dummyVariable.Parent = currentFunction;
            dummyVariable.Root = ainFile;
            this.DummyNumber++;

            dummyVariable.Index = currentFunction.Parameters.Count;
            currentFunction.Parameters.Add(dummyVariable);
            localScope.Add(dummyVariable.Name, dummyVariable);
            if (dummyVariable.DataType.IsPrimitiveRefType())
            {

                var voidVariable = new Variable();
                voidVariable.Name = "<void>";
                voidVariable.DataType = DataType.Void;
                voidVariable.Parent = currentFunction;
                voidVariable.Root = ainFile;

                voidVariable.Index = currentFunction.Parameters.Count;
                currentFunction.Parameters.Add(voidVariable);
            }
            return dummyVariable;
        }

        private void FixGotos()
        {
            var oldLocalScope = localScope;

            foreach (var pair in GotoScopes)
            {
                var expression = pair.Key;
                var scope = pair.Value;

                int labelNumber = expression.Value;

                var labelScope = LabelScopes.GetOrNull(labelNumber);
                if (labelScope == null)
                {
                    RaiseError("Label not found: " + LabelsIndexToName.GetOrDefault(labelNumber, ""), null);
                }

                var commonScope = VariableDictionaryWithParent.GetCommonParent(scope, labelScope);
                if (commonScope != null)
                {
                    localScope = scope;
                    DestroyLocalVariables(commonScope, false, ref expression);
                }
            }

            localScope = oldLocalScope;
        }


        private void DestroyLocalVariables(VariableDictionaryWithParent baseScope, bool includeBaseScope, ref Expression exitExpression)
        {
            //destory local variables created in "scope" argument or later
            //exitExpression is the break command, replace with a chain of commands

            List<Variable> variablesToDestroy = new List<Variable>();

            for (var scope = localScope; scope != null && scope != baseScope; scope = scope.Parent as VariableDictionaryWithParent)
            {
                var values = scope.Dic.Values;
                foreach (var variable in values.OfType<Variable>())
                {
                    if (variable.DataType.IsArray() || variable.DataType.IsStruct() || (variable.DataType == DataType.RefString && ainFile.Version >= 6) /*|| variable.DataType == DataType.Delegate*/)
                    {
                        variablesToDestroy.Add(variable);
                    }
                }
            }
            if (includeBaseScope)
            {
                var values = baseScope.Dic.Values;
                foreach (var variable in values.OfType<Variable>())
                {
                    if (variable.DataType.IsArray() || variable.DataType.IsStruct() || (variable.DataType == DataType.RefString && ainFile.Version >= 6) /*|| variable.DataType == DataType.Delegate*/)
                    {
                        variablesToDestroy.Add(variable);
                    }
                }
            }

            variablesToDestroy = SortByIndexDescending(variablesToDestroy);

            if (variablesToDestroy.Count > 0)
            {
                List<Expression> deleteStatementsList = new List<Expression>();
                foreach (var variable in variablesToDestroy)
                {
                    if (variable.DataType.IsNonRefArray())
                    {
                        var e1 = SymbolToExpression(variable);
                        var e2 = new Expression(Instruction.A_FREE, -1, e1);
                        deleteStatementsList.Add(e2);
                    }
                    else if (variable.DataType == DataType.Delegate)
                    {
                        deleteStatementsList.Add(new Expression(Instruction.DG_CLEAR, -1,
                            new Expression(Instruction.REF, -1,
                                new Expression(Instruction.PUSHLOCALPAGE, -1),
                                new Expression(Instruction.PUSH, variable.Index))
                            ));
                    }
                    else
                    {
                        var e1 = new Expression(Instruction.SH_LOCALDELETE, -1, variable.Index);
                        deleteStatementsList.Add(e1);
                    }
                }
                //deleteStatementsList.Add(
                var statement = ConvertToStatements(deleteStatementsList);
                Expression.InsertStatementBefore(ref exitExpression, statement);
            }
        }

        private static List<Variable> SortByIndexDescending(List<Variable> variablesToDestroy)
        {
            variablesToDestroy = variablesToDestroy.OrderByDescending(v => v.Index).ToList();
            return variablesToDestroy;
        }

        private Expression GenerateBinaryExpression(TokenExpression token)
        {
            TokenType tokenType = token.TokenType;

            var t1 = token.Subexpressions.GetOrNull(0);
            var t2 = token.Subexpressions.GetOrNull(1);

            var e1 = GenerateExpression(t1);
            var e2 = GenerateExpression(t2);

            if (e1 == null)
            {
                RaiseError2("Unable to evaluate as an expression", t1);
            }
            if (e2 == null)
            {
                RaiseError2("Unable to evaluate as an expression", t2);
            }

            if (e1 == null || e2 == null)
            {
                return null;
            }

            var v1 = e1.Variable;
            var v2 = e2.Variable;

            var instruction = IntegerBinaryOperators[tokenType];
            if (v1.DataType != DataType.Int || v2.DataType != DataType.Int)
            {
                if (!(v1.DataType == DataType.Bool && v2.DataType == DataType.Bool))
                {
                    if (v1.DataType == DataType.String || v2.DataType == DataType.String)
                    {
                        if (StringInstructions.ContainsKey(instruction))
                        {
                            instruction = StringInstructions[instruction];
                        }
                        else
                        {
                            RaiseError2("Operator " + token.Token.Value + " can not be used on strings.", token);
                            //error - compatible instruction not found
                        }
                    }
                    else if (v1.DataType == DataType.Bool && v2.DataType == DataType.Int)
                    {

                    }
                    else if (v1.DataType == DataType.Int && v2.DataType == DataType.Bool)
                    {

                    }
                    else if (v2.DataType == DataType.Void)
                    {
                        if (t2.Token.Value == "NULL")
                        {
                            e2 = new Expression(Instruction.PUSH, -1, 0);
                        }
                    }
                    else if (v1.DataType == DataType.Float && v2.DataType == DataType.Float)
                    {
                        if (FloatInstructions.ContainsKey(instruction))
                        {
                            instruction = FloatInstructions[instruction];
                        }
                        else
                        {
                            e1 = new Expression(Instruction.FTOI, -1, e1);
                            e2 = new Expression(Instruction.FTOI, -1, e2);
                        }
                    }
                    else if (v1.DataType == DataType.Int && v2.DataType == DataType.Float)
                    {
                        if (FloatInstructions.ContainsKey(instruction))
                        {
                            e1 = new Expression(Instruction.ITOF, -1, e1);
                            instruction = FloatInstructions[instruction];
                        }
                        else
                        {
                            e2 = new Expression(Instruction.FTOI, -1, e2);
                        }
                    }
                    else if (v1.DataType == DataType.Float && v2.DataType == DataType.Int)
                    {
                        if (FloatInstructions.ContainsKey(instruction))
                        {
                            e2 = new Expression(Instruction.ITOF, -1, e2);
                            instruction = FloatInstructions[instruction];
                        }
                        else
                        {
                            e1 = new Expression(Instruction.FTOI, -1, e1);
                        }
                    }
                    else if ((v1.DataType == DataType.Lint || v1.DataType == DataType.RefLint) || (v2.DataType == DataType.Lint || v2.DataType == DataType.RefLint))
                    {
                        if (LintInstructions.ContainsKey(instruction))
                        {
                            instruction = LintInstructions[instruction];
                        }
                        else
                        {

                        }
                    }
                    else
                    {

                    }
                }
            }

            var e = new Expression(instruction, -1, e1, e2);
            if (instruction == Instruction.S_MOD)
            {
                if (v1.DataType.IsString())
                {
                    int typeNumber = 2;
                    switch (v2.DataType)
                    {
                        case DataType.Int:
                        case DataType.RefInt:
                            typeNumber = 2;
                            break;
                        case DataType.Float:
                        case DataType.RefFloat:
                            typeNumber = 3;
                            break;
                        case DataType.String:
                        case DataType.RefString:
                            typeNumber = 4;
                            break;
                        case DataType.Bool:
                        case DataType.RefBool:
                            typeNumber = 48;
                            break;
                        case DataType.Lint:
                        case DataType.RefLint:
                            typeNumber = 56;
                            break;
                        default:
                            //FIXME: find out all these numbers
                            break;
                    }
                    e.Arg3 = new Expression(Instruction.PUSH, -1, typeNumber);
                }
                else
                {
                    RaiseError2("First argument of S_MOD must be a string", token);
                    //error, first argument of S_MOD must be a string
                }
            }
            return e;
        }

        private Expression GenerateFunctionCallExpression(TokenExpression token)
        {
            var functionArgumentsToken = token.Subexpressions.GetOrNull(1);
            Expression functionCallExpression = null;
            IFunction functionToCall = null;

            var functionToken = token.Subexpressions.GetOrNull(0);
            if (functionToken == null)
            {
                return null;
            }
            if (functionToken.TokenType == TokenType.Identifier || functionToken.TokenType == TokenType.Dot || functionToken.TokenType == TokenType.ArrayIndex)
            {
                functionCallExpression = GenerateExpression(functionToken);
                if (functionCallExpression != null)
                {
                    var functionCallVariable = functionCallExpression.Variable;
                    functionToCall = functionCallVariable as IFunction;
                    if (functionToCall == null && functionCallVariable != null && functionCallVariable.DataType == DataType.Functype)
                    {
                        int funcTypeIndex = functionCallVariable.StructType;
                        if (funcTypeIndex == -1)
                        {
                            if (!ainFile.functionTypesKnown) { FindFuncTypesForParentAin(); }
                            var metadata = functionCallVariable.GetMetadata();
                            if (metadata != null)
                            {
                                funcTypeIndex = metadata.FuncTypeIndex;
                            }
                        }

                        functionToCall = ainFile.FunctionTypes.GetOrNull(funcTypeIndex);
                        if (functionCallExpression.ExpressionType != Instruction.CALLFUNC2)
                        {
                            functionCallExpression = new Expression(Instruction.CALLFUNC2, -1, functionCallExpression, new Expression(Instruction.PUSH, -1, funcTypeIndex));
                        }
                    }
                    if (functionToCall == null && functionCallVariable != null && functionCallVariable.DataType == DataType.Delegate)
                    {
                        int funcTypeIndex = functionCallVariable.StructType;
                        if (funcTypeIndex == -1)
                        {
                            if (!ainFile.functionTypesKnown) { FindFuncTypesForParentAin(); }
                            var metadata = functionCallVariable.GetMetadata();
                            if (metadata != null)
                            {
                                funcTypeIndex = metadata.FuncTypeIndex;
                            }
                        }

                        functionToCall = ainFile.Delegates.GetOrNull(funcTypeIndex);
                        if (functionCallExpression.ExpressionType != Instruction.DG_CALLBEGIN)
                        {
                            functionCallExpression = new Expression(Instruction.DG_CALLBEGIN, -1, funcTypeIndex, (Expression)null, functionCallExpression);
                        }
                    }
                    if (functionToCall == null)
                    {
                        var leftExpression = functionCallExpression.Arg1;
                        var leftVariable = leftExpression.Variable;
                        functionToCall = GetBuiltInFunction(functionCallExpression.ExpressionType, leftVariable);

                        if (leftVariable.DataType.IsArray())
                        {
                            if (functionCallExpression.ExpressionType == Instruction.A_SORT)
                            {
                                if (functionArgumentsToken.TokenType == TokenType.AddressOf)
                                {
                                    var token2 = functionArgumentsToken.Subexpressions[0];
                                    if (token2.TokenType == TokenType.Dot)
                                    {
                                        var e = GenerateExpression(functionArgumentsToken);
                                        if (e != null && e.ExpressionType == Instruction.PUSH)
                                        {
                                            functionCallExpression.ExpressionType = Instruction.A_SORT_MEM;
                                        }
                                        else
                                        {

                                        }
                                    }
                                    else if (token2.TokenType == TokenType.Identifier)
                                    {
                                        if (Functions.Contains(token2.Token.Value))
                                        {

                                        }
                                        else
                                        {

                                        }
                                    }
                                    else
                                    {

                                    }
                                }
                            }
                            if (leftExpression.ExpressionType != Instruction.AssignmentRef)
                            {
                                if (leftExpression.Args.Count == 2)
                                {
                                    leftExpression.ExpressionType = Instruction.AssignmentRef;
                                }
                                else
                                {

                                }
                            }
                        }

                        if (leftVariable.DataType.IsString())
                        {
                            switch (functionCallExpression.ExpressionType)
                            {
                                case Instruction.S_LENGTH:
                                case Instruction.S_LENGTHBYTE:
                                case Instruction.S_PLUSA:
                                case Instruction.S_REF:
                                    if (leftExpression.ExpressionType == Instruction.REF || leftExpression.ExpressionType == Instruction.S_REF)
                                    {
                                        leftExpression.ExpressionType = Instruction.AssignmentRef;
                                    }
                                    else
                                    {
                                        GetStringFunc2(functionCallExpression);
                                        //get the "2" version?
                                    }
                                    break;
                                case Instruction.S_PUSHBACK:
                                case Instruction.S_POPBACK:
                                case Instruction.S_ERASE:
                                    if (MakeShRef(leftExpression))
                                    {
                                        GetStringFunc2(functionCallExpression);
                                    }
                                    else
                                    {
                                        if (leftExpression.ExpressionType == Instruction.REF || leftExpression.ExpressionType == Instruction.S_REF)
                                        {
                                            leftExpression.ExpressionType = Instruction.AssignmentRef;
                                        }
                                        else
                                        {

                                        }
                                    }

                                    break;
                            }
                        }
                    }
                }
            }
            else if (functionToken.TokenType == TokenType.String)
            {
                var argExpression = GenerateExpression(functionArgumentsToken);
                if (argExpression != null)
                {
                    var arg = argExpression.Variable;
                    if (arg != null)
                    {
                        if (arg.DataType.IsInteger() || (arg.DataType == DataType.Bool || arg.DataType == DataType.RefBool))
                        {
                            return new Expression(Instruction.I_STRING, -1, argExpression);

                            //functionCallExpression = new Expression(Instruction.I_STRING, -1);
                            //functionToCall = AinFile.BuiltInFunctionsInverse["int_string"];
                        }
                        else if (arg.DataType.IsFloat())
                        {
                            return new Expression(Instruction.FTOS, -1, argExpression, new Expression(Instruction.PUSH, -1, -1));
                            //functionCallExpression = new Expression(Instruction.FTOS, -1);
                            //functionToCall = AinFile.BuiltInFunctionsInverse["float.String"];
                        }
                        else if (arg.DataType == DataType.String || arg.DataType == DataType.RefString)
                        {
                            return argExpression;
                        }
                        else
                        {
                            RaiseError(arg.ToString() + " can not be converted to a string.", functionArgumentsToken);
                            //error -  unknown type conversion to string
                        }
                    }
                    else
                    {
                        RaiseError(argExpression.ToString() + " does not evaluate to a variable or number.", functionArgumentsToken);
                        //error - does not evaluate as a variable or number
                    }
                }
                else
                {
                    RaiseError(functionArgumentsToken.Token.Value + " does not evaluate to a variable or number.", functionArgumentsToken);
                    //error - does not evaluate as an expression
                }
            }
            else if (functionToken.TokenType == TokenType.Float)
            {
                var argExpression = GenerateExpression(functionArgumentsToken);
                if (argExpression != null)
                {
                    var arg = argExpression.Variable;
                    if (arg != null)
                    {
                        if (arg.DataType.IsInteger() || (arg.DataType == DataType.Bool || arg.DataType == DataType.RefBool))
                        {
                            return new Expression(Instruction.ITOF, -1, argExpression);
                            //functionCallExpression = new Expression(Instruction.ITOF, -1);
                            //functionToCall = AinFile.BuiltInFunctionsInverse["int_float"];
                        }
                        else if (arg.DataType == DataType.Float || arg.DataType == DataType.RefFloat)
                        {
                            return argExpression;
                        }
                        else
                        {
                            RaiseError("Only integers may be converted to floats.  " + arg.ToString() + " is not an integer.", functionArgumentsToken);
                            //error -  unknown type conversion to float
                        }
                    }
                    else
                    {
                        RaiseError(argExpression.ToString() + " does not evaluate to a variable or number.", functionArgumentsToken);
                        //error - does not evaluate as a variable or number
                    }
                }
                else
                {
                    RaiseError(functionArgumentsToken.Token.Value + " does not evaluate to a variable or number.", functionArgumentsToken);
                    //error - does not evaluate as an expression
                }
            }
            else if (functionToken.TokenType == TokenType.Int)
            {
                var argExpression = GenerateExpression(functionArgumentsToken);
                if (argExpression != null)
                {
                    var arg = argExpression.Variable;
                    if (arg != null)
                    {
                        if (arg.DataType.IsFloat())
                        {
                            return new Expression(Instruction.FTOI, -1, argExpression);
                            //functionCallExpression = new Expression(Instruction.FTOI, -1);
                            //functionToCall = AinFile.BuiltInFunctionsInverse["float_int"];
                        }
                        else if (arg.DataType.IsString())
                        {
                            return new Expression(Instruction.STOI, -1, argExpression);
                            //functionCallExpression = new Expression(Instruction.STOI, -1);
                            //functionToCall = AinFile.BuiltInFunctionsInverse["string_int"];
                        }
                        else if (arg.DataType == DataType.Int || arg.DataType == DataType.RefInt)
                        {
                            return argExpression;
                        }
                        else if (arg.DataType == DataType.Bool || arg.DataType == DataType.RefBool)
                        {
                            return argExpression;
                        }
                        else
                        {
                            RaiseError(arg.ToString() + " can not be converted to an integer.", functionArgumentsToken);
                            //error -  unknown type conversion to int
                        }
                    }
                    else
                    {
                        RaiseError(argExpression.ToString() + " does not evaluate to a variable or number.", functionArgumentsToken);
                        //error - does not evaluate as a variable or number
                    }
                }
                else
                {
                    RaiseError(functionArgumentsToken.Token.Value + " does not evaluate to a variable or number.", functionArgumentsToken);
                    //error - does not evaluate as an expression
                }
            }
            else
            {

            }

            if (functionToCall != null)
            {
                int parameterCount = functionToCall.ParameterCount;
                if (parameterCount > 0)
                {
                    int argsIndex = 0;
                    if (functionCallExpression.ExpressionType == Instruction.CALLFUNC2)
                    {
                        argsIndex = 2;
                    }
                    else if (functionCallExpression.ExpressionType == Instruction.DG_CALLBEGIN)
                    {
                        argsIndex = 0;
                    }

                    var objectArgument = functionCallExpression.Args.GetOrNull(argsIndex);

                    if (functionToCall.Root == null)
                    {
                        if (parameterCount > 0)
                        {
                            argsIndex = 1;
                        }
                    }

                    var argumentsExpression = GenerateFunctionArguments(functionToCall, functionArgumentsToken);
                    if (argumentsExpression == null)
                    {
                        RaiseError("Failed to generate arguments for function call " + functionToCall.Name, functionToken);
                    }
                    if (objectArgument != null)
                    {
                        if (functionToCall.Root != null)
                        {
                            if (argumentsExpression == null)
                            {
                                argumentsExpression = objectArgument;
                            }
                            else
                            {
                                argumentsExpression = new Expression(Instruction.Comma, -1, objectArgument, argumentsExpression);
                            }
                        }
                        else
                        {
                            functionCallExpression.Arg1 = objectArgument;
                            if (functionCallExpression.ExpressionType == Instruction.A_ALLOC || functionCallExpression.ExpressionType == Instruction.A_REALLOC)
                            {
                                functionCallExpression.Value = objectArgument.Variable.ArrayDimensions;
                            }
                        }
                    }
                    functionCallExpression.Args.SetOrAdd(argsIndex, argumentsExpression);

                    if (functionCallExpression.ExpressionType == Instruction.DG_ADD || functionCallExpression.ExpressionType == Instruction.DG_ERASE || functionCallExpression.ExpressionType == Instruction.DG_EXIST)
                    {
                        if (functionCallExpression.Arg2.ExpressionType == Instruction.PassThroughExpression)
                        {
                            functionCallExpression.Arg3 = functionCallExpression.Arg2.Arg2;
                            functionCallExpression.Arg2 = functionCallExpression.Arg2.Arg1;
                        }
                    }
                }
            }
            else
            {
                RaiseError("Function not found: " + functionToken.ToStringReal(), functionToken);
                return null;
                //error - no function to call
            }
            if (functionToCall.DataType.IsRef())
            {
                functionCallExpression = CreateDummyVariableExpression(functionToCall, functionCallExpression);
            }

            return functionCallExpression;
        }

        private void FindFuncTypesForParentAin()
        {
            if (AinFile.LastLoadedAinFile != null)
            {
                var previousDefaultInstance = Expression.defaultAinFile;
                try
                {
                    Expression.defaultAinFile = AinFile.LastLoadedAinFile;
                    AinFile.LastLoadedAinFile.FindFunctionTypes();

                    var newMetaDataFile = AinFile.LastLoadedAinFile.MetadataFile.Clone(this.ainFile);
                    Expression.defaultAinFile = previousDefaultInstance;
                    foreach (var pair in newMetaDataFile.Metadata)
                    {
                        if (pair.Value.FuncTypeIndex != -1)
                        {
                            var newValue = this.ainFile.MetadataFile.Metadata.GetOrAddNew(pair.Key);
                            newValue.FuncTypeIndex = pair.Value.FuncTypeIndex;
                        }
                    }
                }
                finally
                {
                    Expression.defaultAinFile = previousDefaultInstance;
                }
            }
        }

        private void GetStringFunc2(Expression functionCallExpression)
        {
            switch (functionCallExpression.ExpressionType)
            {
                case Instruction.S_ERASE:
                    functionCallExpression.ExpressionType = Instruction.S_ERASE2;
                    break;
                case Instruction.S_LENGTH:
                    functionCallExpression.ExpressionType = Instruction.S_LENGTH2;
                    break;
                case Instruction.S_LENGTHBYTE:
                    functionCallExpression.ExpressionType = Instruction.S_LENGTHBYTE2;
                    break;
                case Instruction.S_PLUSA:
                    functionCallExpression.ExpressionType = Instruction.S_PLUSA;
                    break;
                case Instruction.S_POPBACK:
                    functionCallExpression.ExpressionType = Instruction.S_POPBACK2;
                    break;
                case Instruction.S_PUSHBACK:
                    functionCallExpression.ExpressionType = Instruction.S_PUSHBACK2;
                    break;
                case Instruction.S_REF:
                    functionCallExpression.ExpressionType = Instruction.S_REF2;
                    break;
            }
        }

        Expression GenerateBlockExpression(TokenExpression token, bool setBreakScope, bool setContinueScope)
        {
            this.setBreakScope = setBreakScope;
            this.setContinueScope = setContinueScope;

            List<Expression> statementList = GenerateBlockExpressionList(token);
            var statementExpression = ConvertToStatements(statementList);

            this.setBreakScope = false;
            this.setContinueScope = false;

            return statementExpression;
        }



        private List<Expression> GenerateBlockExpressionList(TokenExpression token)
        {
            List<Expression> statementList = new List<Expression>();
            GenerateBlockExpressionList(token, statementList);
            return statementList;
        }

        VariableDictionaryWithParent breakScope = null;
        VariableDictionaryWithParent continueScope = null;
        bool setBreakScope = false;
        bool setContinueScope = false;

        private void GenerateBlockExpressionList(TokenExpression token, List<Expression> statementList)
        {
            var oldScope = localScope;
            localScope = new VariableDictionaryWithParent(oldScope);
            //scope.PushDictionary(localScope);

            var oldBreakScope = breakScope;
            var oldContinueScope = continueScope;

            if (setBreakScope)
            {
                breakScope = localScope;
                setBreakScope = false;
            }
            if (setContinueScope)
            {
                continueScope = localScope;
                setContinueScope = false;
            }

            if (token.TokenType != TokenType.Block)
            {
                Expression expr = GenerateExpression(token);
                if (expr != null)
                {
                    statementList.Add(expr);
                }
            }
            else
            {
                foreach (var statementToken in token.Subexpressions)
                {
                    Expression expr = GenerateExpression(statementToken);
                    if (expr != null)
                    {
                        statementList.Add(expr);
                    }
                }
            }

            if (oldScope != this.firstLocalScope)
            {
                bool alreadyDeleted = WasAlreadyDeleted(token, statementList);

                if (!alreadyDeleted)
                {
                    Expression eraseStatements = null;
                    DestroyLocalVariables(localScope, true, ref eraseStatements);
                    if (eraseStatements != null)
                    {
                        statementList.Add(eraseStatements);
                    }
                }
            }
            //scope.PopDictionary();

            localScope = oldScope;
            breakScope = oldBreakScope;
            continueScope = oldContinueScope;

        }

        private static bool WasAlreadyDeleted(TokenExpression token, List<Expression> statementList)
        {
            bool alreadyDeleted = false;
            if (statementList.Count > 0)
            {
                bool containsDeletes = false;
                var lastStatement = statementList.Last();
                if (lastStatement.ExpressionType == Instruction.Statement)
                {
                    var statement = lastStatement.GetNextStatement(false, false);
                    if (statement.ExpressionType == Instruction.A_FREE || statement.ExpressionType == Instruction.SH_LOCALDELETE || statement.ExpressionType == Instruction.DG_CLEAR)
                    {
                        containsDeletes = true;
                    }

                    if (containsDeletes)
                    {
                        Expression finalStatement = null;
                        while (statement != null)
                        {
                            var nextStatement = statement.GetNextStatement(false, false);
                            if (nextStatement == null)
                            {
                                nextStatement = statement.GetParentNextExpression();
                            }
                            if (nextStatement == null)
                            {
                                finalStatement = statement;
                            }
                            statement = nextStatement;
                        }

                        if (finalStatement.ExpressionType == Instruction.Break || finalStatement.ExpressionType == Instruction.Continue || finalStatement.ExpressionType == Instruction.Goto)
                        {
                            var parentTokenType = token.Parent.TokenType;
                            if (parentTokenType == TokenType.Switch)
                            {
                                alreadyDeleted = true;
                            }
                        }
                    }
                }
            }
            return alreadyDeleted;
        }

        private Expression ConvertToStatements(List<Expression> statementList)
        {
            return Decompiler.ConvertToStatements(statementList);
        }

        private Expression GenerateFunctionArguments(IFunction functionToCall, TokenExpression functionArgumentsToken)
        {
            //TODO: type checking
            int argIndex = 0;
            int parameterCount = functionToCall.ParameterCount;
            var t = functionArgumentsToken;
            Expression eResult = null;
            Expression currentComma = null;

            while (argIndex < functionToCall.ParameterCount)
            {
                var parameter = functionToCall.Parameters[argIndex];

                bool isRef = false;
                if (parameter.DataType.IsPrimitiveRefType())
                {
                    if (functionToCall is HllFunction)
                    {
                        isRef = true;
                    }
                    else if (argIndex + 1 < functionToCall.ParameterCount)
                    {
                        var nextParameter = functionToCall.Parameters[argIndex + 1];
                        if (nextParameter.DataType == DataType.Void)
                        {
                            argIndex++;
                            isRef = true;
                        }
                        else
                        {
                            RaiseError("Function is missing <void> after simple ref type.", functionArgumentsToken);
                        }
                    }
                    else
                    {
                        RaiseError("Function is missing <void> after simple ref type.", functionArgumentsToken);
                        //error - function parameter out of range
                    }
                }
                //if (parameter.DataType == DataType.Void)
                //{
                //    argIndex++;
                //    isRef = true;
                //    parameter = functionToCall.Parameters.GetOrNull(argIndex);
                //    if (parameter == null)
                //    {
                //        break;
                //    }
                //}
                argIndex++;

                TokenExpression tArg;
                if (t != null && t.TokenType == TokenType.Comma)
                {
                    tArg = t.Subexpressions.GetOrNull(0);
                    var t2 = t.Subexpressions.GetOrNull(1);
                    t = t2;
                }
                else
                {
                    tArg = t;
                    t = null;
                }

                bool wasInitialValue = false; //hack for daibanchou bools
                Expression eArg = null;
                if (tArg == null)
                {
                    var iv = InitialValues.GetOrNull(parameter);
                    //check for a default value?
                    if (iv != null)
                    {
                        eArg = ConstantToExpression(iv);
                        wasInitialValue = true;
                    }
                    else if (parameter.HasDefaultValue())
                    {
                        var initialValue = parameter.GetDefaultValue();
                        eArg = ConstantToExpression(initialValue);
                        wasInitialValue = true;
                    }
                    else
                    {
                        RaiseError("Missing value for function argument " + parameter.ToString() + ", and there is no default value", tArg ?? functionArgumentsToken);
                        //error
                    }
                }
                else
                {
                    eArg = GenerateExpression(tArg);
                    if (eArg == null)
                    {
                        RaiseError("Could not determine a value for function argument " + parameter.ToString() + " (value = " + tArg.Token.Value + ")", tArg ?? functionArgumentsToken);
                    }
                    //TODO: type checking/conversion
                }
                if (eArg != null)
                {
                    if (isRef)
                    {
                        if (eArg.ExpressionType == Instruction.REF)
                        {
                            eArg.ExpressionType = Instruction.AssignmentRef;
                        }
                        else if (eArg.ExpressionType == Instruction.CALLFUNC && eArg.Value == 0)
                        {
                            //fixed up later
                        }
                        else
                        {
                            RaiseError(eArg.ToString() + " is not a valid reference for function argument " + parameter.ToString(), tArg);
                            //error?
                        }
                    }

                    var vArg = eArg.Variable;
                    if (vArg != null)
                    {
                        if (parameter.DataType.IsFloat() && vArg.DataType.IsInteger())
                        {
                            eArg = new Expression(Instruction.ITOF, -1, eArg);
                        }
                        else if (parameter.DataType.IsInteger() && vArg.DataType.IsFloat())
                        {
                            eArg = new Expression(Instruction.FTOI, -1, eArg);
                        }
                        else if (parameter.DataType == DataType.Bool && vArg.DataType.IsInteger())
                        {
                            //hack for daibanchou initial values - remove ITOB if there was an initial value
                            if (!wasInitialValue)
                            {
                                eArg = new Expression(Instruction.ITOB, -1, eArg);
                            }
                        }
                        else if (parameter.DataType == DataType.Bool && vArg.DataType == DataType.Bool && vArg.Canonicalize() == null)
                        {
                            eArg = new Expression(Instruction.ITOB, -1, eArg);
                        }
                        else if (parameter.DataType == DataType.Lint && vArg.DataType == DataType.Int)
                        {
                            //hack for sengoku rance initial values - remove ITOLI if there was an initial value
                            if (!wasInitialValue)
                            {
                                eArg = new Expression(Instruction.ITOLI, -1, eArg);
                            }
                        }
                        else if (parameter.DataType.IsArray() && vArg.DataType.IsArray())
                        {
                            if (eArg.ExpressionType == Instruction.AssignmentRef)
                            {
                                eArg.ExpressionType = Instruction.REF;
                                if (parameter.DataType.IsNonRefArray() && vArg.DataType.IsNonRefArray())
                                {
                                    eArg = new Expression(Instruction.A_REF, -1, eArg);  //???
                                }
                            }
                            if (parameter.DataType.IsNonRefArray() && vArg.DataType.IsRefArray())
                            {
                                eArg = new Expression(Instruction.A_REF, -1, eArg);
                            }
                        }
                        else if (eArg.ExpressionType == Instruction.CALLFUNC && eArg.Value == 0)
                        {
                            eArg.ExpressionType = Instruction.PUSH;
                            if (parameter.DataType == DataType.IMainSystem)
                            {
                                //value is already 0
                            }
                            else if (parameter.DataType == DataType.Functype)
                            {
                                //value is already 0
                            }
                            else if (parameter.DataType == DataType.Delegate)
                            {
                                //TODO: pass NULL into function parameter of type delegate
                                //Debugger.Break();
                                eArg = new Expression(Instruction.DG_NEW, -1);
                            }
                            else if (parameter.DataType.IsPrimitiveRefType())
                            {
                                eArg = new Expression(Instruction.AssignmentRef, -1,
                                    new Expression(Instruction.PUSH, -1, -1),
                                    new Expression(Instruction.PUSH, -1, 0));
                            }
                            else if (parameter.DataType != DataType.Int)
                            {
                                eArg.Value = -1;
                            }
                        }
                        else if ((vArg.DataType == DataType.RefStruct || eArg.ExpressionType == Instruction.PUSHSTRUCTPAGE) && parameter.DataType == DataType.Struct)
                        {
                            if (eArg.ExpressionType == Instruction.ASSIGN)
                            {
                                if (this.ainFile.TargetVMVersion > 216)
                                {
                                    eArg = new Expression(Instruction.SR_REF2, -1, eArg, vArg.StructType);
                                }
                                //if (this.TargetVMVersion <= 216)
                                //{
                                //    RaiseError("Cannot pass 'ref struct return values of function calls' as a function argument in VM version <= 216", tArg);
                                //}

                            }
                            else if (eArg.ExpressionType == Instruction.PUSHSTRUCTPAGE)
                            {
                                if (this.ainFile.TargetVMVersion > 216)
                                {
                                    eArg = new Expression(Instruction.SR_REF2, -1, eArg, vArg.StructType);
                                }
                                //if (this.TargetVMVersion <= 216)
                                //{
                                //    RaiseError("Cannot pass 'this' as a function argument in VM version <= 216", tArg);
                                //}
                            }
                        }
                        else if (parameter.DataType == DataType.Delegate)
                        {
                            bool isDelegateInstruction = functionToCall.Name.StartsWith("delegate.");
                            if (eArg.ExpressionType == Instruction.DG_NEW_FROM_METHOD)
                            {
                                if (isDelegateInstruction)
                                {
                                    eArg.ExpressionType = Instruction.PassThroughExpression;
                                }
                            }
                            else if (vArg.DataType == DataType.Delegate)
                            {
                                if (!eArg.ExpressionType.IsFunctionCall())
                                {
                                    eArg = new Expression(Instruction.DG_COPY, -1, eArg);
                                }
                            }
                            else if (eArg.ExpressionType == Instruction.PUSH)
                            {
                                bool isLocalClass = false;
                                var func = ainFile.GetFunction(eArg.Value);
                                if (func != null)
                                {
                                    string myClassName = currentFunction.GetClassName();
                                    string otherClassName = func.GetClassName();
                                    if (!String.IsNullOrEmpty(myClassName) && myClassName == otherClassName)
                                    {
                                        isLocalClass = true;
                                    }
                                }

                                Instruction ins = Instruction.DG_NEW_FROM_METHOD;
                                if (isDelegateInstruction)
                                {
                                    ins = Instruction.PassThroughExpression;
                                }
                                var newE1 = new Expression(Instruction.PUSH, -1, -1);
                                if (isLocalClass)
                                {
                                    newE1.ExpressionType = Instruction.PUSHSTRUCTPAGE;
                                }

                                eArg = new Expression(ins, -1, newE1, eArg);
                            }
                            else if (vArg.DataType.IsString())
                            {
                                if (isDelegateInstruction)
                                {
                                    eArg = //new Expression(Instruction.PassThroughExpression, -1,
                                        new Expression(Instruction.DG_STR_TO_METHOD, -1,
                                            new Expression(Instruction.PUSH, -1, -1), // object for delegate
                                            eArg,
                                            new Expression(Instruction.PUSH, -1, parameter.StructType) //type of delegate
                                            );
                                }
                                else
                                {
                                    eArg = new Expression(Instruction.DG_NEW_FROM_METHOD, -1,
                                        new Expression(Instruction.DG_STR_TO_METHOD, -1,
                                            new Expression(Instruction.PUSH, -1, -1), // object for delegate
                                            eArg,
                                            new Expression(Instruction.PUSH, -1, parameter.StructType) //type of delegate
                                            ));
                                }
                            }
                            else if (eArg.ExpressionType == Instruction.CALLFUNC && eArg.Value == 0)
                            {
                                eArg = new Expression(Instruction.DG_NEW, -1);
                            }
                            else
                            {

                            }
                        }
                    }

                    if (eResult == null)
                    {
                        eResult = eArg;
                    }
                    else
                    {
                        if (currentComma == null)
                        {
                            currentComma = new Expression(Instruction.Comma, -1, eResult, eArg);
                            eResult = currentComma;
                        }
                        else
                        {
                            var oldArg2 = currentComma.Arg2;
                            var newComma = new Expression(Instruction.Comma, -1, oldArg2, eArg);
                            currentComma.Arg2 = newComma;
                            currentComma = newComma;
                        }
                    }
                }
                else
                {
                    RaiseError2("Could not determine a value for function argument " + parameter.ToString(), tArg ?? functionArgumentsToken);
                    //error?
                }
            }
            return eResult;
        }

        private Expression CompileText(string codeText)
        {
            List<TokenExpression> tokens = new List<TokenExpression>();
            IncludeString(codeText, tokens);

            SecondPass secondPass = new SecondPass(this);
            var tokenResults = secondPass.CompileTokens(tokens);

            List<Expression> expressionResults = new List<Expression>();
            foreach (var tokenResult in tokenResults)
            {
                expressionResults.Add(GenerateExpression(tokenResult));
            }

            return ConvertToStatements(expressionResults);
        }

        private Expression ConstantToExpression(InitialValue initialValue)
        {
            if (initialValue.DataType == DataType.Float)
            {
                return new Expression(Instruction.F_PUSH, -1, initialValue.IntValue);
            }
            else if (initialValue.DataType == DataType.String)
            {
                return new Expression(Instruction.S_PUSH, -1, Strings.Add(initialValue.StringValue ?? ""));
            }
            else //if (initialValue.DataType == DataType.Int || initialValue.DataType == DataType.Lint || initialValue.DataType == DataType.Bool)
            {
                return new Expression(Instruction.PUSH, -1, initialValue.IntValue);
            }
        }

        static Dictionary<string, Instruction> BuiltInFunctionToInstruction = GetBuiltInFunctionInstructions();
        public static Dictionary<Instruction, Function> InstructionToBuiltInFunction = GetBuiltInFunctionInstructionsInverse();

        private static Dictionary<Instruction, Function> GetBuiltInFunctionInstructionsInverse()
        {
            var builtInFunctionInstructions = GetBuiltInFunctionInstructions().ToList();
            //add the secondary forms
            builtInFunctionInstructions.Add(new KeyValuePair<string, Instruction>("string.Length", Instruction.S_LENGTH2));
            builtInFunctionInstructions.Add(new KeyValuePair<string, Instruction>("string.LengthByte", Instruction.S_LENGTHBYTE2));
            builtInFunctionInstructions.Add(new KeyValuePair<string, Instruction>("string.Erase", Instruction.S_ERASE2));
            builtInFunctionInstructions.Add(new KeyValuePair<string, Instruction>("string.PushBack", Instruction.S_PUSHBACK2));
            builtInFunctionInstructions.Add(new KeyValuePair<string, Instruction>("string.PopBack", Instruction.S_POPBACK2));
            var dic = new Dictionary<Instruction, Function>();
            foreach (var pair in builtInFunctionInstructions)
            {
                dic[pair.Value] = AinFile.BuiltInFunctionsInverse[pair.Key];
            }
            return dic;
        }

        private static Dictionary<string, Instruction> GetBuiltInFunctionInstructions()
        {
            Dictionary<string, Instruction> dic = new Dictionary<string, Instruction>();
            dic.Add("int.String", Instruction.I_STRING);
            //dic.Add("string", Instruction.I_STRING);
            dic.Add("float.String", Instruction.FTOS);
            dic.Add("string.Int", Instruction.STOI);
            dic.Add("string.Length", Instruction.S_LENGTH);
            dic.Add("string.LengthByte", Instruction.S_LENGTHBYTE);
            dic.Add("string.Empty", Instruction.S_EMPTY);
            dic.Add("string.Find", Instruction.S_FIND);
            dic.Add("string.GetPart", Instruction.S_GETPART);
            dic.Add("string.PushBack", Instruction.S_PUSHBACK);
            dic.Add("string.PopBack", Instruction.S_POPBACK);
            dic.Add("string.Erase", Instruction.S_ERASE);
            dic.Add("array.Alloc", Instruction.A_ALLOC);
            dic.Add("array.Realloc", Instruction.A_REALLOC);
            dic.Add("array.Free", Instruction.A_FREE);
            dic.Add("array.Numof", Instruction.A_NUMOF);
            dic.Add("array.Copy", Instruction.A_COPY);
            dic.Add("array.Fill", Instruction.A_FILL);
            dic.Add("array.PushBack", Instruction.A_PUSHBACK);
            dic.Add("array.PopBack", Instruction.A_POPBACK);
            dic.Add("array.Empty", Instruction.A_EMPTY);
            dic.Add("array.Erase", Instruction.A_ERASE);
            dic.Add("array.Insert", Instruction.A_INSERT);
            dic.Add("array.Sort", Instruction.A_SORT);
            dic.Add("array.Find", Instruction.A_FIND);
            dic.Add("array.Reverse", Instruction.A_REVERSE);
            dic.Add("delegate.Numof", Instruction.DG_NUMOF);
            dic.Add("delegate.Clear", Instruction.DG_CLEAR);
            dic.Add("delegate.Add", Instruction.DG_ADD);
            dic.Add("delegate.Erase", Instruction.DG_ERASE);
            dic.Add("delegate.Exist", Instruction.DG_EXIST);
            return dic;
        }

        private Expression GenerateDot(TokenExpression token)
        {
            var leftToken = token.Subexpressions.GetOrNull(0);
            var rightToken = token.Subexpressions.GetOrNull(1);

            if (leftToken.Subexpressions.Count == 0)
            {
                _unknownSymbolOkay = true;
            }
            var leftExpression = GenerateExpression(leftToken);
            _unknownSymbolOkay = false;

            if (leftExpression != null)
            {
                var leftVariable = leftExpression.Variable;
                if (leftVariable != null && !leftVariable.DataType.IsArray() && !leftVariable.DataType.IsDelegate())
                {
                    var structInfo = leftVariable.GetStructType();
                    if (structInfo != null)
                    {
                        if (rightToken.TokenType == TokenType.Identifier)
                        {
                            string memberName = rightToken.Token.Value;
                            var classMembers = GetClassMembers(structInfo.Name);
                            if (classMembers.ContainsKey(memberName))
                            {
                                var symbol = classMembers[memberName];
                                var expression = SymbolToExpression(symbol, memberName);
                                var expressionToChange = expression;
                                if (expression.Arg1.IsReference())
                                {
                                    expressionToChange = expression.Arg1;
                                }

                                expressionToChange.Arg1 = leftExpression;
                                return expression;
                            }
                            //structInfo.Members
                        }
                        else
                        {
                            RaiseError2("Right side of dot is not an identifier", rightToken);
                            //error - not an identifier
                        }

                    }
                }

                if (leftVariable != null)
                {
                    if (rightToken.TokenType == TokenType.Identifier)
                    {
                        string prefix = "";
                        if (leftVariable.DataType.IsArray())
                        {
                            prefix = "array";
                        }
                        else if (leftVariable.DataType.IsString())
                        {
                            prefix = "string";
                        }
                        else if (leftVariable.DataType.IsInteger())
                        {
                            prefix = "int";
                        }
                        else if (leftVariable.DataType.IsFloat())
                        {
                            prefix = "float";
                        }
                        else if (leftVariable.DataType == DataType.Delegate || leftVariable.DataType == DataType.RefDelegate)
                        {
                            prefix = "delegate";  //TODO: Implement delegates
                        }
                        Instruction ins = Instruction.Nop;
                        if (prefix != "")
                        {
                            string funcName = prefix + "." + rightToken.Token.Value;
                            if (BuiltInFunctionToInstruction.ContainsKey(funcName))
                            {
                                ins = BuiltInFunctionToInstruction[funcName];
                            }
                        }

                        //if (leftVariable.DataType.IsArray())
                        //{
                        //    Instruction ins = Instruction.Nop;
                        //    switch (rightToken.Token.Value)
                        //    {
                        //        case "Alloc":
                        //            ins = Instruction.A_ALLOC;
                        //            break;
                        //        case "Realloc":
                        //            ins = Instruction.A_REALLOC;
                        //            break;
                        //        case "Free":
                        //            ins = Instruction.A_FREE;
                        //            break;
                        //        case "Numof":
                        //            ins = Instruction.A_NUMOF;
                        //            break;
                        //        case "Copy":
                        //            ins = Instruction.A_COPY;
                        //            break;
                        //        case "Fill":
                        //            ins = Instruction.A_FILL;
                        //            break;
                        //        case "PushBack":
                        //            ins = Instruction.A_PUSHBACK;
                        //            break;
                        //        case "PopBack":
                        //            ins = Instruction.A_POPBACK;
                        //            break;
                        //        case "Empty":
                        //            ins = Instruction.A_EMPTY;
                        //            break;
                        //        case "Erase":
                        //            ins = Instruction.A_ERASE;
                        //            break;
                        //        case "Insert":
                        //            ins = Instruction.A_INSERT;
                        //            break;
                        //        case "Sort":
                        //            ins = Instruction.A_SORT;
                        //            break;

                        //    }
                        //    if (ins != Instruction.Nop)
                        //    {
                        //        return new Expression(ins, -1, leftExpression);
                        //    }
                        //    else
                        //    {
                        //        //error
                        //    }
                        //}
                        //else if (leftVariable.DataType.IsString())
                        //{
                        //    Instruction ins = Instruction.Nop;
                        //    switch (rightToken.Token.Value)
                        //    {
                        //        case "Int":
                        //            ins = Instruction.STOI;
                        //            break;
                        //        case "Length":
                        //            ins = Instruction.S_LENGTH;
                        //            break;
                        //        case "LengthByte":
                        //            ins = Instruction.S_LENGTHBYTE;
                        //            break;
                        //        case "Empty":
                        //            ins = Instruction.S_EMPTY;
                        //            break;
                        //        case "Find":
                        //            ins = Instruction.S_FIND;
                        //            break;
                        //        case "GetPart":
                        //            ins = Instruction.S_GETPART;
                        //            break;
                        //        case "PushBack":
                        //            ins = Instruction.S_PUSHBACK;
                        //            break;
                        //        case "PopBack":
                        //            ins = Instruction.S_POPBACK;
                        //            break;
                        //        case "Erase":
                        //            ins = Instruction.S_ERASE;
                        //            break;

                        //    }
                        if (ins != Instruction.Nop)
                        {
                            return new Expression(ins, -1, leftExpression);
                        }
                        else
                        {
                            RaiseError2("No instruction found for", token);
                            //error - instruction not found
                        }
                    }
                    else
                    {
                        RaiseError2("Right side of dot is not an identifier", rightToken);
                        //error - not an indentifier
                    }

                }
            }
            else
            {
                if (leftToken.TokenType == TokenType.Identifier && rightToken.TokenType == TokenType.Identifier)
                {
                    string left = leftToken.Token.Value;
                    string right = rightToken.Token.Value;
                    if (left == "system")
                    {
                        int callNumber = AinFile.SystemCallNameToIndex.GetOrDefault(left + "." + right, -1);
                        if (callNumber != -1)
                        {
                            return new Expression(Instruction.CALLSYS, -1, callNumber);
                        }
                        else
                        {
                            RaiseError("System call not found: system." + rightToken.Token.Value, rightToken);
                            //error - system call not found
                        }
                    }
                    else
                    {
                        string fullName = leftToken.Token.Value + "." + rightToken.Token.Value;
                        var libraryCall = Symbols.GetOrNull(fullName) as HllFunction;
                        if (libraryCall != null)
                        {
                            return new Expression(Instruction.CALLHLL, -1, libraryCall.ParentLibrary.Index, libraryCall.Index);
                        }
                        else
                        {
                            RaiseError("Library not found:" + leftToken.Token.Value + "." + rightToken.Token.Value, rightToken);
                            //error - library not found
                        }
                    }
                }
                else
                {
                    RaiseError("Left side and right side of dot are not identifiers. " + leftToken.Token.Value + "." + rightToken.Token.Value, rightToken);
                    //error - not identifiers
                }
            }
            RaiseError("Unable to generate dot expression " + leftToken.Token.Value + "." + rightToken.Token.Value, token);


            return null;
        }

        //Function GetBuiltInFunctionPrototype(Instruction ins, Variable dataType

        private Function GetBuiltInFunction(Instruction ins, IVariable leftVariaible)
        {
            if (!InstructionToBuiltInFunction.ContainsKey(ins))
            {
                return null;
            }
            var prototype = InstructionToBuiltInFunction[ins];
            switch (ins)
            {
                case Instruction.A_ALLOC:
                case Instruction.A_REALLOC:
                    {
                        if (leftVariaible.ArrayDimensions > 1)
                        {
                            var func = prototype.Clone();
                            for (int i = 0; i < leftVariaible.ArrayDimensions - 1; i++)
                            {
                                func.Parameters.Add(new Variable("numberOfElements", DataType.Int, -1, 0));
                            }
                            func.ParameterCount = func.Parameters.Count;
                            return func;
                        }
                        return prototype;
                    }
                    break;
                case Instruction.A_FIND:
                    {
                        if (leftVariaible.DataType.GetTypeOfArrayElement().IsInteger())
                        {
                            return prototype;
                        }
                        //don't clone parameter #3
                        var parameter3 = prototype.Parameters[3];
                        var func = prototype.Clone();
                        func.Parameters[3] = parameter3;
                        var value = func.Parameters.Where(p => p.Name == "lookFor").FirstOrDefault();
                        value.StructType = leftVariaible.StructType;
                        value.DataType = leftVariaible.DataType.GetTypeOfArrayElement();
                        return func;
                    }
                    break;
                case Instruction.A_FILL:
                case Instruction.A_PUSHBACK:
                case Instruction.A_INSERT:
                    {
                        if (leftVariaible.DataType.GetTypeOfArrayElement() == DataType.Int)
                        {
                            return prototype;
                        }
                        var func = prototype.Clone();
                        var value = func.Parameters.Where(p => p.Name == "value").FirstOrDefault();
                        value.StructType = leftVariaible.StructType;
                        value.DataType = leftVariaible.DataType.GetTypeOfArrayElement();
                        return func;
                    }
                    break;
                case Instruction.A_COPY:
                    {
                        if (leftVariaible.DataType.GetTypeOfArrayElement().IsInteger())
                        {
                            return prototype;
                        }
                        var func = prototype.Clone();
                        var value = func.Parameters.Where(p => p.Name == "a").FirstOrDefault();
                        value.StructType = leftVariaible.StructType;
                        value.DataType = leftVariaible.DataType.GetRefArrayType();
                        return func;
                    }
                case Instruction.DG_ADD:
                case Instruction.DG_ERASE:
                    {
                        var func = prototype.Clone();
                        var value = func.Parameters[0];
                        value.StructType = leftVariaible.StructType;
                        return func;
                    }
                    break;
                default:
                    return prototype;
                    break;
            }
        }


        private Expression SymbolToExpression(IVariable symbol)
        {
            return SymbolToExpression(symbol, symbol.Name);
        }

        private Expression SymbolToExpression(IVariable symbol, string symbolName)
        {
        TryAgain:
            if (symbol != null)
            {
                {
                    var variable = symbol as Variable;
                    if (variable != null)
                    {
                        if (variable.Parent is Struct)
                        {
                            var eStruct = new Expression(Instruction.PUSHSTRUCTPAGE, -1);
                            var eIndex = new Expression(Instruction.PUSH, -1);
                            eIndex.Value = variable.Index;
                            var eRef = new Expression(Instruction.REF, -1);
                            eRef.Arg1 = eStruct;
                            eRef.Arg2 = eIndex;
                            GetRefType(symbol, ref eRef);
                            return eRef;
                        }
                        else if (variable.Parent is Function)
                        {
                            var eLocal = new Expression(Instruction.PUSHLOCALPAGE, -1);
                            var eIndex = new Expression(Instruction.PUSH, -1);
                            eIndex.Value = variable.Index;
                            var eRef = new Expression(Instruction.REF, -1);
                            eRef.Arg1 = eLocal;
                            eRef.Arg2 = eIndex;
                            GetRefType(symbol, ref eRef);
                            return eRef;
                        }
                        else if (this.InitialValues.ContainsKey(symbol) && this.InitialValues.ContainsKey(symbol))
                        {
                            var initialValue = this.InitialValues[symbol];
                            return ConstantToExpression(initialValue);
                        }
                        else
                        {

                        }
                    }
                }
                {
                    var global = symbol as Global;
                    if (global != null)
                    {
                        var eGlobal = new Expression(Instruction.PUSHGLOBALPAGE, -1);
                        var eIndex = new Expression(Instruction.PUSH, -1);
                        eIndex.Value = global.Index;
                        var eRef = new Expression(Instruction.REF, -1);
                        eRef.Arg1 = eGlobal;
                        eRef.Arg2 = eIndex;
                        GetRefType(symbol, ref eRef);
                        return eRef;
                    }
                }
                {
                    var ifunction = symbol as IFunction;
                    if (ifunction != null)
                    {
                        var function = ifunction as Function;
                        if (function != null)
                        {
                            if (function.Name != symbolName)
                            {
                                var eStruct = new Expression(Instruction.PUSHSTRUCTPAGE, -1);
                                var eCallMethod = new Expression(Instruction.CALLMETHOD, -1, function.Index);
                                eCallMethod.Arg1 = eStruct;
                                return eCallMethod;
                            }
                            else
                            {
                                var eCallFunc = new Expression(Instruction.CALLFUNC, -1, function.Index);
                                eCallFunc.Value2 = -1;
                                return eCallFunc;
                            }
                        }
                        var funcType = ifunction as FunctionType;
                        if (funcType != null)
                        {
                            if (Functypes.Contains(symbolName))
                            {
                                //is this ever called?
                                var eCallFunc = new Expression(Instruction.CALLFUNC2, -1);
                                var ePush = new Expression(Instruction.PUSH, -1, funcType.Index);
                                eCallFunc.Arg2 = ePush;
                                eCallFunc.Value2 = -1;
                                return eCallFunc;
                            }
                            else if (Delegates.Contains(symbolName))
                            {
                                //is this ever called?
                                var eCallFunc = new Expression(Instruction.DG_CALLBEGIN, -1);
                                eCallFunc.Value = funcType.Index;
                                return eCallFunc;
                            }
                        }
                        var structInfo = ifunction as Struct;
                        if (structInfo != null)
                        {
                            var functionSymbol = this.ainFile.GetFunction(symbolName);
                            if (functionSymbol != null)
                            {
                                symbol = functionSymbol;
                                goto TryAgain;
                            }

                            var exp = new Expression(Instruction.PrintStructType, -1, structInfo.Index);
                            return exp;
                        }
                    }
                }
            }
            if (symbol == null && symbolName == "this")
            {
                return new Expression(Instruction.PUSHSTRUCTPAGE, -1);
            }
            return null;
        }

        private static void GetRefType(IVariable symbol, ref Expression expression)
        {
            var dataType = symbol.DataType;
            switch (dataType)
            {
                case DataType.RefBool:
                case DataType.RefFloat:
                case DataType.RefFunctype:
                case DataType.RefInt:
                case DataType.RefLint:
                    expression.ExpressionType = Instruction.REFREF;
                    expression = new Expression(Instruction.REF, -1, expression);
                    return;
                case DataType.RefDelegate:
                case DataType.Delegate:
                    expression.ExpressionType = Instruction.REF;
                    return;
                case DataType.RefString:
                case DataType.String:
                    expression.ExpressionType = Instruction.S_REF;
                    return;
                case DataType.RefStruct:
                case DataType.Struct:
                    expression.ExpressionType = Instruction.SR_REF;
                    expression.Value = symbol.StructType;
                    return;
                case DataType.ArrayBool:
                case DataType.ArrayDelegate:
                case DataType.ArrayFloat:
                case DataType.ArrayFunctype:
                case DataType.ArrayInt:
                case DataType.ArrayLint:
                case DataType.ArrayString:
                case DataType.ArrayStruct:
                case DataType.RefArrayBool:
                case DataType.RefArrayDelegate:
                case DataType.RefArrayFloat:
                case DataType.RefArrayFunctype:
                case DataType.RefArrayInt:
                case DataType.RefArrayLint:
                case DataType.RefArrayString:
                case DataType.RefArrayStruct:
                    expression.ExpressionType = Instruction.AssignmentRef;
                    return;
                default:
                    expression.ExpressionType = Instruction.REF;
                    return;
            }
        }

        //private Expression GenerateSymbolExpression(TokenExpression e1)
        //{
        //    string symbolName = e1.Token.Value;
        //    var symbol = Symbols.GetOrNull(symbolName);



        //    return new Expression(Instruction.PUSHGLOBALPAGE, -1);
        //}

    }
}
