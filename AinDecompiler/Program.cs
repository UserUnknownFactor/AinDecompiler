using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Globalization;
using System.Text;

namespace AinDecompiler
{
    static class Program
    {
        public static bool ConsoleMode = false;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            //initialization of static classes that needs to be done after all other static initialization - doing it here
            ArgumentKinds.InitArgumentKinds();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ExplorerForm explorerForm = null;
            var args = Environment.GetCommandLineArgs();

            string fileName1 = FixMojibake(args.GetOrDefault(1, ""));
            string fileName2 = FixMojibake(args.GetOrDefault(2, ""));
            string fileName3 = FixMojibake(args.GetOrDefault(3, ""));
            string ext1 = Path.GetExtension(fileName1).ToLowerInvariant();
            string ext2 = Path.GetExtension(fileName2).ToLowerInvariant();
            string ext3 = Path.GetExtension(fileName3).ToLowerInvariant();
            bool file1Exists = File.Exists(fileName1);
            bool file2Exists = File.Exists(fileName2);
            bool file3Exists = File.Exists(fileName3);

            if (fileName1 == "")
            {
                //no arguments - run the program
                explorerForm = new ExplorerForm();
                Application.Run(explorerForm);
                return;
            }

            if ((ext1 == ".ain" || ext1 == ".ain_") && file1Exists && fileName2 == "")
            {
                //one argument, an AIN file, run the program opening that file.
                explorerForm = new ExplorerForm(fileName1);
                Application.Run(explorerForm);
                return;
            }

            ConsoleTools.CreateOrAttachConsole();
            Program.ConsoleMode = true;
            //use modified Shift Jis 2.0 by default when running in console mode
            Extensions.BinaryEncoding = ModifiedShiftJis2.GetEncoding();
            Extensions.TextEncoding = new UTF8Encoding(true);

            if ((ext1 == ".ain" || ext1 == ".ain_") && file1Exists && fileName2 != "")
            {
                switch (ext2)
                {
                    case ".txt":
                        if (file2Exists && fileName3 != "" && ext3 == ".ain")
                        {
                            if (!ImportText(fileName1, fileName2, fileName3))
                            {
                                Environment.ExitCode = 1;
                            }
                            return;
                        }
                        break;
                    case ".jaf":
                        if (file2Exists && fileName3 != "" && ext3 == ".ain")
                        {
                            QuickCompileCodePatch(fileName1, fileName2, fileName3);
                            return;
                        }
                        break;
                    case ".jam":
                        CreateProject(fileName1, fileName2);
                        return;
                        break;
                    case ".pje":
                        DecompileCode(fileName1, fileName2);
                        return;
                        break;
                }
            }
            if (ext1 == ".jam" && file1Exists && fileName2 != "" && (ext2 == ".ain" || ext2 == ".ain_"))
            {
                BuildProject(fileName1, fileName2);
                return;
            }
            if (ext1 == ".pje" && file1Exists)
            {
                CompileCode(fileName1);
                return;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Command line syntax:" + Environment.NewLine +
                "No arguments: Start the program" + Environment.NewLine +
                "<file.ain>: Load the AIN file" + Environment.NewLine +
                "<file.ain> <file.jam>: Decompile ASM to directory containing file.jam (multiple files)" + Environment.NewLine +
                "<file.ain> <file.pje>: Decompile Code to directory contianing file.pje" + Environment.NewLine +
                "<file.jam> <file.ain>: Compile ASM code" + Environment.NewLine +
                "<file.pje>: Compile Code" + Environment.NewLine +
                "<file.ain> <file.txt> <file.ain>: Import Text (New)" + Environment.NewLine +
                "<file.ain> <file.jaf> <file.ain>: Quick Compile Code Patch");
            Console.Error.WriteLine();
        }

        private static bool FileOrDirectoryExists(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return false;
            }
            try
            {
                if (File.Exists(path))
                {
                    return true;
                }
                if (Directory.Exists(path))
                {
                    return true;
                }
                string pathDirectory = "";
                try
                {
                    pathDirectory = Path.GetDirectoryName(path);
                }
                catch
                {

                }
                if (!String.IsNullOrEmpty(pathDirectory))
                {
                    if (Directory.Exists(pathDirectory))
                    {
                        //directory name must contain non-ascii characters
                        bool containsNonAscii = false;
                        foreach (char c in pathDirectory)
                        {
                            if (c >= 128)
                            {
                                containsNonAscii = true;
                                break;
                            }
                        }

                        if (containsNonAscii)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private static string FixMojibake(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return path;
            }
            bool pathIsAscii = true;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] >= 128)
                {
                    pathIsAscii = false;
                    break;
                }
            }
            if (pathIsAscii)
            {
                return path;
            }

