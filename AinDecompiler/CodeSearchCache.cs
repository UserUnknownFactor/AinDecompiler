using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    public class CodeSearchCache
    {
        AinFile ainFile;
        CodeSearcher codeSearcher;
        public CodeSearchCache(AinFile ainFile)
        {
            this.ainFile = ainFile;
            this.codeSearcher = new CodeSearcher(ainFile);
            this.FunctionsCache = new Cache<Function>(ainFile, this.GetCalls2);
            this.GlobalsCache = new AllMatchesCache<Global>(ainFile, this.AddGlobals);
            this.IntsCache = new AllMatchesCache<int>(ainFile, this.AddInts);
            this.StringsCache = new AllMatchesCache<string>(ainFile, this.AddStrings);
            this.MessagesCache = new AllMatchesCache<string>(ainFile, this.AddMessages);
            this.LibraryCallsCache = new AllMatchesCache<HllFunction>(ainFile, this.AddLibraryCalls);
            this.SystemCallsCache = new AllMatchesCache<Function>(ainFile, this.AddSystemCalls);
            this.BuiltInMethodsCache = new BuiltInFunctionsCache(ainFile, this.AddInstructions);
            this.StructCache = new Cache2(ainFile);
        }

        public class Cache<TThing>
        {
            protected AinFile ainFile;
            public Cache(AinFile ainFile, Func<Function, HashSet<TThing>> getUsesFunction)
            {
                this.ainFile = ainFile;
                this.GetUsesFunction = getUsesFunction;
            }

            protected Dictionary<Function, HashSet<TThing>> Uses = new Dictionary<Function, HashSet<TThing>>();
            protected Dictionary<TThing, HashSet<Function>> UsedBy = new Dictionary<TThing, HashSet<Function>>();
            protected HashSet<Function> _functionsThatUseSomething = null;
            public HashSet<Function> FunctionsThatUseSomething
            {
                get
                {
                    if (_functionsThatUseSomething == null)
                    {
                        _functionsThatUseSomething = new HashSet<Function>();

                        foreach (var function in ainFile.Functions)
                        {
                            var uses = GetUses(function);
                            if (uses.Count > 0)
                            {
                                _functionsThatUseSomething.Add(function);
                            }
                        }
                    }
                    return _functionsThatUseSomething;
                }
            }

            public HashSet<TThing> GetUses(Function startFunction)
            {
                if (Uses.ContainsKey(startFunction))
                {
                    return Uses[startFunction];
                }
                else
                {
                    var set = GetUsesFunction(startFunction);
                    Uses.Add(startFunction, set);
                    return set;
                }
            }

            public HashSet<Function> GetUsedBy(TThing thing)
            {
                if (UsedBy.ContainsKey(thing))
                {
                    return UsedBy[thing];
                }
                else
                {
                    var set = GetUsedBy2(thing);
                    UsedBy.Add(thing, set);
                    return set;
                }
            }

            protected Func<Function, HashSet<TThing>> GetUsesFunction;

            protected HashSet<Function> GetUsedBy2(TThing thing)
            {
                HashSet<Function> result = new HashSet<Function>();
                foreach (var function in FunctionsThatUseSomething)
                {
                    var set = GetUses(function);
                    if (set.Contains(thing))
                    {
                        result.Add(function);
                    }
                }
                return result;
            }
        }

        public class BuiltInFunctionsCache : AllMatchesCache<Instruction>
        {
            public BuiltInFunctionsCache(AinFile ainFile, Action<Function, Action<Instruction>> codeEnumerationFunction)
                : base(ainFile, codeEnumerationFunction)
            {

            }

            public new HashSet<Function> GetUses(Function startFunction)
            {
                var uses = base.GetUses(startFunction);
                return new HashSet<Function>(uses.Select(i => CodeSearchCache.BuiltInFunctions.GetOrNull((int)i)));
            }

            public new HashSet<Function> GetUsedBy(Function func)
            {
                var list = CodeSearchCache.BuiltInFunctionsInverse.GetOrNull(func);
                if (list == null || list.Count == 0)
                {
                    return new HashSet<Function>();
                }
                if (list.Count == 1)
                {
                    return base.GetUsedBy(list[0]);
                }
                else
                {
                    return new HashSet<Function>(list.SelectMany(ins => base.GetUsedBy(ins)));
                }
            }
        }

        public class AllMatchesCache<TThing>
        {
            protected AinFile ainFile;
            public AllMatchesCache(AinFile ainFile, Action<Function, Action<TThing>> codeEnumerationFunction)
            {
                this.ainFile = ainFile;
                this.codeEnumerationFunction = codeEnumerationFunction;
            }

            protected bool created = false;

            protected Dictionary<Function, HashSet<TThing>> Uses = new Dictionary<Function, HashSet<TThing>>();
            protected Dictionary<TThing, HashSet<Function>> UsedBy = new Dictionary<TThing, HashSet<Function>>();

            protected void GetAllUses()
            {
                foreach (var function in ainFile.Functions)
                {
                    GetUsesInternal(function);
                }
                created = true;
            }

            protected void GetUsesInternal(Function startFunction)
            {
                var set = new HashSet<TThing>();
                codeEnumerationFunction(startFunction,
                    (thing) =>
                    {
                        set.Set(thing);
                        UsedBy.Set(thing, startFunction);
                    });
                Uses.Add(startFunction, set);
            }

            public HashSet<TThing> GetUses(Function startFunction)
            {
                if (!created)
                {
                    GetAllUses();
                }
                return Uses.GetOrNew(startFunction);
            }

            public HashSet<Function> GetUsedBy(TThing thing)
            {
                if (!created)
                {
                    GetAllUses();
                }
                return UsedBy.GetOrNew(thing);
            }

            protected Action<Function, Action<TThing>> codeEnumerationFunction;
        }

        public Cache<Function> FunctionsCache;
        public AllMatchesCache<Global> GlobalsCache;
        public AllMatchesCache<int> IntsCache;
        public AllMatchesCache<string> StringsCache;
        public AllMatchesCache<string> MessagesCache;
        public AllMatchesCache<HllFunction> LibraryCallsCache;
        public AllMatchesCache<Function> SystemCallsCache;
        public BuiltInFunctionsCache BuiltInMethodsCache;

        void AddInts(Function startFunction, Action<int> addSomething)
        {
            codeSearcher.EnumerateCode(startFunction, ArgumentKind.Int, addSomething);
        }

        void AddStrings(Function startFunction, Action<string> addSomething)
        {
            codeSearcher.EnumerateCode(startFunction, ArgumentKind.String, (i) => addSomething(ainFile.Strings[i]));
        }

        void AddMessages(Function startFunction, Action<string> addSomething)
        {
            codeSearcher.EnumerateCode(startFunction, ArgumentKind.Message, (i) => addSomething(ainFile.Messages[i]));
        }

        void AddGlobals(Function startFunction, Action<Global> addSomething)
        {
            codeSearcher.EnumerateCode(startFunction, ArgumentKind.Global, (i) => addSomething(ainFile.Globals[i]));
        }

        void AddLibraryCalls(Function startFunction, Action<HllFunction> addSomething)
        {
            codeSearcher.EnumerateCode(startFunction, ArgumentKind.LibraryFunction, (i) => addSomething(ainFile.Libraries[codeSearcher.libraryNumber].Functions[i]));
        }

        void AddSystemCalls(Function startFunction, Action<Function> addSomething)
        {
            codeSearcher.EnumerateCode(startFunction, ArgumentKind.SystemCall, (i) => addSomething(AinFile.SystemCalls[i]));
        }

        void AddInstructions(Function startFunction, Action<Instruction> addSomething)
        {
            codeSearcher.GetAllBuiltInFunctions(startFunction, addSomething);
        }

        HashSet<Function> GetCalls2(Function startFunction)
        {
            HashSet<Function> calls = new HashSet<Function>();
            int address = startFunction.Address;
            int lastAddress = ainFile.Code.Length;
            InstructionInfo previousInstructionInfo = default(InstructionInfo);
            if (address < lastAddress)
            {
                previousInstructionInfo = Decompiler.Peek(ainFile.Code, address);
            }
            while (address < lastAddress)
            {
                int functionIndex = -1;
                var instructionInfo = Decompiler.Peek(ainFile.Code, address);

                if (instructionInfo.instruction == Instruction.FUNC ||
                    instructionInfo.instruction == Instruction.ENDFUNC)
                {
                    break;
                }

                if (instructionInfo.instruction == Instruction.CALLFUNC ||
                    instructionInfo.instruction == Instruction.CALLMETHOD)
                {
                    functionIndex = instructionInfo.word1;
                }

                if (instructionInfo.instruction == Instruction.CALLONJUMP)
                {
                    if (previousInstructionInfo.instruction == Instruction.S_PUSH)
                    {
                        int stringIndex = previousInstructionInfo.word1;
                        if (stringIndex >= 0 && stringIndex < ainFile.Strings.Count)
                        {
                            string str = ainFile.Strings[stringIndex];
                            if (ainFile.FunctionNameToIndex.ContainsKey(str))
                            {
                                functionIndex = ainFile.FunctionNameToIndex[str];
                            }
                        }
                    }
                }

                if (instructionInfo.instruction == Instruction.FT_ASSIGNS)
                {
                    if (previousInstructionInfo.instruction == Instruction.PUSH)
                    {
                        int functionTypeNumber = previousInstructionInfo.word1;
                        if (functionTypeNumber >= 0 && functionTypeNumber < ainFile.FunctionTypes.Count)
                        {
                            var stringInstructionInfo = Decompiler.Peek(ainFile.Code, previousInstructionInfo.CurrentAddress - 6);
                            if (stringInstructionInfo.instruction == Instruction.S_PUSH)
                            {
                                int stringNumber = stringInstructionInfo.word1;
                                if (stringNumber >= 0 && stringNumber < ainFile.Strings.Count)
                                {
                                    string functionName = ainFile.Strings[stringNumber];
                                    if (ainFile.FunctionNameToIndex.ContainsKey(functionName))
                                    {
                                        var function = ainFile.Functions[ainFile.FunctionNameToIndex[functionName]];
                                        functionIndex = function.Index;
                                    }
                                }
                            }
                        }
                    }
                }

                if (functionIndex >= 0 && functionIndex < ainFile.Functions.Count)
                {
                    var function = ainFile.Functions[functionIndex];
                    if (!calls.Contains(function))
                    {
                        calls.Add(function);
                    }
                    functionIndex = -1;
                }
                address = instructionInfo.nextAddress;
                previousInstructionInfo = instructionInfo;
            }
            return calls;
        }

        public class CodeSearcher
        {
            public CodeSearcher(AinFile ainFile)
            {
                this.ainFile = ainFile;
            }

            AinFile ainFile;
            int currentStructType = -1;
            int previousStructType = -1;
            int currentFunction = -1;
            int currentClass = -1;

            IList<IVariable> locals;
            public void EnumerateCode(Function function, ArgumentKind kindToFind, Action<int> addAction)
            {
                int startAddress = function.Address; //skipping the FUNC instruction
                var decompiler = new Decompiler(ainFile);
                int endAddress = decompiler.FindEndAddress(function.Address);
                currentFunction = function.Index;
                var currentClassObject = function.GetClass();
                currentClass = currentClassObject != null ? currentClassObject.Index : -1;
                locals = ((IFunction)function).Parameters;

                int address = startAddress;

                while (address < endAddress)
                {
                    var instructionInfo = Decompiler.Peek(ainFile.Code, address);
                    int currentAddress = instructionInfo.CurrentAddress;
                    address = instructionInfo.nextAddress;
                    switch (instructionInfo.instruction)
                    {
                        case Instruction.PUSHGLOBALPAGE:
                        case Instruction.PUSHLOCALPAGE:
                        case Instruction.PUSHSTRUCTPAGE:
                        case Instruction.SR_REF:
                        case Instruction.SR_REF2:
                        case Instruction.SR_REFREF:
                        case Instruction.SH_STRUCT_SR_REF:
                            {
                                EnumerateGenericInstruction(instructionInfo, 0, kindToFind, addAction);
                                var nextInstruction = Decompiler.Peek(ainFile.Code, address);
                                if (nextInstruction.instruction == Instruction.PUSH)
                                {
                                    var overrideKind = ArgumentKind.Member;
                                    if (instructionInfo.instruction == Instruction.PUSHGLOBALPAGE) overrideKind = ArgumentKind.Global;
                                    if (instructionInfo.instruction == Instruction.PUSHLOCALPAGE) overrideKind = ArgumentKind.Local;
                                    if (instructionInfo.instruction == Instruction.PUSHSTRUCTPAGE) overrideKind = ArgumentKind.LocalMember;
                                    EnumerateGenericInstruction(nextInstruction, overrideKind, kindToFind, addAction);
                                    address = nextInstruction.nextAddress;
                                }
                            }
                            break;
                        case Instruction.STRSWITCH:
                            if (kindToFind == ArgumentKind.String)
                            {
                                var switchBlock = ainFile.Switches.GetOrNull(instructionInfo.word1);
                                foreach (var switchCase in switchBlock.SwitchCases)
                                {
                                    int value = switchCase.Value;
                                    addAction(value);
                                }
                            }
                            break;
                        case Instruction.SWITCH:
                            if (kindToFind == ArgumentKind.Int)
                            {
                                var switchBlock = ainFile.Switches.GetOrNull(instructionInfo.word1);
                                foreach (var switchCase in switchBlock.SwitchCases)
                                {
                                    int value = switchCase.Value;
                                    addAction(value);
                                }
                            }
                            break;
                        default:
                            {
                                if (instructionInfo.totalArguments > 0)
                                {
                                    EnumerateGenericInstruction(instructionInfo, 0, kindToFind, addAction);
                                }
                            }
                            break;
                    }
                }
            }

            public int libraryNumber;

            void EnumerateGenericInstruction(InstructionInfo instructionInfo, ArgumentKind overrideKind, ArgumentKind kindToFind, Action<int> addAction)
            {
                if (instructionInfo.totalArguments > 0)
                {
                    if (ArgumentKinds.InstructionArgumentKinds.ContainsKey((int)instructionInfo.instruction))
                    {
                        var argumentKinds = ArgumentKinds.InstructionArgumentKinds[(int)instructionInfo.instruction];
                        for (int i = 0; i < instructionInfo.totalArguments; i++)
                        {
                            var argumentKind = argumentKinds[i];
                            if (overrideKind != 0)
                            {
                                argumentKind = overrideKind;
                            }
                            int word = instructionInfo.words[i];
                            switch (argumentKind)
                            {
                                case ArgumentKind.AssignInt:
                                    argumentKind = ArgumentKind.Int;
                                    break;
                                case ArgumentKind.Function:
                                    if (word >= 0 && word < ainFile.Functions.Count)
                                    {
                                        var func = ainFile.Functions[word];
                                        previousStructType = currentStructType;
                                        currentStructType = func.StructType;
                                    }
                                    break;
                                case ArgumentKind.Global:
                                    if (word >= 0 && word < ainFile.Globals.Count)
                                    {
                                        var global = ainFile.Globals[word];
                                        previousStructType = currentStructType;
                                        currentStructType = global.StructType;
                                    }
                                    break;
                                case ArgumentKind.Library:
                                    libraryNumber = word;
                                    break;
                                case ArgumentKind.LibraryFunction:
                                    currentStructType = -1;
                                    break;
                                case ArgumentKind.Local:
                                    if (word >= 0 && word < locals.Count)
                                    {
                                        var local = locals[word];
                                        previousStructType = currentStructType;
                                        currentStructType = local.StructType;
                                    }
                                    break;
                                case ArgumentKind.LocalMember:
                                    argumentKind = ArgumentKind.Member;
                                    currentStructType = currentClass;
                                    if (currentStructType >= 0 && currentStructType < ainFile.Structs.Count)
                                    {
                                        var structInfo = ainFile.Structs[currentStructType];
                                        if (word >= 0 && word < structInfo.Members.Count)
                                        {
                                            var member = structInfo.Members[word];
                                            previousStructType = currentStructType;
                                            currentStructType = member.StructType;
                                        }
                                    }
                                    break;
                                case ArgumentKind.Member:
                                    if (currentStructType >= 0 && currentStructType < ainFile.Structs.Count)
                                    {
                                        var structInfo = ainFile.Structs[currentStructType];
                                        if (word >= 0 && word < structInfo.Members.Count)
                                        {
                                            var member = structInfo.Members[word];
                                            previousStructType = currentStructType;
                                            currentStructType = member.StructType;
                                        }
                                    }
                                    break;
                                case ArgumentKind.StructType:
                                    currentStructType = word;
                                    break;
                                case ArgumentKind.SystemCall:
                                    currentStructType = -1;
                                    break;
                            }
                            if (argumentKind == kindToFind)
                            {
                                addAction(word);
                            }
                        }
                    }
                }
            }


            internal void GetAllBuiltInFunctions(Function startFunction, Action<Instruction> addSomething)
            {
                int startAddress = startFunction.Address;
                int address = startAddress;
                int endAddress = startFunction.GetEndAddress();

                while (address < endAddress)
                {
                    var ins = ainFile.Peek(address);

                    if (CodeSearchCache.BuiltInFunctions.ContainsKey((int)ins.instruction))
                    {
                        addSomething(ins.instruction);
                    }
                    address = ins.nextAddress;
                }
            }
        }

        Cache2 StructCache;

        public HashSet<Function> GetFunctionsUsingStructMember(IVariable structMember)
        {
            HashSet<Function> results = new HashSet<Function>();
            if (structMember != null)
            {
                var structInfo = structMember.Parent as Struct;
                if (structInfo != null)
                {
                    var possibleFunctions = GetPossibleFunctionsUsingStructType(structInfo);
                    foreach (var function in possibleFunctions)
                    {
                        var expressionMap = ainFile.DecompiledCodeCache.GetExpressionMap(function);
                        var nodes = expressionMap.FindVariable(structMember);
                        if (nodes.FirstOrDefault() != null)
                        {
                            results.Set(function);
                        }
                    }
                }
            }
            return results;
        }

        public HashSet<Function> GetFunctionsUsingStructType(Struct structInfo)
        {
            HashSet<Function> results = new HashSet<Function>();
            var possibleFunctions = GetPossibleFunctionsUsingStructType(structInfo);
            foreach (var function in possibleFunctions)
            {
                var expressionMap = ainFile.DecompiledCodeCache.GetExpressionMap(function);
                var nodes = expressionMap.FindStructType(structInfo.Index);
                if (nodes.FirstOrDefault() != null)
                {
                    results.Set(function);
                }
            }
            return results;
        }

        HashSet<Function> GetPossibleFunctionsUsingStructType(Struct structInfo)
        {
            var structTypes = GetStructTypesUsingStructType(structInfo);
            var globals = GetGlobalsUsingStructTypes(structTypes);
            var functions = GetFunctionsUsingStructTypes(structTypes);
            foreach (var global in globals)
            {
                var funcs2 = GlobalsCache.GetUsedBy(global);
                functions.UnionWith(funcs2);
            }
            return functions;
        }

        HashSet<Struct> GetStructTypesUsingStructType(Struct structInfo)
        {
            var searcher = new StructSearcher(ainFile, StructCache);
            return searcher.GetStructTypesUsingStructType(structInfo);
        }

        HashSet<Global> GetGlobalsUsingStructTypes(HashSet<Struct> structs)
        {
            HashSet<Global> globalsUsingStructType = new HashSet<Global>();
            foreach (var global in ainFile.Globals)
            {
                if (global.StructType != -1 && structs.Contains(global.GetStructType()))
                {
                    globalsUsingStructType.Set(global);
                }
            }
            return globalsUsingStructType;
        }

        HashSet<Function> GetFunctionsUsingStructTypes(HashSet<Struct> structs)
        {
            HashSet<Function> functionsUsingStructType = new HashSet<Function>();
            //using the struct type as an argument, or local variable, or "this"
            foreach (var function in ainFile.Functions)
            {
                if (structs.Contains(function.GetClass()))
                {
                    functionsUsingStructType.Set(function);
                }
                else
                {
                    foreach (var variable in function.Parameters)
                    {
                        if (variable.StructType != -1 && structs.Contains(variable.GetStructType()))
                        {
                            functionsUsingStructType.Set(function);
                            break;
                        }
                    }
                }
            }
            return functionsUsingStructType;
        }

        class StructSearcher
        {
            Cache2 structCache;
            AinFile ainFile;

            HashSet<Struct> result = new HashSet<Struct>();

            public StructSearcher(AinFile ainFile, Cache2 structCache)
            {
                this.ainFile = ainFile;
                this.structCache = structCache;
            }

            public HashSet<Struct> GetStructTypesUsingStructType(Struct structInfo)
            {
                result.Set(structInfo);
                foreach (var otherStruct in structCache.GetUsedBy(structInfo))
                {
                    if (!result.Contains(otherStruct))
                    {
                        GetStructTypesUsingStructType(otherStruct);
                    }
                }
                return result;
            }
        }

        public class Cache2
        {
            protected AinFile ainFile;
            public Cache2(AinFile ainFile)
            {
                this.ainFile = ainFile;
            }

            public Dictionary<Struct, HashSet<Struct>> Uses = new Dictionary<Struct, HashSet<Struct>>();
            public Dictionary<Struct, HashSet<Struct>> UsedBy = new Dictionary<Struct, HashSet<Struct>>();
            protected HashSet<Struct> _structsThatUseSomething = null;
            public HashSet<Struct> StructsThatUseSomething
            {
                get
                {
                    if (_structsThatUseSomething == null)
                    {
                        _structsThatUseSomething = new HashSet<Struct>();

                        foreach (var structInfo in ainFile.Structs)
                        {
                            var uses = GetUses(structInfo);
                            if (uses.Count > 0)
                            {
                                _structsThatUseSomething.Add(structInfo);
                            }
                        }
                    }
                    return _structsThatUseSomething;
                }
            }

            public HashSet<Struct> GetUses(Struct startStruct)
            {
                if (Uses.ContainsKey(startStruct))
                {
                    return Uses[startStruct];
                }
                else
                {
                    var set = GetUses2(startStruct);
                    Uses.Add(startStruct, set);
                    return set;
                }
            }

            public HashSet<Struct> GetUsedBy(Struct thing)
            {
                if (UsedBy.ContainsKey(thing))
                {
                    return UsedBy[thing];
                }
                else
                {
                    var set = GetUsedBy2(thing);
                    UsedBy.Add(thing, set);
                    return set;
                }
            }

            protected HashSet<Struct> GetUses2(Struct startStruct)
            {
                HashSet<Struct> result = new HashSet<Struct>();
                foreach (var member in startStruct.Members)
                {
                    if (member.StructType != -1)
                    {
                        result.Add(ainFile.Structs[member.StructType]);
                    }
                }
                return result;
            }

            protected HashSet<Struct> GetUsedBy2(Struct thing)
            {
                HashSet<Struct> result = new HashSet<Struct>();
                foreach (var structInfo in StructsThatUseSomething)
                {
                    var set = GetUses(structInfo);
                    if (set.Contains(thing))
                    {
                        result.Add(structInfo);
                    }
                }
                return result;
            }
        }

        private static Dictionary<int, Function> _builtInFunctions;
        internal static Dictionary<int, Function> BuiltInFunctions
        {
            get
            {
                if (_builtInFunctions == null)
                {
                    _builtInFunctions = BuildDesiredInstructions();
                }
                return _builtInFunctions;
            }
        }

        private static Dictionary<Function, List<Instruction>> _builtInFunctionsInverse;
        internal static Dictionary<Function, List<Instruction>> BuiltInFunctionsInverse
        {
            get
            {
                if (_builtInFunctionsInverse == null)
                {
                    _builtInFunctionsInverse = new Dictionary<Function, List<Instruction>>();
                    var builtInFunctions = BuiltInFunctions;
                    foreach (var pair in builtInFunctions)
                    {
                        _builtInFunctionsInverse.GetOrAddNew(pair.Value).Add((Instruction)pair.Key);
                    }
                }
                return _builtInFunctionsInverse;
            }
        }

        private static Dictionary<int, Function> BuildDesiredInstructions()
        {
            Dictionary<int, Function> desiredInstructions = new Dictionary<int, Function>();
            foreach (var pair in Compiler.Compiler.InstructionToBuiltInFunction)
            {
                desiredInstructions.Set((int)pair.Key, pair.Value);
            }
            foreach (var pair in CompositeInstructions.ExpandedInstructions)
            {
                Instruction ins = (Instruction)pair.Key;
                foreach (var ins2 in pair.Value)
                {
                    if (desiredInstructions.ContainsKey((int)ins2))
                    {
                        desiredInstructions.Set((int)ins, desiredInstructions[(int)ins2]);
                    }
                }
            }
            desiredInstructions.Set((int)Instruction.A_SORT_MEM, desiredInstructions[(int)Instruction.A_SORT]);
            return desiredInstructions;
        }
    }
}
