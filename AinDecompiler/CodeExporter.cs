using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace AinDecompiler
{
    public class BackgroundCodeExporter : BackgroundTask
    {
        protected override string TitleBarText { get { return "Decompiling Code..."; } }

        protected override bool AbortOnCancel { get { return false; } }

        public bool Run(string outputPath, CodeExporter codeExporter)
        {
            return base.Run(codeExporter.ainFile.OriginalFilename, outputPath, codeExporter);
        }

        protected override object DoWork(string inputFileName, string outputFileName, BackgroundTask.WorkerData workerData)
        {
            var codeExporter = workerData.ExtraData as CodeExporter;
            codeExporter.backgroundWorker = this.backgroundWorker;
            codeExporter.ExportFiles(outputFileName);

            return true;
        }
    }

    public class CodeExporter
    {
        TinyStopwatch stopwatch = new TinyStopwatch();
        internal AinFile ainFile;
        internal MyBackgroundWorker backgroundWorker;
        Decompiler decompiler;
        CodeDisplayOptions codeDisplayOptions;
        public CodeExporter(AinFile ainFile, CodeDisplayOptions codeDisplayOptions)
        {
            this.ainFile = ainFile;
            this.codeDisplayOptions = codeDisplayOptions;
            this.decompiler = new Decompiler(ainFile);
        }

        public void ExportFiles()
        {
            if (!PromptForUnknownFunctionTypes()) return;

            var sfd = new SaveFileDialog();
            sfd.DefaultExt = "pje";
            sfd.FileName = "EXPORT FILES HERE";
            if (sfd.ShowDialogWithTopic(DialogTopic.DecompileCode) == DialogResult.OK)
            {
                string destinationPath = Path.GetDirectoryName(sfd.FileName);
                ExportFilesInBackground(destinationPath);
                //ExportFiles(destinationPath);
            }

        }

        private void ExportFilesInBackground(string destinationPath)
        {
            var backgroundCodeExporter = new BackgroundCodeExporter();
            backgroundCodeExporter.Run(destinationPath, this);
        }

        bool PromptForUnknownFunctionTypes()
        {
            ainFile.FindFunctionTypes();
            if (ainFile.GetVariablesWithUnknownFunctypes().FirstOrDefault() != null)
            {
                if (MessageBox.Show("Some function pointers (FuncType variables) have an unknown type," + Environment.NewLine +
                "the decompiler will assign them a void function type with no parameters." + Environment.NewLine +
                "This may lead to incorrect behavior when the code is compiled or run." + Environment.NewLine +
                "You should fix the unknown function types first by browsing the code, and editing their metadata." + Environment.NewLine +
                "Decompile the code anyway?", "There are variables with unknown function types.", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
                {
                    return false;
                }
            }
            return true;
        }

        HashSet<string> SeenFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private FileStream CreateFileUnique(ref string newFileName, string path)
        {
            if (SeenFilenames.Contains(newFileName.ToLowerInvariant()))
            {
                //generate a unique filename
                string dir = Path.GetDirectoryName(newFileName);
                string baseName = Path.GetFileNameWithoutExtension(newFileName);
                string ext = Path.GetExtension(newFileName);
                int n = 0;

                do
                {
                    newFileName = Path.Combine(dir, baseName + "_" + n.ToString("00"));
                    n++;
                } while (SeenFilenames.Contains(newFileName));
            }
            SeenFilenames.Add(newFileName.ToLowerInvariant());

            return CreateFile(newFileName, path);
        }

        private FileStream CreateFile(string newFileName, string path)
        {
            FileStream file2 = null;
            string newDirectory = Path.Combine(path, Path.GetDirectoryName(newFileName));

            if (!Directory.Exists(newDirectory))
            {
                Directory.CreateDirectory(newDirectory);
            }


            string fileName = Path.Combine(path, newFileName);
            file2 = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            return file2;
        }

        internal void ExportFiles(string destinationPath)
        {
            ainFile.FindFunctionTypes();
            stopwatch.Start();

            var encoding = Extensions.BinaryEncoding;
            var enumerator = new FunctionEnumerator(this.ainFile);
            var results = enumerator.GetFilesAndFunctions();
            HashSet<Struct> UnvisitedStructs = new HashSet<Struct>(ainFile.Structs);
            HashSet<int> VisitedFunctions = new HashSet<int>();
            int functionsVisited = 0;
            int totalFunctions = ainFile.Functions.Count;

            codeDisplayOptions.DisplayDefaultValuesForMethods = false;
            var displayer = new ExpressionDisplayer(ainFile, codeDisplayOptions);

            StringBuilder mainIncludeFile = new StringBuilder();
            mainIncludeFile.AppendLine("Source = {");
            mainIncludeFile.AppendLine("\t\"constants.jaf\",");
            mainIncludeFile.AppendLine("\t\"classes.jaf\",");
            mainIncludeFile.AppendLine("\t\"globals.jaf\",");

            this.SeenFilenames.Add("classes.jaf");
            this.SeenFilenames.Add("globals.jaf");
            this.SeenFilenames.Add("constants.jaf");
            this.SeenFilenames.Add("HLL\\hll.inc");

            if (ainFile.Libraries.Count > 0)
            {
                mainIncludeFile.AppendLine("\t\"HLL\\hll.inc\",");
            }

            foreach (var fileNode in results)
            {
                string fileName = fileNode.name.Replace("\r", "\\r").Replace("\n", "\\n");  //fix filenames that went through bad tools
                if (fileNode.children.Count > 0)
                {
                    using (FileStream fs = CreateFileUnique(ref fileName, destinationPath))
                    {
                        mainIncludeFile.AppendLine("\t\"" + fileName + "\",");

                        using (var streamWriter = new StreamWriter(fs, encoding))
                        {
                            foreach (var functionNode in fileNode.children)
                            {
                                if (backgroundWorker != null)
                                {
                                    if (backgroundWorker.CancellationPending == true)
                                    {
                                        //abort
                                        return;
                                    }
                                }

                                int functionNumber = functionNode.id;
                                var function = ainFile.GetFunction(functionNumber);

                                if(function.ToString().Contains("SP_SET_CG_REAL")) {
                                    Console.WriteLine("SP_SET_CG_REAL");
                                }

                                if (!VisitedFunctions.Contains(functionNumber))
                                {
                                    VisitedFunctions.Add(functionNumber);
                                    if (this.backgroundWorker != null && stopwatch.ElapsedTime >= 250)
                                    {
                                        stopwatch.Start();
                                        backgroundWorker.ReportProgress(100 * functionsVisited / totalFunctions,
                                            "Function " + functionNumber.ToString() + " of " + totalFunctions.ToString() +
                                            ", currently decompiling" + Environment.NewLine + function.Name);
                                    }

                                    if (function.Name == "0" || function.Name.EndsWith("@2"))
                                    {
                                        //exclude global array initializer and struct array initializer functions
                                    }
                                    else
                                    {
                                        //var structInfo = function.GetClass();
                                        //if (structInfo != null)
                                        //{
                                        //    if (UnvisitedStructs.Contains(structInfo))
                                        //    {
                                        //        UnvisitedStructs.Remove(structInfo);
                                        //        string classDeclaration = displayer.GetClassDeclaration(structInfo);
                                        //        streamWriter.Write(classDeclaration);
                                        //    }
                                        //}
                                        if (Debugger.IsAttached)
                                        {
                                            //no exception handling when debugging - we want to see the exceptions
                                            try
                                            {
                                                var expression = decompiler.DecompileFunction(function);
                                                string text = displayer.PrintExpression2(expression, true);
                                                streamWriter.WriteLine(text);
                                            }
                                            finally
                                            {

                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                var expression = decompiler.DecompileFunction(function);
                                                string text = displayer.PrintExpression2(expression, true);
                                                streamWriter.WriteLine(text);
                                            }
                                            catch (Exception ex)
                                            {
                                                string errorMessage = "Function " + functionNode.name + " failed to decompile.";
                                                RaiseError(errorMessage, ex);
                                            }
                                            finally
                                            {

                                            }
                                        }
                                    }
                                    functionsVisited++;
                                }
                            }
                            streamWriter.Flush();
                            streamWriter.Close();
                        }
                    }
                }
            }
            mainIncludeFile.AppendLine("}");

            if (UnvisitedStructs.Count > 0)
            {
                var remainingStructs = UnvisitedStructs.OrderByIndex().ToArray();

                if (this.backgroundWorker != null)
                {
                    backgroundWorker.ReportProgress(100, "Generating class declarations...");
                }
                using (var fs = CreateFile("classes.jaf", destinationPath))
                {
                    using (var sw = new StreamWriter(fs, encoding))
                    {
                        foreach (var structInfo in remainingStructs)
                        {
                            string classDeclaration = displayer.GetClassDeclaration(structInfo);
                            sw.Write(classDeclaration);
                            sw.WriteLine();
                        }

                        foreach (var funcType in ainFile.FunctionTypes)
                        {
                            string funcTypeDeclaration = displayer.GetFunctypeDeclaration(funcType);
                            sw.WriteLine(funcTypeDeclaration);
                        }

                        foreach (var delg in ainFile.Delegates)
                        {
                            string delegateDeclaration = displayer.GetDelegateDeclaration(delg);
                            sw.WriteLine(delegateDeclaration);
                        }
                        sw.Flush();
                        sw.Close();
                    }
                }
            }

            Dictionary<Global, Expression> globalInitializers = GetGlobalInitializers();

            if (this.backgroundWorker != null)
            {
                backgroundWorker.ReportProgress(100, "Listing global variables...");
            }

            using (var fs = CreateFile("globals.jaf", destinationPath))
            {
                string lastGlobalGroupName = null;
                using (var sw = new MyIndentedTextWriter(new StreamWriter(fs, encoding)))
                {
                    Dictionary<int, GlobalInitialValue> initialValues = new Dictionary<int, GlobalInitialValue>();
                    foreach (var globalInitialValue in ainFile.GlobalInitialValues)
                    {
                        initialValues[globalInitialValue.GlobalIndex] = globalInitialValue;
                    }


                    foreach (var global in ainFile.Globals)
                    {
                        if (global.DataType != DataType.Void)
                        {
                            string globalGroupName = global.GroupName;
                            if (globalGroupName != lastGlobalGroupName)
                            {
                                if (lastGlobalGroupName != null)
                                {
                                    sw.Indent--;
                                    sw.WriteLine("}");
                                }
                                if (globalGroupName != null)
                                {
                                    sw.WriteLine("globalgroup " + globalGroupName);
                                    sw.WriteLine("{");
                                    sw.Indent++;
                                }
                            }
                            lastGlobalGroupName = globalGroupName;

                            sw.Write(global.GetDataTypeName());
                            sw.Write(" ");
                            sw.Write(global.Name);

                            if (global.DataType.IsArray())
                            {
                                if (globalInitializers.ContainsKey(global))
                                {
                                    var expr = globalInitializers[global];

                                    foreach (var e in expr.Args)
                                    {
                                        if (e.ExpressionType == Instruction.PUSH)
                                        {
                                            sw.Write("[" + e.Value.ToString() + "]");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (initialValues.ContainsKey(global.Index))
                                {
                                    sw.Write(" = ");
                                    var initialValue = initialValues[global.Index];
                                    sw.Write(initialValue.GetValueQuoted());
                                }
                            }
                            sw.WriteLine(";");
                        }
                    }
                    if (lastGlobalGroupName != null)
                    {
                        sw.Indent--;
                        sw.WriteLine("}");
                    }
                    sw.Flush();
                    sw.Close();
                }
            }

            using (var fs = CreateFile("constants.jaf", destinationPath))
            {
                using (StreamWriter sw = new StreamWriter(fs, encoding))
                {
                    sw.WriteLine("const int true = 1;");
                    sw.WriteLine("const int false = 0;");
                    sw.WriteLine();
                    if (ainFile.MetadataFile != null)
                    {
                        foreach (var pair in ainFile.MetadataFile.EnumerationTypes)
                        {
                            var enumerationType = pair.Value;
                            sw.WriteLine("//" + enumerationType.Name);
                            foreach (var pair2 in enumerationType)
                            {
                                sw.WriteLine("const int " + pair2.Value + " = " + pair2.Key.ToString() + ";");
                            }
                            sw.WriteLine();
                        }
                    }

                    sw.Flush();
                    fs.Flush();
                    sw.Close();
                    fs.Close();
                }


            }

            if (this.backgroundWorker != null)
            {
                backgroundWorker.ReportProgress(100, "Creating project file...");
            }

            if (ainFile.Libraries.Count > 0)
            {
                StringBuilder libraryIncludeFile = new StringBuilder();
                libraryIncludeFile.AppendLine("SystemSource = {");

                string hllDirectory = Path.Combine(destinationPath, "HLL");
                foreach (var library in ainFile.Libraries)
                {
                    string hllFileName = library.LibraryName + ".hll";

                    using (var fs = CreateFileUnique(ref hllFileName, hllDirectory))
                    {
                        libraryIncludeFile.AppendLine("\t\"" + hllFileName + "\",\t\"" + library.LibraryName + "\",");
                        using (var sw = new StreamWriter(fs, encoding))
                        {
                            foreach (var func in library.Functions)
                            {
                                string declaration = func.GetDeclaration() + ";";
                                sw.WriteLine(declaration);
                            }
                            sw.Flush();
                            sw.Close();
                        }
                    }
                }
                libraryIncludeFile.AppendLine("}");
                string includeFileContents = libraryIncludeFile.ToString();

                using (var fs = CreateFile("hll.inc", hllDirectory))
                {
                    using (var sw = new StreamWriter(fs, encoding))
                    {
                        sw.Write(includeFileContents);
                    }
                }
            }

            File.WriteAllText(Path.Combine(destinationPath, "main.inc"), mainIncludeFile.ToString(), encoding);

            //build a PJE file
            {
                StringBuilder pje = new StringBuilder();
                pje.AppendLine("// Project Environment File");
                pje.AppendLine("ProjectName = \"" + Path.GetFileNameWithoutExtension(this.ainFile.OriginalFilename) + "\"");
                pje.AppendLine();
                pje.AppendLine("CodeName = \"" + Path.GetFileNameWithoutExtension(this.ainFile.OriginalFilename) + ".ain\"");
                pje.AppendLine();
                pje.AppendLine("#define _AINVERSION " + ainFile.Version.ToString());
                pje.AppendLine("#define _KEYCODE 0x" + ainFile.KeyCode.ToString("X8"));
                pje.AppendLine("#define _ISAI2FILE " + (ainFile.IsAi2File ? "true" : "false"));
                if (ainFile.Version >= 6)
                {
                    pje.AppendLine("#define _USESMSG1 " + (ainFile.UsesMsg1 ? "true" : "false"));
                }
                pje.AppendLine("#define _TARGETVM " + ainFile.TargetVMVersion.ToString());
                pje.AppendLine();
                pje.AppendLine("GameVersion = " + ainFile.GameVersion.ToString());
                pje.AppendLine();
                pje.AppendLine("// Settings for each directory");
                pje.AppendLine("SourceDir = \".\"");
                pje.AppendLine("HLLDir = \"HLL\"");
                pje.AppendLine("ObjDir = \"OBJ\"");
                pje.AppendLine("OutputDir = \"Run\"");
                pje.AppendLine();
                pje.AppendLine("Source = {");
                pje.AppendLine("    \"main.inc\",");
                pje.AppendLine("}");

                string pjeFileName = Path.Combine(destinationPath, Path.GetFileNameWithoutExtension(this.ainFile.OriginalFilename) + ".pje");

                File.WriteAllText(pjeFileName, pje.ToString(), encoding);
            }
        }

        public event EventHandler<ErrorEventArgs> Error;

        private void RaiseError(string errorMessage)
        {
            RaiseError(errorMessage, null);
        }

        private void RaiseError(string errorMessage, Exception ex)
        {
            if (backgroundWorker != null)
            {
                backgroundWorker.ReportProgress(-1, new Exception(errorMessage, ex));
            }
            if (this.Error != null)
            {
                this.Error(this, new ErrorEventArgs(new Exception(errorMessage, ex)));
            }
        }

        private Dictionary<Global, Expression> GetGlobalInitializers()
        {
            Dictionary<Global, Expression> globalInitializers = new Dictionary<Global, Expression>();
            var globalArrayInitializerFunction = ainFile.GetFunction("0");
            if (globalArrayInitializerFunction != null)
            {
                var code = ainFile.DecompiledCodeCache.GetDecompiledCode(globalArrayInitializerFunction);
                if (code.Arg1.ExpressionType != Instruction.RETURN)
                {
                    foreach (var expression in code.GetChildExpressions())
                    {
                        if (expression.ExpressionType == Instruction.A_ALLOC)
                        {
                            var global = expression.Arg1.Variable.Canonicalize() as Global;
                            globalInitializers[global] = expression;
                        }
                    }
                }
            }

            return globalInitializers;
        }

    }
}