            //if path is correct, accept it
            if (FileOrDirectoryExists(path))
            {
                return path;
            }

            var defaultEncodingWithThrow = Encoding.GetEncoding(0, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            var shiftJisWithThrow = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            if (defaultEncodingWithThrow.CodePage != 932)
            {
                bool PathIsValidJapanese = false;
                try
                {
                    byte[] shiftJisBytes = shiftJisWithThrow.GetBytes(path);
                    string shiftJisPath = shiftJisWithThrow.GetString(shiftJisBytes);
                    if (path == shiftJisPath)
                    {
                        PathIsValidJapanese = true;
                    }
                }
                catch
                {

                }

                byte[] bytes = null;
                string path2;
                try
                {
                    bytes = defaultEncodingWithThrow.GetBytes(path);
                }
                catch
                {
                    return path;
                }

                try
                {
                    path2 = shiftJisWithThrow.GetString(bytes);
                }
                catch
                {
                    return path;
                }

                if (FileOrDirectoryExists(path2))
                {
                    return path2;
                }

                if (PathIsValidJapanese)
                {
                    return path;
                }
                return path2;
            }
            return path;
        }

        private static void CompileCode(string projectFileName)
        {
            var compiler = new Compiler.Compiler();
            compiler.Error += new EventHandler<ErrorEventArgs>(compiler_Error);
            compiler.Compile(projectFileName);
        }

        static void compiler_Error(object sender, ErrorEventArgs e)
        {
            var exception = e.GetException();
            string errorMessage = exception.Message;
            Console.WriteLine(errorMessage);
        }

        private static void DecompileCode(string ainFileName, string pjeFileName)
        {
            var ainFile = new AinFile(ainFileName);
            var codeDisplayOptions = new CodeDisplayOptions();
            var codeExporter = new CodeExporter(ainFile, codeDisplayOptions);
            codeExporter.Error += new EventHandler<ErrorEventArgs>(codeExporter_Error);

            string directory2 = Path.GetDirectoryName(pjeFileName);
            if (!Directory.Exists(directory2))
            {
                Directory.CreateDirectory(directory2);
            }
            codeExporter.ExportFiles(directory2);
        }

        static void codeExporter_Error(object sender, ErrorEventArgs e)
        {
            var exception = e.GetException();
            string errorMessage = exception.Message;
            Console.WriteLine(errorMessage);
        }

        private static bool ImportText(string inputAinFileName, string textFileName, string outputAinFileName)
        {
            var ainFile = new AinFile(inputAinFileName);
            var importer = new TextImportExport(ainFile);

            var wordWrapOptions = WordWrapOptions.GetWordWrapOptions(ainFile);
            importer.wordWrapOptions = wordWrapOptions;

            return importer.ReplaceText(textFileName, outputAinFileName);
        }

        private static void QuickCompileCodePatch(string inputAinFileName, string jafFileName, string outputAinFileName)
        {
            var ainFile = new AinFile(inputAinFileName);
            var compiler = new Compiler.Compiler(ainFile, 0);
            compiler.Error += new EventHandler<ErrorEventArgs>(compiler_Error);
            compiler.CompileCodeInPlace(new string[] { jafFileName });
            ainFile.WriteAndEncryptAinFile(outputAinFileName, ZLibNet.CompressionLevel.BestSpeed);
        }

        private static void BuildProject(string inputProjectFilename, string outputAinFilename)
        {
            var reader = new AssemblerProjectReader();
            reader.LoadProject(inputProjectFilename);
            var ainFile = reader.MakeAinFile();
            if (outputAinFilename.EndsWith("_"))
            {
                ainFile.WriteAinFile(outputAinFilename);
            }
            else
            {
                ainFile.WriteAndEncryptAinFile(outputAinFilename);
            }
        }

        private static void CreateProject(string inputAinFilename, string outputProjectFilename)
        {
            var ainFile = new AinFile(inputAinFilename);
            ainFile.LoadAlternativeNames();
            //var alternativeNames = new AlternativeNames(inputAinFilename);
            var writer = new AssemblerProjectWriter(ainFile);
            //writer.AlternativeNames = alternativeNames;
            writer.SaveAsProject(outputProjectFilename, false);
        }
    }

    public static partial class Util
    {
        public static void RunWhenIdle(Action action)
        {
            Action<object, EventArgs> handler = null;

            handler = (sender, e) =>
            {
                Application.Idle -= new EventHandler(handler);
                action();
            };

            Application.Idle += new EventHandler(handler);
        }

    }
}
