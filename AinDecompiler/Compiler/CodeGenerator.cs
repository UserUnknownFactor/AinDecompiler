using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace AinDecompiler.Compiler
{
    public class CodeGenerator
    {
        AinFile ainFile;
        public CodeGenerator(AinFile ainFile)
        {
            this.ainFile = ainFile;
            this.Code = new MemoryStream();
            this.br = new BinaryReader(Code);
            this.bw = new BinaryWriter(Code);

            if (ainFile.Version >= 6)
            {
                this.GenerateLockPeek = false;
            }
            else
            {
                this.GenerateLockPeek = true;
                if (ainFile.TargetVMVersion >= 200 && ainFile.TargetVMVersion <= 210)
                {
                    this.GenerateLockPeek = false;
                    this.OldGenerateLockPeek = true;
                }
            }

            if (this.ainFile.UseSimplifiedCode)
            {
                //this.BubbleDelegates = false;
                //this.BubbleFloats = false;
                //this.BubbleDummyVariables = true;
                this.AddCheckUdo = true;
                //this.EliminateShReferences = true;
                this.RefAssignmentNoDupSpInc = true;
                this.ReturnNoDupSpInc = true;
            }
            this.RefAssignmentNoDupSpInc = true;
        }

        Function currentFunction;
        public bool BubbleDummyVariables = false;
        public bool BubbleFloats = false;
        public bool ReplaceNegatedCondition = false;
        public bool BubbleDelegates = true;
        public bool AddCheckUdo = false;
        public bool RefAssignmentNoDupSpInc = false;
        public bool ReturnNoDupSpInc = false;
        public bool GenerateLockPeek = false;
        public bool OldGenerateLockPeek = false;

        int statementsToDiscard = 0;

        int isTopExpression = -1;

        int breakLabel = -1;
        int continueLabel = -1;

        int currentSwitchBlockIndex = -1;
        SwitchBlock currentSwitchBlock = null;

        /// <summary>
        /// Relates label number (key) to address (value)
        /// </summary>
        List<int> labels = new List<int>();
        /// <summary>
        /// Contains code index (key) and label number (value)
        /// </summary>
        List<KeyValuePair<int, int>> fixups = new List<KeyValuePair<int, int>>();

        Dictionary<int, int> CodelabelToLabel = new Dictionary<int, int>();

        int AddLabel(int address)
        {
            int labelNumber = labels.Count;
            labels.Add(labelNumber);
            return labelNumber;
        }

        int AddLabel()
        {
            return AddLabel(-1);
        }

        void SetLabel(int labelNumber)
        {
            this.labels[labelNumber] = this.Address;
        }

        void AddFixup(int codeAddress, int labelNumber)
        {
            fixups.Add(new KeyValuePair<int, int>(codeAddress, labelNumber));
        }

        void AddFixup(int labelNumber)
        {
            int codeAddress = (int)Code.Position;
            AddFixup(codeAddress, labelNumber);
        }

        public MemoryStream Code;
        private BinaryReader br;
        private BinaryWriter bw;

        public int Address;

        public void WriteInstruction(Instruction instruction)
        {
            bw.Write((short)instruction);
            Address += 2;
        }

        public void WriteInstruction(Instruction instruction, int value)
        {
            WriteInstruction(instruction);
            WriteIntValue(value);
        }

        public void WriteInstruction(Instruction instruction, int value, int value2)
        {
            WriteInstruction(instruction);
            WriteIntValue(value);
            WriteIntValue(value2);
        }

        public void WriteBranchInstruction(Instruction instruction, int labelNumber)
        {
            WriteInstruction(instruction);
            WriteDummyAddress(labelNumber);
        }

        private void WriteBranchInstruction(Instruction ins, int value1, int elseLabel)
        {
            WriteInstruction(ins, value1);
            WriteDummyAddress(elseLabel);
        }

        private void WriteBranchInstruction(Instruction ins, int value1, int value2, int elseLabel)
        {
            WriteInstruction(ins, value1, value2);
            WriteDummyAddress(elseLabel);
        }

        public void WriteIntValue(int value)
        {
            bw.Write((int)value);
            Address += 4;
        }

        public void WriteDummyAddress(int labelNumber)
        {
            AddFixup(labelNumber);
            WriteIntValue(0);
        }

        public void GenerateCodeForFunction(Expression functionExpression)
        {
            if (functionExpression.ExpressionType == Instruction.FUNC)
            {
                this.currentFunction = functionExpression.Variable as Function;
                Expression.defaultFunctionNumber = this.currentFunction.Index;
            }
            
            GenerateCode(functionExpression);
            
            //Fix up labels
            foreach (var pair in fixups)
            {
                int codeAddress = pair.Key;
                int labelNumber = pair.Value;
                int labelAddress = labels[labelNumber];
                bw.Write(labelAddress, codeAddress);
            }
            this.fixups.Clear();
            this.labels.Clear();
            this.CodelabelToLabel.Clear();
        }

        private void GenerateCodeForSubexpressions(Expression expression)
        {
            //output each argument
            foreach (var subExpression in expression.Args)
            {
                GenerateCode(subExpression);
            }
        }

        private void GenerateCodeForThisExpression(Expression expression)
        {
            //output this instruction
            var instruction = expression.ExpressionType;
            WriteInstruction(instruction);

            int instructionArguments = instruction.GetNumberOfWords();
            if (instructionArguments >= 1)
            {
                WriteIntValue(expression.Value);
            }
            if (instructionArguments >= 2)
            {
                WriteIntValue(expression.Value2);
            }
            if (instructionArguments >= 3)
            {
                WriteIntValue(expression.Value3);
            }
        }

        void GenerateCodeDefault(Expression expression)
        {
            GenerateCodeForSubexpressions(expression);
            GenerateCodeForThisExpression(expression);
        }

        /// <summary>
        /// Outputs code, and outputs a POP instruction
        /// </summary>
        /// <param name="expression"></param>
        void GenerateCodeForStatement(Expression expression)
        {
            this.isTopExpression = 2;
            GenerateCode(expression);
            GeneratePop(expression);
        }

        private void GeneratePop(Expression expression)
        {
            if (expression == null) return;
            if (expression.ExpressionType == Instruction.Statement) return;

            var variable = expression.Variable;
            var instruction = expression.ExpressionType;

            DataType expressionDataType = DataType.Void;
            if (variable != null)
            {
                expressionDataType = variable.DataType;
                var dataType2 = expression.GetDataType();
                if (expressionDataType.GetTypeOfRef() != dataType2.GetTypeOfRef())
                {
                    if (instruction == Instruction.SH_LOCALASSIGN ||
                        instruction == Instruction.SH_LOCALDEC ||
                        instruction == Instruction.SH_LOCALINC ||
                        instruction == Instruction.SH_LOCALCREATE ||
                        instruction == Instruction.SH_LOCALDELETE ||
                        instruction == Instruction.SH_LOCALASSIGN_SUB_IMM ||
                        instruction == Instruction.SH_LOCALREF_SASSIGN_LOCALSREF ||
                        instruction == Instruction.SH_LOCALSTRUCT_ASSIGN_IMM ||
                        instruction == Instruction.SH_LOCREF_ASSIGN_MEM ||
                        instruction == Instruction.SH_MEM_ASSIGN_IMM ||
                        instruction == Instruction.SH_MEM_ASSIGN_LOCAL ||
                        instruction == Instruction.SH_S_ASSIGN_CALLSYS19 ||
                        instruction == Instruction.SH_S_ASSIGN_REF ||
                        instruction == Instruction.SH_S_ASSIGN_STR0 ||
                        instruction == Instruction.SH_SASSIGN_GLOBALSREF ||
                        instruction == Instruction.SH_SASSIGN_LOCALSREF ||
                        instruction == Instruction.SH_SASSIGN_STRUCTSREF ||
                        instruction == Instruction.SH_SR_ASSIGN ||
                        instruction == Instruction.SH_STRUCT_ASSIGN_LOCALREF_ITOB ||
                        instruction == Instruction.SH_STRUCTREF_SASSIGN_LOCALSREF ||
                        instruction == Instruction.SH_LOCAL_ASSIGN_STRUCTREF ||
                        instruction == Instruction.SH_GLOBAL_ASSIGN_LOCAL ||
                        instruction == Instruction.SH_GLOBAL_ASSIGN_IMM
                        )
                    {
                        expressionDataType = DataType.Void;
                    }
                    else if (instruction == Instruction.FT_ASSIGNS)
                    {
                        expressionDataType = DataType.String;
                    }
                    else if (instruction == Instruction.SR_ASSIGN)
                    {
                        expressionDataType = DataType.Struct;
                        if (ainFile.TargetVMVersion >= 200 && ainFile.TargetVMVersion <= 210)
                        {
                            Instruction i1, i2;
                            i1 = expression.Arg1.ExpressionType;
                            i2 = expression.Arg2.ExpressionType;
                            if ((i1 == Instruction.SH_LOCALREF || i1 == Instruction.SH_GLOBALREF || i1 == Instruction.SH_STRUCTREF) &&
                                (i2 == Instruction.SH_LOCALREF || i2 == Instruction.SH_GLOBALREF || i2 == Instruction.SH_STRUCTREF))
                            {
                                expressionDataType = DataType.Int;
                            }
                        }
                    }
                    else if (instruction == Instruction.ASSIGN ||
                        instruction == Instruction.R_ASSIGN ||
                        instruction == Instruction.CALLFUNC2 ||
                        instruction == Instruction.DG_CALLBEGIN)
                    {

                    }
                    else if (instruction == Instruction.IfElse)
                    {
                        expressionDataType = DataType.Void;
                    }
                    else if (instruction == Instruction.RefAssignment || instruction == Instruction.RefInitializer || instruction == Instruction.RefIntAssignment || instruction == Instruction.RefIntInitializer)
                    {
                        expressionDataType = DataType.Void;
                    }
                    else if (instruction == Instruction.DG_SET)
                    {
                        expressionDataType = DataType.Void;
                    }
                }
            }
            else
            {
                expressionDataType = expression.GetDataType();
            }

            //degenerate cases - no side effect from void context
            if (instruction == Instruction.AssignmentRef)
            {
                expressionDataType = DataType.Void;
            }
            if (instruction == Instruction.PUSHSTRUCTPAGE && expression.Parent.ExpressionType == Instruction.AssignmentRef)
            {
                expressionDataType = DataType.Int;
            }

            switch (expressionDataType)
            {
                case DataType.Void:
                    break;
                case DataType.RefStruct:
                default:
                    WriteInstruction(Instruction.POP);
                    break;
                case DataType.RefString:
                    if (expression.ExpressionType == Instruction.ASSIGN)
                    {
                        WriteInstruction(Instruction.POP);
                    }
                    else
                    {
                        WriteInstruction(Instruction.S_POP);
                    }
                    break;
                case DataType.String:
                    WriteInstruction(Instruction.S_POP);
                    break;
                case DataType.Struct:
                    WriteInstruction(Instruction.SR_POP);
                    break;
                case DataType.Delegate:
                case DataType.RefDelegate:
                    WriteInstruction(Instruction.DG_POP);
                    break;
                case DataType.RefInt:
                case DataType.RefLint:
                case DataType.RefFloat:
                case DataType.RefBool:
                    if (expression.ExpressionType == Instruction.R_ASSIGN)
                    {
                        WriteInstruction(Instruction.POP);
                    }
                    WriteInstruction(Instruction.POP);
                    break;
            }
        }

        private void GenerateCode(Expression expression)
        {
            if (expression == null)
            {
                return;
            }
            this.isTopExpression--;
        nextStatement:
            var instruction = expression.ExpressionType;
            switch (instruction)
            {
                case Instruction.Statement:
                    {
                        var statementExpression = expression.Arg1;
                        var nextStatement = expression.Arg2;
                        if (statementsToDiscard > 0)
                        {
                            statementsToDiscard--;
                        }
                        else
                        {
                            GenerateCodeForStatement(statementExpression);
                        }
                        //prevent stack overflow
                        expression = nextStatement;
                        if (expression != null && expression.ExpressionType != Instruction.Statement)
                        {
                            GenerateCodeForStatement(expression);
                            return;
                        }
                        else
                        {
                            goto nextStatement;
                        }
                    }
                    break;
                case Instruction.FUNC:
                    {
                        GenerateCodeForThisExpression(expression);
                        //output each argument
                        foreach (var subExpression in expression.Args)
                        {
                            //force each argument to be a statement, since # functions are missing returns at the end
                            GenerateCodeForStatement(subExpression);
                        }

                        //if (expression.Root != null)
                        //{
                        //    var function = expression.Root.Variable;
                        //    if (function.Name.Contains('@') && !function.Name.EndsWith("@2"))   //fixme, constructors somtimes also get endfuncs?
                        //    {
                        //        //generate no endfunc
                        //    }
                        //    else
                        //    {
                        //        //generate ENDFUNC
                        //        WriteInstruction(Instruction.ENDFUNC, expression.Value);
                        //    }
                        //}
                    }
                    break;
                case Instruction.A_ALLOC:
                case Instruction.A_REALLOC:
                    {
                        GenerateCodeForSubexpressions(expression);
                        //restore value that was pushed onto the stack
                        WriteInstruction(Instruction.PUSH, expression.Value);
                        GenerateCodeForThisExpression(expression);
                    }
                    break;
                case Instruction.ITOF:
                    {
                        GenerateCodeDefault(expression);
                    }
                    break;
                case Instruction.F_ADD:
                case Instruction.F_ASSIGN:
                case Instruction.F_DIV:
                case Instruction.F_DIVA:
                case Instruction.F_EQUALE:
                case Instruction.F_GT:
                case Instruction.F_GTE:
                //case Instruction.F_INV:
                case Instruction.F_LT:
                case Instruction.F_LTE:
                case Instruction.F_MINUSA:
                case Instruction.F_MUL:
                case Instruction.F_MULA:
                case Instruction.F_NOTE:
                case Instruction.F_PLUSA:
                //case Instruction.F_PUSH:
                case Instruction.F_SUB:
                    {
                        bool generateSwaps = false;
                        if (this.BubbleFloats)
                        {
                            if (expression.Arg1 != null && expression.Arg1.ExpressionType == Instruction.ITOF)
                            {
                                //??? still not quite there...
                                if (expression.Arg2 != null && expression.Arg2.ExpressionType != Instruction.ITOF)
                                {
                                    generateSwaps = true;
                                }
                                //if (expression.Arg1.Arg1.ExpressionType == Instruction.SH_LOCALREF) //???
                                //{
                                //    generateSwaps = true;
                                //}
                            }
                        }

                        if (generateSwaps)
                        {
                            GenerateCodeForSubexpressions(expression.Arg1);
                            GenerateCode(expression.Arg2);
                            WriteInstruction(Instruction.SWAP);
                            WriteInstruction(Instruction.ITOF);
                            WriteInstruction(Instruction.SWAP);
                            WriteInstruction(expression.ExpressionType);

                        }
                        else
                        {
                            GenerateCodeDefault(expression);
                        }
                    }
                    break;

                case Instruction.RETURN:
                    {
                        var function = expression.Root.Variable;
                        if (function.DataType == DataType.RefStruct || function.DataType == DataType.RefString)
                        {
                            //if this is the last return, don't do anything special
                            var nextExpression = expression.GetParentNextExpression();
                            if (nextExpression == null || nextExpression.ExpressionType == Instruction.ENDFUNC)
                            {
                                GenerateCodeDefault(expression);
                            }
                            else
                            {
                                if (this.ReturnNoDupSpInc && expression.Arg1 != null && expression.Arg1.ExpressionType == Instruction.PUSH && expression.Arg1.Value == -1)
                                {
                                    GenerateCodeDefault(expression);
                                }
                                else
                                {
                                    //This code IS necessary to make the games work.
                                    GenerateCodeForSubexpressions(expression);
                                    WriteInstruction(Instruction.DUP);
                                    WriteInstruction(Instruction.SP_INC);
                                    GenerateCodeForThisExpression(expression);
                                }
                            }
                        }
                        else if (function.DataType.IsPrimitiveRefType())
                        {
                            if (expression.Arg1.ExpressionType == Instruction.AssignmentRef || expression.Arg1.ExpressionType == Instruction.R_ASSIGN)
                            {
                                var nextExpression = expression.Arg1.GetParentNextExpression();
                                if (nextExpression == null || nextExpression.ExpressionType == Instruction.ENDFUNC)
                                {
                                    GenerateCodeDefault(expression);
                                }
                                else
                                {
                                    GenerateCode(expression.Arg1);
                                    WriteInstruction(Instruction.DUP_U2);
                                    WriteInstruction(Instruction.SP_INC);
                                    GenerateCodeForThisExpression(expression);
                                }
                            }
                            else
                            {
                                ///???
                                GenerateCodeDefault(expression);
                            }
                        }
                        else
                        {
                            GenerateCodeDefault(expression);
                        }
                    }
                    break;
                case Instruction.ASSIGN:
                    {
                        //The game works fine without doing this, so this is just here to make it match the original file.
                        bool handled = false;
                        if (this.BubbleDummyVariables)
                        {
                            //are we assigning to a ref struct named "<dummy"?
                            var variable1 = expression.Arg1.Variable;
                            if (variable1 != null &&
                                variable1.DataType == DataType.RefStruct &&
                                variable1.Name.StartsWith("<dummy") &&
                                expression.Arg1.ExpressionType == Instruction.AssignmentRef &&
                                expression.Arg1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE &&
                                expression.Arg1.Arg2.ExpressionType == Instruction.PUSH)
                            {
                                //is previous line of code a DELETE?

                                //are we calling a method?
                                if (expression.Arg2.ExpressionType == Instruction.CALLMETHOD)
                                {
                                    var callMethodOnExpression = expression.Arg2.Arg1;
                                    Expression callMethodNextExpression = null;
                                    if (callMethodOnExpression.ExpressionType == Instruction.Comma)
                                    {
                                        callMethodNextExpression = callMethodOnExpression.Arg2;
                                        callMethodOnExpression = callMethodOnExpression.Arg1;

                                    }
                                    //output the struct we're calling the method on
                                    GenerateCode(callMethodOnExpression);
                                    if (this.AddCheckUdo)
                                    {
                                        GenerateCode(expression.Arg1.Arg1);
                                        GenerateCode(expression.Arg1.Arg2);
                                        WriteInstruction(Instruction.REF);
                                        WriteInstruction(Instruction.CHECKUDO);
                                    }

                                    //bubble it through the two instructions of the dummy reference
                                    GenerateCode(expression.Arg1.Arg1); //pushlocalpage
                                    WriteInstruction(Instruction.SWAP);
                                    GenerateCode(expression.Arg1.Arg2); //push
                                    WriteInstruction(Instruction.SWAP);
                                    //output the rest of the method arguments
                                    GenerateCode(callMethodNextExpression);
                                    //output the CALLMETHOD
                                    GenerateCodeForThisExpression(expression.Arg2);
                                    //output the ASSIGN
                                    GenerateCodeForThisExpression(expression);
                                    handled = true;
                                }
                            }
                        }


                        if (!handled)
                        {
                            GenerateCodeDefault(expression);
                        }
                    }
                    break;
                case Instruction.CALLFUNC2:
                    {
                        //This version also works, but does not match original files.
                        ////output arg3, arg1, arg2, this
                        //GenerateCode(expression.Arg3);
                        //GenerateCode(expression.Arg1);
                        //GenerateCode(expression.Arg2);
                        //GenerateCodeForThisExpression(expression);

                        //arg1 = function object
                        //arg2 = functype number (PUSH xx instruction)
                        //arg3 = functype parameters (Comma expressions)
                        //output arg1 (function object) and bubble it to the stack top
                        GenerateCode(expression.Arg1);

                        var funcType = ainFile.GetFuncType(expression.Arg2.Value);
                        if (funcType == null)
                        {
                            //TODO: handle daiteikoku later
                        }

                        //bubble the functype past each parameter up to the top of the stack
                        BubbleToTop(funcType, 0, expression.Arg3);

                        //output arg3, arg1, arg2, this
                        //GenerateCode(expression.Arg3);
                        //GenerateCode(expression.Arg1);
                        GenerateCode(expression.Arg2);
                        GenerateCodeForThisExpression(expression);
                    }
                    break;
                case Instruction.DG_CALLBEGIN:
                    {
                        GenerateCode(expression.Arg2);
                        GenerateCode(expression.Arg1);
                        GenerateCodeForThisExpression(expression);
                        int loopLabel = AddLabel();
                        int exitLabel = AddLabel();
                        SetLabel(loopLabel);
                        WriteBranchInstruction(Instruction.DG_CALL, expression.Value, exitLabel);
                        WriteBranchInstruction(Instruction.JUMP, loopLabel);
                        SetLabel(exitLabel);

                        //TODO: Write this
                        //should output this:
                        //<args>
                        //DG_CALLBEGIN <value>
                        //label:
                        //DG_CALL <value> <label2>
                        //JUMP label
                        //label2:
                    }
                    break;
                case Instruction.DG_NEW_FROM_METHOD:
                    {
                        if (this.BubbleDelegates && expression.Arg1.ExpressionType == Instruction.PUSH && expression.Arg1.Value == -1)
                        {
                            if (expression.Arg2.ExpressionType == Instruction.DG_STR_TO_METHOD)
                            {
                                GenerateCode(expression.Arg2.Arg1);
                                GenerateCode(expression.Arg1);
                                WriteInstruction(Instruction.SWAP);
                                GenerateCode(expression.Arg2.Arg2);
                                GenerateCodeForThisExpression(expression.Arg2);
                                GenerateCodeForThisExpression(expression);
                            }
                            else
                            {
                                GenerateCode(expression.Arg2);
                                GenerateCode(expression.Arg1);
                                WriteInstruction(Instruction.SWAP);
                                GenerateCodeForThisExpression(expression);
                            }
                        }
                        else
                        {
                            GenerateCodeDefault(expression);
                        }
                    }
                    break;
                case Instruction.DG_STR_TO_METHOD:
                    {
                        if (this.BubbleDelegates && expression.Arg1.ExpressionType == Instruction.PUSH && expression.Arg1.Value == -1 &&
                            expression.Arg3.ExpressionType == Instruction.PUSH)
                        {
                            GenerateCode(expression.Arg2);
                            GenerateCode(expression.Arg1);
                            WriteInstruction(Instruction.SWAP);
                            GenerateCode(expression.Arg3);
                            GenerateCodeForThisExpression(expression);
                        }
                        else
                        {
                            GenerateCodeDefault(expression);
                        }
                    }
                    break;
                case Instruction.DG_EXIST:
                    {
                        if (this.BubbleDelegates && expression.Arg2.ExpressionType == Instruction.PassThroughExpression && expression.Arg2.Arg1.Value == -1 &&
                            expression.Arg2.Arg1.ExpressionType == Instruction.PUSH)
                        {
                            GenerateCode(expression.Arg1);
                            GenerateCode(expression.Arg2.Arg2);
                            GenerateCode(expression.Arg2.Arg1);
                            WriteInstruction(Instruction.SWAP);
                            GenerateCodeForThisExpression(expression);
                        }
                        else if (this.BubbleDelegates && expression.Arg2.ExpressionType == Instruction.PUSH && expression.Arg3 != null && expression.Arg3.ExpressionType == Instruction.PUSH)
                        {
                            GenerateCode(expression.Arg1);
                            GenerateCode(expression.Arg3);
                            GenerateCode(expression.Arg2);
                            WriteInstruction(Instruction.SWAP);
                            GenerateCodeForThisExpression(expression);
                        }
                        else
                        {
                            GenerateCodeDefault(expression);
                        }
                    }
                    break;
                case Instruction.DG_ADD:
                case Instruction.DG_ERASE:
                    {
                        if (this.BubbleDelegates && expression.Arg2.ExpressionType == Instruction.PassThroughExpression && expression.Arg2.Arg1.Value == -1 &&
                            expression.Arg2.Arg1.ExpressionType == Instruction.PUSH)
                        {
                            GenerateCode(expression.Arg1);
                            GenerateCode(expression.Arg2.Arg2);
                            GenerateCode(expression.Arg2.Arg1);
                            WriteInstruction(Instruction.SWAP);
                            GenerateCodeForThisExpression(expression);
                        }
                        else if (this.BubbleDelegates && expression.Arg2.ExpressionType == Instruction.PUSH && expression.Arg2.Value == -1 && expression.Arg3 != null &&
                            (expression.Arg3.ExpressionType == Instruction.PUSH))
                        {
                            GenerateCode(expression.Arg1);
                            GenerateCode(expression.Arg3);
                            GenerateCode(expression.Arg2);
                            WriteInstruction(Instruction.SWAP);
                            GenerateCodeForThisExpression(expression);
                        }
                        else if (this.BubbleDelegates && expression.Arg2.ExpressionType == Instruction.PUSH && expression.Arg2.Value == -1 && expression.Arg3 != null &&
                            expression.Arg3.ExpressionType == Instruction.DG_STR_TO_METHOD)
                        {
                            GenerateCode(expression.Arg1);
                            GenerateCode(expression.Arg3.Arg1);
                            GenerateCode(expression.Arg2);
                            WriteInstruction(Instruction.SWAP);
                            GenerateCode(expression.Arg3.Arg2);
                            GenerateCodeForThisExpression(expression.Arg3);
                            GenerateCodeForThisExpression(expression);
                        }
                        else
                        {
                            GenerateCodeDefault(expression);
                        }
                    }
                    break;
                case Instruction.DG_ASSIGN:
                    {
                        if (expression.Arg1.ExpressionType == Instruction.AssignmentRef)
                        {
                            GenerateCode(expression.Arg1);
                            GenerateCode(expression.Arg2);
                            WriteInstruction(Instruction.DUP_X2);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.REF);
                            WriteInstruction(Instruction.SWAP);
                            GenerateCodeForThisExpression(expression);
                        }
                        else
                        {
                            GenerateCodeDefault(expression);
                        }
                    }
                    break;
                //case Instruction.SR_ASSIGN:
                //{
                //    var structExp = expression.Arg1;
                //    var assignmentRef = expression.Arg2;

                //    GenerateCode(structExp);
                //    GenerateCode(assignmentRef);
                //    GenerateCodeForThisExpression(expression);
                //}
                //break;
                case Instruction.StructAssignmentRef:
                case Instruction.ArrayIndex:
                case Instruction.Comma:
                case Instruction.PassThroughExpression:
                    {
                        if (isTopExpression <= 0)
                        {
                            GenerateCodeForSubexpressions(expression);
                        }
                        else
                        {
                            foreach (var subexpression in expression.Args)
                            {
                                GenerateCodeForStatement(subexpression);
                            }
                        }
                        //GenerateCodeForSubexpressions(expression);
                    }
                    break;
                case Instruction.AssignmentRef:
                    {
                        if (this.AddCheckUdo)
                        {
                            if (expression.Arg1 != null && expression.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && expression.Arg2 != null && expression.Arg2.ExpressionType == Instruction.PUSH)
                            {
                                var variable = expression.Variable;
                                if (variable != null && variable.Name.StartsWith("<dummy"))
                                {
                                    GenerateCodeForSubexpressions(expression);
                                    WriteInstruction(Instruction.REF);
                                    WriteInstruction(Instruction.CHECKUDO);
                                    GenerateCodeForSubexpressions(expression);
                                    break;
                                }
                            }
                        }


                        if (isTopExpression <= 0)
                        {
                            GenerateCodeForSubexpressions(expression);
                        }
                        else
                        {
                            foreach (var subexpression in expression.Args)
                            {
                                GenerateCodeForStatement(subexpression);
                            }
                        }
                    }
                    break;
                case Instruction.Break:
                    {
                        if (breakLabel != -1)
                        {
                            WriteBranchInstruction(Instruction.JUMP, breakLabel);
                        }
                        else
                        {
                            //error
                        }
                    }
                    break;
                case Instruction.Continue:
                    {
                        if (continueLabel != -1)
                        {
                            WriteBranchInstruction(Instruction.JUMP, continueLabel);
                        }
                        else
                        {
                            //error
                        }
                    }
                    break;
                case Instruction.IfElse:
                    {
                        bool isQuestionColon = this.isTopExpression <= 0;

                        //TODO: make it distingush between value returning form "? :" and void form "if else".
                        int elseLabel = AddLabel();
                        int endifLabel = AddLabel();

                        var condition = expression.Arg1;
                        var ifBody = expression.Arg2;
                        var elseBlock = expression.Arg3;

                        bool handled;

                        GenerateVersion6Condition(condition, Instruction.IFZ, elseLabel);

                        if (isQuestionColon)
                        {
                            GenerateCode(ifBody);
                        }
                        else
                        {
                            GenerateCodeForStatement(ifBody);
                        }

                        if (ainFile.Version >= 6 && expression.Arg3 == null)
                        {
                            //suppress JUMP at end of if-block for version 6
                        }
                        else
                        {
                            WriteBranchInstruction(Instruction.JUMP, endifLabel);
                        }

                        SetLabel(elseLabel);

                        if (isQuestionColon)
                        {
                            GenerateCode(elseBlock);
                        }
                        else
                        {
                            GenerateCodeForStatement(elseBlock);
                        }

                        SetLabel(endifLabel);
                    }
                    break;
                case Instruction.LogicalAnd:
                    {
                        var firstCondition = expression.Arg1;
                        var secondCondition = expression.Arg2;

                        int eitherFalseLabel = AddLabel();
                        int afterBothTrueLabel = AddLabel();

                        GenerateVersion6Condition(firstCondition, Instruction.IFZ, eitherFalseLabel);
                        GenerateVersion6Condition(secondCondition, Instruction.IFZ, eitherFalseLabel);

                        WriteInstruction(Instruction.PUSH, 1);
                        WriteBranchInstruction(Instruction.JUMP, afterBothTrueLabel);

                        SetLabel(eitherFalseLabel);

                        WriteInstruction(Instruction.PUSH, 0);

                        SetLabel(afterBothTrueLabel);
                    }
                    break;
                case Instruction.LogicalOr:
                    {
                        //fixme
                        var firstCondition = expression.Arg1;
                        var secondCondition = expression.Arg2;

                        int eitherTrueLabel = AddLabel();
                        int afterBothFalseLabel = AddLabel();

                        GenerateVersion6Condition(firstCondition, Instruction.IFNZ, eitherTrueLabel);
                        GenerateVersion6Condition(secondCondition, Instruction.IFNZ, eitherTrueLabel);

                        WriteInstruction(Instruction.PUSH, 0);
                        WriteBranchInstruction(Instruction.JUMP, afterBothFalseLabel);

                        SetLabel(eitherTrueLabel);

                        WriteInstruction(Instruction.PUSH, 1);

                        SetLabel(afterBothFalseLabel);
                    }
                    break;
                case Instruction.Label:
                    {
                        int labelNumber;
                        if (CodelabelToLabel.ContainsKey(expression.Value))
                        {
                            labelNumber = CodelabelToLabel[expression.Value];
                        }
                        else
                        {
                            labelNumber = AddLabel();
                            CodelabelToLabel.Add(expression.Value, labelNumber);
                        }
                        labels[labelNumber] = Address;
                    }
                    break;
                case Instruction.Goto:
                    {
                        int labelNumber;
                        if (CodelabelToLabel.ContainsKey(expression.Value))
                        {
                            labelNumber = CodelabelToLabel[expression.Value];
                        }
                        else
                        {
                            labelNumber = AddLabel();
                            CodelabelToLabel.Add(expression.Value, labelNumber);
                        }
                        WriteInstruction(Instruction.JUMP);
                        WriteDummyAddress(labelNumber);
                    }
                    break;
                case Instruction.ForLoop:
                    {
                        int oldBreakLabel = breakLabel;
                        int oldContinueLabel = continueLabel;

                        //Init: ...
                        //Cond: ...
                        //IFZ After
                        //JUMP Body
                        //Inc: ...
                        // JUMP Cond
                        //Body: ... 
                        // JUMP Inc
                        //After:

                        var initializer = expression.Arg1;
                        var condition = expression.Arg2;
                        var increment = expression.Arg3;
                        var loopBody = expression.Arg4;

                        var previousStatement = expression.Parent;
                        if (previousStatement != null && previousStatement.Arg1 == expression)
                        {
                            previousStatement = previousStatement.Parent;
                        }
                        if (previousStatement != null && previousStatement.ExpressionType == Instruction.Statement)
                        {
                            previousStatement = previousStatement.Arg1;
                            if (previousStatement != null && previousStatement.ExpressionType == Instruction.SH_LOCALASSIGN)
                            {
                                if (initializer != null && initializer.ExpressionType == Instruction.SH_LOCALASSIGN)
                                {
                                    if (previousStatement.Value == initializer.Value && previousStatement.Value2 == 0)
                                    {
                                        bw.BaseStream.Position -= 10;
                                        this.Address -= 10;
                                    }
                                }
                            }
                            else if (previousStatement != null && previousStatement.ExpressionType == Instruction.ASSIGN)
                            {
                                if (initializer != null && initializer.ExpressionType == Instruction.ASSIGN)
                                {
                                    var initializerVariable = initializer.Arg1.Variable;
                                    if (initializerVariable != null) { initializerVariable = initializerVariable.Canonicalize(); }
                                    var previousVariable = previousStatement.Arg1.Variable;
                                    if (previousVariable != null) { previousVariable = previousVariable.Canonicalize(); }
                                    if (initializerVariable != null && previousVariable != null &&
                                        initializerVariable == previousVariable &&
                                        initializerVariable.Parent == currentFunction)
                                    {
                                        if (previousStatement.Arg1.ExpressionType == Instruction.AssignmentRef &&
                                            previousStatement.Arg1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE &&
                                            previousStatement.Arg1.Arg2.ExpressionType == Instruction.PUSH &&
                                            previousStatement.Arg2.ExpressionType == Instruction.PUSH &&
                                            previousStatement.Arg2.Value == 0)
                                        {
                                            bw.BaseStream.Position -= 18;
                                            this.Address -= 18;
                                        }
                                    }

                                }
                            }
                        }

                        int conditionLabel = AddLabel();
                        int incrementLabel = AddLabel();
                        int loopBodyLabel = AddLabel();
                        int afterLabel = AddLabel();

                        breakLabel = afterLabel;
                        continueLabel = incrementLabel;

                        GenerateCodeForStatement(initializer);

                        SetLabel(conditionLabel);

                        bool isOldVersion = this.ainFile.TargetVMVersion >= 200 && this.ainFile.TargetVMVersion <= 210;

                        if (isOldVersion || condition != null || increment != null)
                        {
                            if (condition != null)
                            {
                                GenerateVersion6Condition(condition, Instruction.IFZ, afterLabel);
                            }
                            WriteBranchInstruction(Instruction.JUMP, loopBodyLabel);

                            SetLabel(incrementLabel);

                            GenerateCodeForStatement(increment);
                            WriteBranchInstruction(Instruction.JUMP, conditionLabel);
                        }
                        else
                        {
                            SetLabel(incrementLabel);
                        }

                        SetLabel(loopBodyLabel);

                        GenerateCodeForStatement(loopBody);
                        WriteBranchInstruction(Instruction.JUMP, incrementLabel);

                        SetLabel(afterLabel);

                        breakLabel = oldBreakLabel;
                        continueLabel = oldContinueLabel;
                    }
                    break;
                case Instruction.WhileLoop:
                    {
                        int oldBreakLabel = breakLabel;
                        int oldContinueLabel = continueLabel;

                        //<first>
                        //ifz <target>
                        //...
                        //jump <before first>
                        //<target>:

                        var condition = expression.Arg1;
                        var loopBody = expression.Arg2;

                        int conditionLabel = AddLabel();
                        //int loopBodyLabel = AddLabel();
                        int afterLabel = AddLabel();

                        continueLabel = conditionLabel;
                        breakLabel = afterLabel;

                        SetLabel(conditionLabel);
                        GenerateVersion6Condition(condition, Instruction.IFZ, afterLabel);

                        GenerateCodeForStatement(loopBody);

                        WriteBranchInstruction(Instruction.JUMP, conditionLabel);

                        SetLabel(afterLabel);

                        breakLabel = oldBreakLabel;
                        continueLabel = oldContinueLabel;
                    }
                    break;
                case Instruction.DoWhileLoop:
                    {
                        //DoWhileLoop first appeared in evenicle?  (maybe earlier?)
                        int oldBreakLabel = breakLabel;
                        int oldContinueLabel = continueLabel;

                        //loop_label:
                        //<loopBody>
                        //condition_label: (continue address)
                        //<condition>
                        //ifz loop_label
                        //break_label:

                        var condition = expression.Arg1;
                        var loopBody = expression.Arg2;

                        int conditionLabel = AddLabel();
                        int loopLabel = AddLabel();
                        int afterLabel = AddLabel();

                        continueLabel = conditionLabel;
                        breakLabel = afterLabel;

                        SetLabel(loopLabel);

                        GenerateCodeForStatement(loopBody);

                        SetLabel(conditionLabel);

                        GenerateVersion6Condition(condition, Instruction.IFNZ, loopLabel);

                        SetLabel(afterLabel);

                        breakLabel = oldBreakLabel;
                        continueLabel = oldContinueLabel;
                    }
                    break;
                case Instruction.SWITCH:
                case Instruction.STRSWITCH:
                    {
                        int oldBreakLabel = breakLabel;
                        int oldSwitchBlockIndex = currentSwitchBlockIndex;
                        SwitchBlock oldSwitchBlock = currentSwitchBlock;

                        currentSwitchBlockIndex = ainFile.Switches.Count;
                        currentSwitchBlock = new SwitchBlock();
                        currentSwitchBlock.CaseType2or4 = 2;
                        currentSwitchBlock.Index = currentSwitchBlockIndex;
                        currentSwitchBlock.Root = ainFile;
                        currentSwitchBlock.DefaultCaseAddress = -1;
                        ainFile.Switches.Add(currentSwitchBlock);

                        var switchValue = expression.Arg1;
                        var switchCase = expression.Arg2;
                        var switchVariaible = switchValue.Variable;
                        if (switchVariaible != null && switchVariaible.DataType.IsString())
                        {
                            currentSwitchBlock.CaseType2or4 = 4;
                        }

                        int afterLabel = AddLabel();
                        breakLabel = afterLabel;

                        GenerateCode(switchValue);
                        if (currentSwitchBlock.CaseType2or4 != 4)
                        {
                            WriteInstruction(Instruction.SWITCH);
                        }
                        else
                        {
                            WriteInstruction(Instruction.STRSWITCH);
                        }
                        WriteIntValue(currentSwitchBlockIndex);
                        WriteBranchInstruction(Instruction.JUMP, afterLabel);

                        GenerateCode(switchCase);

                        SetLabel(afterLabel);

                        currentSwitchBlock = oldSwitchBlock;
                        currentSwitchBlockIndex = oldSwitchBlockIndex;
                        breakLabel = oldBreakLabel;
                    }
                    break;
                case Instruction.DefaultSwitchCase:
                case Instruction.SwitchCase:
                    {
                        if (currentSwitchBlock == null)
                        {
                            //error
                        }

                        if (instruction == Instruction.DefaultSwitchCase)
                        {
                            currentSwitchBlock.DefaultCaseAddress = Address;
                        }
                        else
                        {
                            var newSwitchCase = new SwitchCase();
                            newSwitchCase.Parent = currentSwitchBlock;
                            newSwitchCase.Index = currentSwitchBlock.SwitchCases.Count;
                            newSwitchCase.Root = ainFile;
                            newSwitchCase.TargetAddress = Address;
                            newSwitchCase.Value = expression.Value;
                            currentSwitchBlock.SwitchCases.Add(newSwitchCase);
                        }

                        var caseBody = expression.Arg1;
                        var nextCase = expression.Arg2;

                        GenerateCodeForStatement(caseBody);
                        GenerateCode(nextCase);
                    }
                    break;
                case Instruction.Nop:
                    break;
                case Instruction.INC:
                case Instruction.LI_INC:
                case Instruction.DEC:
                case Instruction.LI_DEC:
                    {
                        if (expression.Arg1.ExpressionType == Instruction.REF)
                        {
                            GenerateCodeForSubexpressions(expression.Arg1);
                        }
                        else
                        {
                            GenerateCode(expression.Arg1);
                        }
                        WriteInstruction(Instruction.DUP2);  //duplicate page/index pair
                        WriteInstruction(Instruction.REF);   //read variable
                        WriteInstruction(Instruction.DUP_X2); //copy page/index pair and variaible contents
                        WriteInstruction(Instruction.POP);   //remove variaible that was read
                        WriteInstruction(instruction);       //do the operation
                    }
                    break;
                case Instruction.PreDecrement:
                case Instruction.PreIncrement:
                    {
                        if (expression.Arg1.ExpressionType == Instruction.REF)
                        {
                            GenerateCodeForSubexpressions(expression.Arg1);
                        }
                        else
                        {
                            GenerateCode(expression.Arg1);
                        }
                        var variable = expression.Arg1.Variable;
                        if (instruction == Instruction.PreIncrement)
                        {
                            if (variable.DataType == DataType.Lint || variable.DataType == DataType.RefLint)
                            {
                                instruction = Instruction.LI_INC;
                            }
                            else
                            {
                                instruction = Instruction.INC;
                            }
                        }
                        else
                        {
                            if (variable.DataType == DataType.Lint || variable.DataType == DataType.RefLint)
                            {
                                instruction = Instruction.LI_DEC;
                            }
                            else
                            {
                                instruction = Instruction.DEC;
                            }
                        }
                        WriteInstruction(Instruction.DUP2);
                        WriteInstruction(instruction);
                        var parent = expression.Parent ?? Expression.Empty;
                        var parentIns = parent.ExpressionType;
                        if ((parentIns == Instruction.ASSIGN && parent.Arg1 == expression) || parentIns == Instruction.AssignmentRef)
                        {
                            //bizarre assignment made to inc/dec operator, do nothing
                        }
                        else if (!expression.IsVoidContext())
                        {
                            WriteInstruction(Instruction.REF);
                        }
                        else
                        {
                            WriteInstruction(Instruction.POP);
                        }
                    }
                    break;
                case Instruction.RefIntInitializer:
                    {
                        if (expression.Arg2 != null)
                        {
                            if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                            GenerateCode(expression.Arg1);
                            if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                            WriteInstruction(Instruction.DUP2);
                            WriteInstruction(Instruction.REFREF);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.DELETE);
                            WriteInstruction(Instruction.DUP2);
                            GenerateCode(expression.Arg2);
                            WriteInstruction(Instruction.R_ASSIGN);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.REF);
                            WriteInstruction(Instruction.SP_INC);
                            if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //unlockpeek
                        }
                        else
                        {
                            if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                            GenerateCode(expression.Arg1);
                            if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                            WriteInstruction(Instruction.DUP2);
                            WriteInstruction(Instruction.REFREF);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.DELETE);
                            WriteInstruction(Instruction.PUSH, -1);
                            WriteInstruction(Instruction.PUSH, 0);
                            WriteInstruction(Instruction.R_ASSIGN);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.POP);
                            if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //unlockpeek
                        }
                    }
                    break;
                case Instruction.RefInitializer:
                    {
                        if (expression.Arg2 != null)
                        {
                            if (expression.Value == 0)
                            {
                                //ref = ref
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                GenerateCode(expression.Arg1);
                                if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                WriteInstruction(Instruction.DUP2);
                                WriteInstruction(Instruction.REFREF);
                                WriteInstruction(Instruction.POP);
                                WriteInstruction(Instruction.DELETE);
                                WriteInstruction(Instruction.DUP2);
                                GenerateCode(expression.Arg2);
                                WriteInstruction(Instruction.R_ASSIGN);
                                WriteInstruction(Instruction.POP);
                                WriteInstruction(Instruction.POP);
                                WriteInstruction(Instruction.REF);
                                WriteInstruction(Instruction.SP_INC);
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //unlockpeek
                            }
                            else if (expression.Value == 1)
                            {
                                //ref = object
                                if (ainFile.TargetVMVersion >= 200 && ainFile.TargetVMVersion <= 210)
                                {
                                    //for old versions (like Daibanchou), use a normal ref assignment instead of a ref initialzation
                                    GenerateCode(expression.Arg1);
                                    WriteInstruction(Instruction.CALLSYS, 3); //lockpeek
                                    WriteInstruction(Instruction.POP);
                                    WriteInstruction(Instruction.DUP2);
                                    WriteInstruction(Instruction.REF);
                                    WriteInstruction(Instruction.DELETE);
                                    WriteInstruction(Instruction.CALLSYS, 4); //unlockpeek
                                    WriteInstruction(Instruction.POP);
                                    GenerateCode(expression.Arg2);
                                    WriteInstruction(Instruction.CALLSYS, 3); //lockpeek
                                    WriteInstruction(Instruction.POP);
                                    WriteInstruction(Instruction.DUP);
                                    WriteInstruction(Instruction.SP_INC);
                                    WriteInstruction(Instruction.ASSIGN);
                                    WriteInstruction(Instruction.POP);
                                    WriteInstruction(Instruction.CALLSYS, 4); //unlockpeek
                                    WriteInstruction(Instruction.POP);
                                }
                                else
                                {
                                    if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); }  //lockpeek
                                    GenerateCode(expression.Arg1);
                                    WriteInstruction(Instruction.DUP2);
                                    WriteInstruction(Instruction.REF);
                                    WriteInstruction(Instruction.DELETE);
                                    WriteInstruction(Instruction.DUP2);
                                    GenerateCode(expression.Arg2);
                                    WriteInstruction(Instruction.ASSIGN);
                                    WriteInstruction(Instruction.DUP_X2);
                                    WriteInstruction(Instruction.POP);
                                    WriteInstruction(Instruction.REF);
                                    WriteInstruction(Instruction.SP_INC);
                                    WriteInstruction(Instruction.POP);
                                    if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); }  //unlockpeek
                                }
                            }
                            else if (expression.Value == 2)
                            {
                                //explicitly assigning the value of NULL in the variable declaration
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                GenerateCode(expression.Arg1);
                                if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                WriteInstruction(Instruction.DUP2);
                                WriteInstruction(Instruction.REF);
                                WriteInstruction(Instruction.DELETE);
                                WriteInstruction(Instruction.DUP2);
                                GenerateCode(expression.Arg2);
                                WriteInstruction(Instruction.ASSIGN);
                                WriteInstruction(Instruction.POP);
                                WriteInstruction(Instruction.POP);
                                WriteInstruction(Instruction.POP);
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //unlockpeek
                            }
                            else if (expression.Value == 3)
                            {
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                //old form from Daibanchou, released System 4.0 compiler doesn't generate this anymore
                                GenerateCode(expression.Arg1);
                                if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                WriteInstruction(Instruction.DUP2);
                                WriteInstruction(Instruction.REF);
                                WriteInstruction(Instruction.DELETE);
                                //WriteInstruction(Instruction.CALLSYS, 4);
                                //WriteInstruction(Instruction.POP);
                                if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //lockpeek
                                GenerateCode(expression.Arg2);
                                WriteInstruction(Instruction.ASSIGN);
                                WriteInstruction(Instruction.POP);
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //unlockpeek
                            }
                            else if (expression.Value == 4)
                            {
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                GenerateCode(expression.Arg1);
                                if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                WriteInstruction(Instruction.DUP2);
                                WriteInstruction(Instruction.REF);
                                WriteInstruction(Instruction.DELETE);
                                GenerateCode(expression.Arg2);
                                WriteInstruction(Instruction.ASSIGN);
                                WriteInstruction(Instruction.DUP);
                                WriteInstruction(Instruction.SP_INC);
                                WriteInstruction(Instruction.ASSIGN);
                                WriteInstruction(Instruction.POP);
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //unlockpeek
                            }
                        }
                        else
                        {
                            if (expression.Value != 2)
                            {
                                //not explicitly assigning NULL in the variable declaration

                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                GenerateCode(expression.Arg1);
                                if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                WriteInstruction(Instruction.DUP2);
                                WriteInstruction(Instruction.REF);
                                WriteInstruction(Instruction.DELETE);
                                if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //unlockpeek
                                WriteInstruction(Instruction.PUSH, -1);
                                WriteInstruction(Instruction.ASSIGN);
                                WriteInstruction(Instruction.POP);
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //unlockpeek
                            }
                            else
                            {
                                //explicitly assigning the value of NULL in the variable declaration

                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                GenerateCode(expression.Arg1);
                                if (OldGenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); } //lockpeek
                                WriteInstruction(Instruction.DUP2);
                                WriteInstruction(Instruction.REF);
                                WriteInstruction(Instruction.DELETE);
                                WriteInstruction(Instruction.DUP2);
                                WriteInstruction(Instruction.PUSH, -1);
                                WriteInstruction(Instruction.ASSIGN);
                                WriteInstruction(Instruction.POP);
                                WriteInstruction(Instruction.POP);
                                WriteInstruction(Instruction.POP);
                                if (GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); } //unlockpeek
                            }
                        }
                    }
                    break;
                case Instruction.RefIntAssignment:
                    {
                        if (expression.Arg2.ExpressionType == Instruction.PUSH && expression.Arg2.Value == -1)
                        {
                            GenerateCode(expression.Arg1);
                            if (this.GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); }  //lockpeek
                            WriteInstruction(Instruction.DUP2);
                            WriteInstruction(Instruction.REFREF);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.DELETE);
                            GenerateCode(expression.Arg2);
                            WriteInstruction(Instruction.PUSH, 0);
                            WriteInstruction(Instruction.R_ASSIGN);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.POP);
                            if (this.GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); }  //unlockpeek
                        }
                        else
                        {
                            GenerateCode(expression.Arg1);
                            if (this.GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); }  //lockpeek
                            WriteInstruction(Instruction.DUP2);
                            WriteInstruction(Instruction.REFREF);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.DELETE);
                            GenerateCode(expression.Arg2);
                            WriteInstruction(Instruction.DUP_U2);
                            WriteInstruction(Instruction.SP_INC);
                            WriteInstruction(Instruction.R_ASSIGN);
                            WriteInstruction(Instruction.POP);
                            WriteInstruction(Instruction.POP);
                            if (this.GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); }  //lockpeek
                        }
                    }
                    break;
                case Instruction.RefAssignment:
                    {
                        if (ainFile.TargetVMVersion >= 200 && ainFile.TargetVMVersion <= 210)
                        {
                            //is this a dummy assignment?
                            Expression e2 = expression.Arg2 ?? Expression.Empty;
                            IVariable v2 = (e2 != null) ? e2.Variable : null;
                            bool isDummyAssignment = false;
                            if (v2 != null && v2.Name.StartsWith("<dummy"))
                            {
                                isDummyAssignment = true;
                            }

                            //for old versions
                            GenerateCode(expression.Arg1);

                            bool oldBubbleDummyVariables = this.BubbleDummyVariables;
                            if (isDummyAssignment)
                            {
                                WriteInstruction(Instruction.CALLSYS, 3); //lockpeek
                                WriteInstruction(Instruction.POP);

                                this.BubbleDummyVariables = true;
                            }

                            WriteInstruction(Instruction.DUP2);
                            WriteInstruction(Instruction.REF);
                            WriteInstruction(Instruction.DELETE);

                            GenerateCode(expression.Arg2);

                            WriteInstruction(Instruction.DUP);
                            WriteInstruction(Instruction.SP_INC);
                            WriteInstruction(Instruction.ASSIGN);
                            WriteInstruction(Instruction.POP);

                            if (isDummyAssignment)
                            {
                                WriteInstruction(Instruction.CALLSYS, 4); //unlockpeek
                                WriteInstruction(Instruction.POP);
                            }
                            this.BubbleDummyVariables = oldBubbleDummyVariables;
                        }
                        else
                        {
                            GenerateCode(expression.Arg1);

                            if (this.GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 3); WriteInstruction(Instruction.POP); }  //lockpeek

                            WriteInstruction(Instruction.DUP2);
                            WriteInstruction(Instruction.REF);
                            WriteInstruction(Instruction.DELETE);
                            GenerateCode(expression.Arg2);
                            if (RefAssignmentNoDupSpInc && expression.Arg2.ExpressionType == Instruction.PUSH && expression.Arg2.Value == -1)
                            {
                                //needs confirmation
                            }
                            else
                            {
                                WriteInstruction(Instruction.DUP);
                                WriteInstruction(Instruction.SP_INC);
                            }
                            WriteInstruction(Instruction.ASSIGN);
                            WriteInstruction(Instruction.POP);

                            if (this.GenerateLockPeek) { WriteInstruction(Instruction.CALLSYS, 4); WriteInstruction(Instruction.POP); }  //unlockpeek
                        }
                    }
                    break;
                default:
                    if (instruction < 0)
                    {
                        //error
                    }
                    else
                    {
                        GenerateCodeDefault(expression);
                    }
                    break;
            }
        }

        private static bool ContainsArrayIndex(Expression baseExpression)
        {
            bool containsArrayIndex = false;
            foreach (var expr in baseExpression.GetChildExpressions())
            {
                var v = expr.Variable;
                if (v != null && v.DataType == DataType.ArrayStruct || v.DataType == DataType.RefArrayStruct)
                {
                    containsArrayIndex = true;
                    break;
                }
            }
            return containsArrayIndex;
        }

        private bool GenerateVersion6Condition(Expression condition, Instruction branchInstruction, int elseLabel)
        {
            bool handled = false;
            if (condition != null && ainFile.Version >= 6 && !ainFile.UseSimplifiedCode)
            {
                var e1 = condition.Arg1;
                var e2 = condition.Arg2;
                if (e1 != null && e2 != null)
                {
                    //var v1 = e1.Variable.Canonicalize();
                    //var v2 = e2.Variable.Canonicalize();
                    if (e1.ExpressionType == Instruction.SH_LOCALREF && e2.ExpressionType == Instruction.PUSH)
                    {
                        Instruction ins = Instruction.Nop;

                        switch (condition.ExpressionType)
                        {
                            case Instruction.LT:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_LOC_GE_IMM : Instruction.SH_IF_LOC_LT_IMM;
                                break;
                            case Instruction.LTE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_LOC_GT_IMM : Instruction.Nop;
                                break;
                            case Instruction.GT:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.Nop : Instruction.SH_IF_LOC_GT_IMM;
                                break;
                            case Instruction.GTE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_LOC_LT_IMM : Instruction.SH_IF_LOC_GE_IMM;
                                break;
                            case Instruction.EQUALE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_LOC_NE_IMM : Instruction.Nop;
                                break;
                            case Instruction.NOTE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.Nop : Instruction.SH_IF_LOC_NE_IMM;
                                break;
                        }
                        if (ins != Instruction.Nop /*&& e2.Value != -1*/)
                        {
                            WriteBranchInstruction(ins, e1.Value, e2.Value, elseLabel);
                            handled = true; ;
                        }
                    }
                    else if (e1.ExpressionType == Instruction.PUSH && e2.ExpressionType == Instruction.SH_LOCALREF)
                    {
                        Instruction ins = Instruction.Nop;

                        switch (condition.ExpressionType)
                        {
                            case Instruction.LT:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.Nop : Instruction.SH_IF_LOC_GT_IMM;
                                break;
                            case Instruction.LTE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_LOC_LT_IMM : Instruction.SH_IF_LOC_GE_IMM;
                                break;
                            case Instruction.GT:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_LOC_GE_IMM : Instruction.SH_IF_LOC_LT_IMM;
                                break;
                            case Instruction.GTE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_LOC_GT_IMM : Instruction.Nop;
                                break;
                            case Instruction.EQUALE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_LOC_NE_IMM : Instruction.Nop;
                                break;
                            case Instruction.NOTE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.Nop : Instruction.SH_IF_LOC_NE_IMM;
                                break;
                        }
                        if (ins != Instruction.Nop /*&& e1.Value != -1*/)
                        {
                            WriteBranchInstruction(ins, e2.Value, e1.Value, elseLabel);
                            handled = true;
                        }
                    }
                    else if (e1.ExpressionType == Instruction.SH_STRUCTREF && e2.ExpressionType == Instruction.PUSH)
                    {
                        Instruction ins = Instruction.Nop;

                        switch (condition.ExpressionType)
                        {
                            case Instruction.LT:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.Nop : Instruction.Nop;
                                break;
                            case Instruction.LTE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_STRUCTREF_GT_IMM : Instruction.Nop;
                                break;
                            case Instruction.GT:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.Nop : Instruction.SH_IF_STRUCTREF_GT_IMM;
                                break;
                            case Instruction.GTE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.Nop : Instruction.Nop;
                                break;
                            case Instruction.EQUALE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_STRUCTREF_NE_IMM : Instruction.SH_IF_STRUCTREF_EQ_IMM;
                                break;
                            case Instruction.NOTE:
                                ins = (branchInstruction == Instruction.IFZ) ? Instruction.SH_IF_STRUCTREF_EQ_IMM : Instruction.SH_IF_STRUCTREF_NE_IMM;
                                break;
                        }
                        if (ins != Instruction.Nop /*&& e2.Value != -1*/)
                        {
                            WriteBranchInstruction(ins, e1.Value, e2.Value, elseLabel);
                            handled = true;
                        }
                    }
                    else if (e1.ExpressionType == Instruction.SH_STRUCTREF && e2.ExpressionType == Instruction.SH_LOCALREF && condition.ExpressionType == Instruction.EQUALE && branchInstruction == Instruction.IFZ)
                    {
                        WriteBranchInstruction(Instruction.SH_IF_STRUCTREF_NE_LOCALREF, e1.Value, e2.Value, elseLabel);
                        handled = true;
                    }
                    else if (e1.ExpressionType == Instruction.SH_LOCALREF && e2.ExpressionType == Instruction.SH_STRUCTREF && condition.ExpressionType == Instruction.EQUALE && branchInstruction == Instruction.IFZ)
                    {
                        WriteBranchInstruction(Instruction.SH_IF_STRUCTREF_NE_LOCALREF, e2.Value, e1.Value, elseLabel);
                        handled = true;
                    }
                    else if ((condition.ExpressionType == Instruction.S_EQUALE && e1.ExpressionType == Instruction.S_REF && e2.ExpressionType == Instruction.S_PUSH && branchInstruction == Instruction.IFZ) ||
                        (condition.ExpressionType == Instruction.S_NOTE && e1.ExpressionType == Instruction.S_REF && e2.ExpressionType == Instruction.S_PUSH && branchInstruction == Instruction.IFNZ))
                    {
                        GenerateCodeForSubexpressions(e1);
                        WriteBranchInstruction(Instruction.SH_IF_SREF_NE_STR0, e2.Value, elseLabel);
                        handled = true;
                    }
                    else if ((condition.ExpressionType == Instruction.S_EQUALE && e1.ExpressionType == Instruction.S_REF2 && e2.ExpressionType == Instruction.S_PUSH && branchInstruction == Instruction.IFZ) ||
                        (condition.ExpressionType == Instruction.S_NOTE && e1.ExpressionType == Instruction.S_REF2 && e2.ExpressionType == Instruction.S_PUSH && branchInstruction == Instruction.IFNZ))
                    {
                        GenerateCodeForSubexpressions(e1);
                        WriteInstruction(Instruction.PUSH, e1.Value);
                        WriteBranchInstruction(Instruction.SH_IF_SREF_NE_STR0, e2.Value, elseLabel);
                        handled = true;
                    }

                }
                if (condition.ExpressionType == Instruction.SH_STRUCTREF_GT_IMM)
                {
                    if (branchInstruction == Instruction.IFNZ)
                    {
                        WriteBranchInstruction(Instruction.SH_IF_STRUCTREF_GT_IMM, condition.Value, condition.Value2, elseLabel);
                        handled = true;
                    }
                }
                if (condition.ExpressionType == Instruction.SH_STRUCTREF && (condition.Variable.DataType == DataType.Bool || condition.Variable.DataType == DataType.Int) && branchInstruction == Instruction.IFZ)
                {
                    WriteBranchInstruction(Instruction.SH_IF_STRUCTREF_Z, condition.Value, elseLabel);
                    handled = true;
                }
                else if (branchInstruction == Instruction.IFZ && condition.ExpressionType == Instruction.A_EMPTY && e1.ExpressionType == Instruction.AssignmentRef && e1.Arg1.ExpressionType == Instruction.PUSHSTRUCTPAGE && e1.Arg2.ExpressionType == Instruction.PUSH && e1.Variable.DataType.IsArray())
                {
                    WriteBranchInstruction(Instruction.SH_IF_STRUCT_A_NOT_EMPTY, e1.Arg2.Value, elseLabel);
                    handled = true;
                }


                if (condition.ExpressionType == Instruction.NOT && this.ReplaceNegatedCondition)
                {
                    condition = condition.Clone();
                    Expression.Negate(ref condition);
                    GenerateCode(condition);
                    if (branchInstruction == Instruction.IFZ)
                    {
                        WriteBranchInstruction(Instruction.IFNZ, elseLabel);
                    }
                    else
                    {
                        WriteBranchInstruction(Instruction.IFZ, elseLabel);
                    }
                    handled = true;
                }
            }

            if (!handled)
            {
                GenerateCode(condition);
                WriteBranchInstruction(branchInstruction, elseLabel);
            }
            return handled;
        }

        private void BubbleToTop(IFunction funcType, int parameterIndex, Expression expression)
        {
            if (expression == null) return;
            if (funcType == null) return;
            if (parameterIndex < 0 || parameterIndex >= funcType.ParameterCount) return;
            var parameter = funcType.Parameters[parameterIndex];
            Expression argumentExpression = expression;
            Expression nextExpression = null;
            if (argumentExpression.ExpressionType == Instruction.Comma)
            {
                argumentExpression = expression.Arg1;
                nextExpression = expression.Arg2;
            }

            if (parameter.DataType.IsPrimitiveRefType())
            {
                GenerateCode(argumentExpression);
                WriteInstruction(Instruction.DUP2_X1);
                WriteInstruction(Instruction.POP);
                WriteInstruction(Instruction.POP);
                parameterIndex += 2;
            }
            else
            {
                GenerateCode(argumentExpression);
                WriteInstruction(Instruction.SWAP);
                parameterIndex++;
            }
            BubbleToTop(funcType, parameterIndex, nextExpression);
        }
    }

    public static class MemoryStreamExtensions
    {
        public static void Write(this BinaryWriter bw, int value, int address)
        {
            long lastPosition = bw.BaseStream.Position;
            bw.BaseStream.Position = address;
            bw.Write(value);
            bw.BaseStream.Position = lastPosition;
        }

        public static int ReadInt32(this BinaryReader br, int address)
        {
            long lastPosition = br.BaseStream.Position;
            br.BaseStream.Position = address;
            int value = br.ReadInt32();
            br.BaseStream.Position = lastPosition;
            return value;
        }
    }
}
