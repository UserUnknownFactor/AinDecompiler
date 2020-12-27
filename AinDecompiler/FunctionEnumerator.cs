using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    public class FunctionEnumerationResult
    {
        /// <summary>
        /// The name of the function or file
        /// </summary>
        public string name;
        /// <summary>
        /// For a file node, the list of function nodes contained in that file node
        /// </summary>
        public List<FunctionEnumerationResult> children;
        /// <summary>
        /// The address of the FUNC instruction, add 6 to get the first instruction of the function.
        /// </summary>
        public int address;
        /// <summary>
        /// The last address of the function, this address contains the ENDFUNC, EOF, or next FUNC instruction
        /// </summary>
        public int lastAddress;
        /// <summary>
        /// The number of the function
        /// </summary>
        public int id;
        /// <summary>
        /// Returns the function name
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return name;
        }
        /// <summary>
        /// Returns the function associated with this function node, or null if the function does not exist.
        /// </summary>
        public Function Function
        {
            get
            {
                InstructionInfo instructionInfo;
                try
                {
                    instructionInfo = Expression.defaultAinFile.Peek(address);
                }
                catch
                {
                    return null;
                }
                if (instructionInfo.instruction == Instruction.FUNC)
                {
                    return Expression.defaultAinFile.GetFunction(instructionInfo.word1);
                }
                return null;
            }
        }
    }

    class FunctionEnumerator
    {
        public FunctionEnumerator(AinFile ainFile)
        {
            this.ainFile = ainFile;
        }
        AinFile ainFile;

        public FunctionEnumerationResult[] GetFilesAndFunctions_old()
        {
            int address = 0;
            int codeLength = ainFile.Code.Length;

            List<FunctionEnumerationResult> files = new List<FunctionEnumerationResult>();
            FunctionEnumerationResult currentFunction = null;
            FunctionEnumerationResult currentFile = new FunctionEnumerationResult();
            currentFile.children = new List<FunctionEnumerationResult>();

            currentFile.address = 0;

            while (address < codeLength)
            {
                var info = Decompiler.Peek(ainFile.Code, address);
                if (info.instruction == Instruction.EOF)
                {
                    if (info.word1 >= 0 && info.word1 < ainFile.Filenames.Count)
                    {
                        currentFile.name = ainFile.Filenames[info.word1];
                        currentFile.id = info.word1;
                        if (currentFunction != null)
                        {
                            currentFunction.lastAddress = address;
                            currentFile.children.Add(currentFunction);
                            currentFunction = null;
                        }
                        currentFile.lastAddress = info.nextAddress;
                        files.Add(currentFile);
                        currentFile = new FunctionEnumerationResult();
                        currentFile.children = new List<FunctionEnumerationResult>();
                        currentFile.address = info.nextAddress;
                    }
                }
                if (info.instruction == Instruction.FUNC)
                {
                    if (currentFunction != null)
                    {
                        currentFunction.lastAddress = address;
                        currentFile.children.Add(currentFunction);
                    }
                    if (info.word1 >= 0 && info.word1 < ainFile.Functions.Count)
                    {
                        currentFunction = new FunctionEnumerationResult();
                        currentFunction.id = info.word1;
                        currentFunction.name = ainFile.Functions[info.word1].Name;
                        currentFunction.address = address;
                    }
                }
                if (info.instruction == Instruction.ENDFUNC)
                {
                    if (currentFunction != null)
                    {
                        currentFunction.lastAddress = info.nextAddress;
                        currentFile.children.Add(currentFunction);
                        currentFunction = null;
                    }
                }
                address = info.nextAddress;
            }
            if (currentFile.children.Count > 0)
            {
                if (String.IsNullOrEmpty(currentFile.name))
                {
                    currentFile.name = "remaining.jaf";
                }
                files.Add(currentFile);
            }
            return files.ToArray();
        }

        public FunctionEnumerationResult[] GetFilesAndFunctions()
        {
            if (ainFile.Version == 0)
            {
                return GetFilesAndFunctionsVersion0();
            }

            int address = 0;
            int codeLength = ainFile.Code.Length;

            HashSet<int> fileNumberSeen = new HashSet<int>();

            List<FunctionEnumerationResult> files = new List<FunctionEnumerationResult>();
            FunctionEnumerationResult currentFunction = null;
            FunctionEnumerationResult currentFile = new FunctionEnumerationResult();
            currentFile.children = new List<FunctionEnumerationResult>();

            currentFile.address = 0;

            bool wantNewFile = false;
            InstructionInfo newFileInstruction = null;

            bool haveFunction = false;

            while (address < codeLength)
            {
                var info = Decompiler.Peek(ainFile.Code, address);
                if (info.instruction == Instruction.EOF)
                {
                    if (!fileNumberSeen.Contains(info.word1))
                    {
                        fileNumberSeen.Add(info.word1);
                        if (wantNewFile)
                        {
                            FinishFile(address, files, ref currentFunction, ref currentFile, newFileInstruction);
                            wantNewFile = false;
                            newFileInstruction = null;
                        }
                        wantNewFile = true;
                        newFileInstruction = info;
                    }
                }
                if (info.instruction == Instruction.FUNC)
                {
                    if (wantNewFile)
                    {
                        FinishFile(address, files, ref currentFunction, ref currentFile, newFileInstruction);
                        wantNewFile = false;
                        newFileInstruction = null;
                    }
                    FinishFunction(address, ref currentFunction, currentFile);

                    if (info.word1 >= 0 && info.word1 < ainFile.Functions.Count)
                    {
                        currentFunction = new FunctionEnumerationResult();
                        currentFunction.id = info.word1;
                        currentFunction.name = ainFile.Functions[info.word1].Name;
                        currentFunction.address = address;
                    }
                }
                //if (info.instruction == Instruction.ENDFUNC)
                //{
                //    FinishFunction(address, ref currentFunction, currentFile);
                //}
                address = info.nextAddress;
            }
            if (currentFile.children.Count > 0)
            {
                if (String.IsNullOrEmpty(currentFile.name))
                {
                    currentFile.name = "remaining.jaf";
                }
                FinishFile(address, files, ref currentFunction, ref currentFile, newFileInstruction);
                //files.Add(currentFile);
            }

            return files.ToArray();
        }

        private FunctionEnumerationResult[] GetFilesAndFunctionsVersion0()
        {
            var fileNode = new FunctionEnumerationResult();
            fileNode.name = "code.jaf";
            fileNode.children = new List<FunctionEnumerationResult>();
            fileNode.address = -1;

            for (int i = 0; i < ainFile.Functions.Count; i++)
            {
                var function = ainFile.Functions[i];
                FunctionEnumerationResult functionNode = new FunctionEnumerationResult();
                functionNode.address = function.Address + 6;
                functionNode.name = function.Name;
                functionNode.id = function.Index;

                if (i + 1 < ainFile.Functions.Count)
                {
                    functionNode.lastAddress = ainFile.Functions[i + 1].Address;
                }
                else
                {
                    functionNode.lastAddress = ainFile.Code.Length;
                }
                fileNode.children.Add(functionNode);
            }
            return new FunctionEnumerationResult[] { fileNode };
        }

        private void FinishFile(int address, List<FunctionEnumerationResult> files, ref FunctionEnumerationResult currentFunction, ref FunctionEnumerationResult currentFile, InstructionInfo newFileInstruction)
        {
            if (newFileInstruction == null || (newFileInstruction.word1 >= 0 && newFileInstruction.word1 < ainFile.Filenames.Count))
            {
                if (newFileInstruction != null)
                {
                    currentFile.name = ainFile.Filenames[newFileInstruction.word1];
                    currentFile.id = newFileInstruction.word1;
                }
                FinishFunction(address, ref currentFunction, currentFile);
                currentFile.lastAddress = address;
                files.Add(currentFile);
                currentFile = new FunctionEnumerationResult();
                currentFile.children = new List<FunctionEnumerationResult>();
                currentFile.address = address;
            }
        }

        private static void FinishFunction(int address, ref FunctionEnumerationResult currentFunction, FunctionEnumerationResult currentFile)
        {
            if (currentFunction != null)
            {
                currentFunction.lastAddress = address;
                currentFile.children.Add(currentFunction);
                currentFunction = null;
            }
        }

        public FunctionEnumerationResult[] GetClasses()
        {
            var classesObject = new FunctionEnumerationResult() { address = -1, name = "classes", id = -1, lastAddress = -1 };
            classesObject.children = new List<FunctionEnumerationResult>(ainFile.Structs.Count);
            foreach (var structInfo in ainFile.Structs)
            {
                var newObject = new FunctionEnumerationResult() { address = -1, name = structInfo.Name, id = structInfo.Index, lastAddress = -1 };
                classesObject.children.Add(newObject);
            }
            return new[] { classesObject };
        }

        public Dictionary<string, string> GetFunctionNameToFileName()
        {
            Dictionary<string,string> dic = new Dictionary<string,string>();
            var results = this.GetFilesAndFunctions();
            foreach (var result1 in results)
            {
                if (result1.children != null && result1.children.Count > 0)
                {
                    string fileName = result1.name;
                    foreach (var result2 in result1.children)
                    {
                        dic[result2.name] = fileName;
                    }
                }
            }
            return dic;

        }
    }
}
