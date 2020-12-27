using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ZLibNet;

namespace AinDecompiler
{
    /// <summary>
    /// An AIN file from AliceSoft games.
    /// </summary>
    public class AinFile
    {
        //used so the debugger can find an AIN file somewhere even if the currently executing function can't see one
        [Browsable(false)]
        public static AinFile defaultInstance
        {
            get
            {
                return Expression.defaultAinFile;
            }
        }

        //used when finding function calls when compiling on top of existing AIN
        [Browsable(false)]
        public static AinFile LastLoadedAinFile;

        /// <summary>
        /// The list of filenames used by the EOF instruction
        /// </summary>
        public StringCollection Filenames = new StringCollection();    //FNAM
        /// <summary>
        /// The list of functions (functions contain parameters and local variables)
        /// </summary>
        public FunctionCollection Functions = new FunctionCollection();  //FUNC
        /// <summary>
        /// The list of strings (some strings represent function names)
        /// </summary>
        public StringCollection Strings = new StringCollection();      //STR0
        /// <summary>
        /// The list of dialog messages displayed by the game
        /// </summary>
        public StringCollection Messages = new StringCollection();     //MSG0
        /// <summary>
        /// The code for the game.  Instructions are 2 bytes wide, instruction arguments are 4 bytes each.  See Instruction.cs for more information.
        /// </summary>
        public byte[] Code;           //CODE
        /// <summary>
        /// The version of the game.  Version 4 is the first version, Version 5 adds groups for global variables, Version 6 adds many new instructions.
        /// </summary>
        public int Version = -1;           //VERS
        /// <summary>
        /// Whether or not to use a compressed (AI2) file instead of an uncompressed AIN file
        /// </summary>
        public bool IsAi2File;
        /// <summary>
        /// Not sure exactly what this is for.  In version 6, always seems to be zeroes.
        /// </summary>
        public int KeyCode;           //KEYC
        /// <summary>
        /// The list of global variables.  In version 5 and later, global variables can be put into separate groups when being saved and loaded.
        /// </summary>
        public GlobalCollection Globals = new GlobalCollection();      //GLOB
        /// <summary>
        /// The initial values for global variables.  May be an int, bool, float, or a string.
        /// </summary>
        public GlobalInitialValueCollection GlobalInitialValues = new GlobalInitialValueCollection();   //GSET
        /// <summary>
        /// The list of structs or classes.
        /// </summary>
        public StructCollection Structs = new StructCollection();      //STRT
        /// <summary>
        /// The ID number of the main function (entry point)
        /// </summary>
        public int MAIN;              //MAIN
        /// <summary>
        /// The ID number of the function called when a message is displayed.
        /// </summary>
        public int MSGF;              //MSGF
        /// <summary>
        /// The list of libraries, which contain the library functions
        /// </summary>
        public HllLibraryCollection Libraries = new HllLibraryCollection();     //HLL0
        /// <summary>
        /// The list of switch blocks.  Used by the SWITCH and STRSWITCH instructions.
        /// </summary>
        public SwitchBlockCollection Switches = new SwitchBlockCollection();     //SWI0
        /// <summary>
        /// The version of the game.  100 means 1.00, 107 means 1.07, etc...
        /// </summary>
        public int GameVersion;       //GVER
        /// <summary>
        /// The ID number for the onjump function, called when using the jump statement
        /// </summary>
        public int OJMP;              //OJMP
        /// <summary>
        /// The list of function types
        /// </summary>
        public FuncTypeCollection FunctionTypes = new FuncTypeCollection();  //FNCT
        /// <summary>
        /// The list of global variable groups used when the game saves and loads saved games.
        /// </summary>
        public StringCollection GlobalGroupNames = new StringCollection();  //OBJG
        /// <summary>
        /// The list of delegates (function pointers that can contain multiple functions)
        /// </summary>
        public FuncTypeCollection Delegates = new FuncTypeCollection();           //DELG

        /// <summary>
        /// A dictionary mapping struct names to their index in the Structs list
        /// </summary>
        [Browsable(false)]
        public Dictionary<string, int> StructNameToIndex = new Dictionary<string, int>();
        /// <summary>
        /// A dictionary mapping functions names to their index in the Functions list
        /// </summary>
        [Browsable(false)]
        public Dictionary<string, int> FunctionNameToIndex = new Dictionary<string, int>();
        /// <summary>
        /// A dictionary mapping global names to their index in the Globals list.  Both dotted and non-dotted forms are used.
        /// </summary>
        [Browsable(false)]
        public Dictionary<string, int> GlobalNameToIndex = new Dictionary<string, int>();
        /// <summary>
        /// A dictionary mapping system call names to their index in the system calls list.
        /// </summary>
        [Browsable(false)]
        public static readonly Dictionary<string, int> SystemCallNameToIndex = CreateSystemCallsDictionary();
        /// <summary>
        /// The list of system calls.  They all begin with "system." in their names.
        /// </summary>
        [Browsable(false)]
        public readonly static FunctionCollection SystemCalls = new FunctionCollection(CreateSystemCalls());

        public readonly static FunctionCollection BuiltInFunctions = new FunctionCollection(CreateBuiltInFunctions());
        public readonly static Dictionary<string, Function> BuiltInFunctionsInverse = BuiltInFunctions.ToDictionary(f => f.Name);

        /// <summary>
        /// Whether or not this AIN file is an "old version" seen in games like Daibanchou.
        /// This is identified by an old file version, and the lack of any directories named "system".
        /// Causes slightly different code generation behavior.
        /// </summary>
        //public bool IsOldVersion = false;

        private bool _ContainsDelgTag = false;

        /// <summary>
        /// The target version of Sys42VM (determined by heuristics)
        /// </summary>
        public int TargetVMVersion;


        public bool UseSimplifiedCode = false;

        /// <summary>
        /// The filename that the AIN file was loaded with.
        /// </summary>
        public string OriginalFilename = "";

        /// <summary>
        /// The MetadataFile associated with this AinFile.  Contains variable replacement names, enumeration types, default arguments, etc.
        /// </summary>
        [Browsable(false)]
        public MetadataFile MetadataFile;

        /// <summary>
        /// A cache containing decompiled code so that searches do not have to decompile the same function multiple times.
        /// </summary>
        [Browsable(false)]
        public DecompiledCodeCache DecompiledCodeCache;

        /// <summary>
        /// A cache containing partial or complete results of searches, so that less data needs to be scanned during searches.
        /// </summary>
        [Browsable(false)]
        public CodeSearchCache CodeSearchCache;

        /// <summary>
        /// Whether or not we are displaying Alternative Names instead of normal names.  Make sure this is set to false everywhere outside the explorer.
        /// </summary>
        [Browsable(false)]
        public bool DisplayAlternativeNames;

        /// <summary>
        /// The set of alternative names by lookup table, can be used instead of the names from the metadata.
        /// </summary>
        [Browsable(false)]
        public AlternativeNames AlternativeNames = new AlternativeNames();

        /// <summary>
        /// Creates a blank AIN file.  Call ReadAinFile next.
        /// </summary>
        public AinFile()
        {
            //if (Expression.defaultAinFile == null)
            //{
            //    Expression.defaultAinFile = this;
            //}
            this.DecompiledCodeCache = new DecompiledCodeCache(this);
            this.CodeSearchCache = new CodeSearchCache(this);
            this.MetadataFile = new MetadataFile(this);
        }

        /// <summary>
        /// Creates an AIN file from a non-encrypted file.
        /// </summary>
        /// <param name="fileName">The file to load.</param>
        public AinFile(string fileName)
            : this()
        {
            ReadAinFile(fileName);
        }

        /// <summary>
        /// Reads an AIN file from the disk.  Prompts to decrypt the AIN file if necessary.
        /// </summary>
        /// <param name="inputFileName">Filename to load and decrypt</param>
        /// <returns>True if the file was loaded, otherwise returns false.</returns>
        public bool ReadAndDecryptAinFile(string inputFileName)
        {
            return ReadAndDecryptAinFile(inputFileName, false);
        }

        /// <summary>
        /// Reads an AIN file from the disk.  Prompts to decrypt the AIN file if necessary.
        /// </summary>
        /// <param name="inputFileName">Filename to load and decrypt</param>
        /// <param name="noPrompt">If true, hides all message boxes and answers Yes to all of them.</param>
        /// <returns>True if the file was loaded, otherwise returns false.</returns>
        public bool ReadAndDecryptAinFile(string inputFileName, bool noPrompt)
        {
            //If we read the file successfully, exit now.  Otherwise try to read a different file or decrypt it.
            if (!ReadAinFile(inputFileName))
            {
                //check for a system 3.9 AIN file just to display a warning message.
                if (File.Exists(inputFileName))
                {
                    try
                    {
                        using (var fs = File.OpenRead(inputFileName))
                        {
                            var br = new BinaryReader(fs);
                            var first8bytes = br.ReadBytes(8);
                            byte[] ainiSignature = new byte[] { (byte)'A', (byte)'I', (byte)'N', (byte)'I', 1, 0, 0, 0 };
                            byte[] ainiSignature2 = new byte[] { (byte)'A', (byte)'I', (byte)'N', (byte)'2', 1, 0, 0, 0 };  //rance 5d download edition uses an alternative header
                            if (first8bytes.SequenceEqual(ainiSignature) || first8bytes.SequenceEqual(ainiSignature2))
                            {
                                if (!noPrompt)
                                {
                                    MessageBox.Show("System 3.9 .AIN files are not supported by this program.  Use Sys3Decompiler or DC39 instead.", "Unsupported File", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                }
                                return false;
                            }
                        }
                    }
                    catch (IOException ex)
                    {

                    }
                }

            }
            else
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Reads an AIN file from the disk.  May be encrypted or compressed.
        /// </summary>
        /// <param name="fileName">The name of the file to read.</param>
        /// <returns>Returns true if the file was successfully read, otherwise returns false.</returns>
        public bool ReadAinFile(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return false;
            }

            using (var ainFileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var br = new BinaryReader(ainFileStream);
                if (ReadAinFile(br))
                {
                    this.OriginalFilename = fileName;
                    DetectVMVersion();

                    LoadMetadata();
                    LoadAlternativeNames();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Reads an AIN file given a binary reader.  May be encrypted or compressed.
        /// </summary>
        /// <param name="br">The binary reader</param>
        /// <returns>Returns true if the file was successfully read, otherwise returns false.</returns>
        public bool ReadAinFile(BinaryReader br)
        {
            this.IsAi2File = CheckForAI2File(ref br);
            CheckForEncryptedFile(ref br);

            while (br.BaseStream.Position >= 0 && br.BaseStream.Position < br.BaseStream.Length)
            {
                //If it fails to read a known tag, assume it has hit the end of the file
                if (!ReadTag(br))
                {
                    //If a version tag wasn't successfully read, then the file failed to load.
                    if (Version == -1)
                    {
                        return false;
                    }
                    return false;
                }
            }

            //Set index, parent, and root values for everything
            ApplyIndexesAndParents();
            //fix function names for version 0
            if (Version == 0)
            {
                FixVersion0File();
            }
            //create the lookup tables (dictionaries)
            CreateLookups();
            Expression.defaultAinFile = this;
            AinFile.LastLoadedAinFile = this;
            return true;
        }

        private bool CheckForEncryptedFile(ref BinaryReader br)
        {
            if (this.IsAi2File == true)
            {
                return false;
            }
            var first8Bytes = br.ReadBytes(8);
            br.BaseStream.Position -= 8;
            DecodeAin.Decode(first8Bytes);
            if (ASCIIEncoding.ASCII.GetString(first8Bytes, 0, 4).Equals("VERS") && first8Bytes.Skip(5).AllEqualTo((byte)0))
            {
                int size = (int)(br.BaseStream.Length - br.BaseStream.Position);
                var allBytes = br.ReadBytes(size);
                DecodeAin.Decode(allBytes);
                MemoryStream ms = new MemoryStream(allBytes);
                BinaryReader br2 = new BinaryReader(ms);
                br = br2;
                return true;
            }
            return false;
        }

        private static bool CheckForAI2File(ref BinaryReader br)
        {
            //check for AI2 file - read first 8 bytes and see if it's AI2\0 \0\0\0\0
            var first8Bytes = br.ReadBytes(8);
            br.BaseStream.Position -= 8;
            if (ASCIIEncoding.ASCII.GetString(first8Bytes, 0, 3) == "AI2" && first8Bytes.Skip(3).AllEqualTo((byte)0))
            {
                BinaryReader br2 = DecompressAi2File(br);
                if (br2 != null)
                {
                    br = br2;
                    return true;
                }
            }
            return false;
        }

        private static BinaryReader DecompressAi2File(BinaryReader br)
        {
            try
            {
                br.BaseStream.Position += 8;
                int decompressedSize = br.ReadInt32();
                int compressedSize = br.ReadInt32();

                var compressedBytes = br.ReadBytes(compressedSize);
                var bytes = Decompress(compressedBytes, decompressedSize);
                if (bytes == null)
                {
                    return null;
                }
                return new BinaryReader(new MemoryStream(bytes));
            }
            catch
            {
                return null;
            }
        }

        private static byte[] Decompress(byte[] bytes, int size)
        {
            return Decompress(bytes, size, 0);
        }

        private static byte[] Decompress(byte[] bytes, int size, int offset)
        {
            var ms = new MemoryStream(bytes);
            ms.Position = offset;
            ZLibStream zlibStream = new ZLibStream(ms, CompressionMode.Decompress);
            var brDeflate = new BinaryReader(zlibStream);
            return brDeflate.ReadBytes(size);
        }

        private void FixVersion0File()
        {
            //struct names to table
            Dictionary<string, Struct> structTable = new Dictionary<string, Struct>();
            foreach (var structInfo in Structs)
            {
                structTable[structInfo.Name] = structInfo;
            }

            foreach (var structInfo in Structs)
            {
                var cons = GetFunction(structInfo.Constructor);
                var des = GetFunction(structInfo.Destructor);
                if (cons != null)
                {
                    if (cons.Name == structInfo.Name)
                    {
                        cons.Name = structInfo.Name + "@0";
                    }
                }
                if (des != null)
                {
                    if (des.Name == structInfo.Name)
                    {
                        des.Name = structInfo.Name + "@1";
                    }
                }

            }

            foreach (var function in Functions)
            {
                if (structTable.ContainsKey(function.Name))
                {
                    function.Name += "@func" + function.Index.ToString();
                }
            }

            foreach (var global in Globals)
            {
                if (structTable.ContainsKey(global.Name))
                {
                    global.Name = "_" + global.Name;
                }
            }

            this.Filenames.AddRange(this.Messages);
            this.Strings.AddRange(this.Messages);
        }

        static void GetDllVersion(string fileName, ref int version)
        {
            if (File.Exists(fileName))
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(fileName);
                if (versionInfo != null)
                {
                    string[] pieces = versionInfo.FileVersion.Split(", ");
                    if (pieces.Length >= 3)
                    {
                        version = (pieces[0][0] - '0') * 100 + (pieces[1][0] - '0') * 10 + (pieces[2][0] - '0');
                    }
                }
            }
        }

        bool _AnySystemFunctions()
        {
            return this.Filenames.Any(name => name.StartsWith("system", StringComparison.OrdinalIgnoreCase) || name.StartsWith("システム"));
        }

        bool _LibrariesContains(string libraryName)
        {
            return this.Libraries.Any(lib => lib.LibraryName.Equals(libraryName, StringComparison.OrdinalIgnoreCase));
        }

        static int _GetSys42DllVersion(string ainFileName)
        {
            int dllFileVersion = -1;
            string dllDirectory = Path.Combine(Path.GetDirectoryName(ainFileName), "DLL");
            GetDllVersion(Path.Combine(dllDirectory, "sys40vm.dll"), ref dllFileVersion);
            GetDllVersion(Path.Combine(dllDirectory, "sys42vm.dll"), ref dllFileVersion);
            GetDllVersion(Path.Combine(dllDirectory, "sys43vm.dll"), ref dllFileVersion);
            if (dllFileVersion == -1)
            {
                dllDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ainFileName), "..\\DLL"));
                GetDllVersion(Path.Combine(dllDirectory, "sys40vm.dll"), ref dllFileVersion);
                GetDllVersion(Path.Combine(dllDirectory, "sys42vm.dll"), ref dllFileVersion);
                GetDllVersion(Path.Combine(dllDirectory, "sys43vm.dll"), ref dllFileVersion);
            }
            return dllFileVersion;
        }

        void DetectVMVersion()
        {
            int dllFileVersion = _GetSys42DllVersion(this.OriginalFilename);

            int detectedVersion = -1;

            if (this.Version == 0)
            {
                //DALK Gaiden is the only version 0 game, and uses sys40vm.dll version 1.0.0
                detectedVersion = 100;
            }
            if (this.Version == 4)
            {
                //known versions and games:
                //206 - Daibanchou
                //210 - Prince-sama LV2 trial (Alice Blue)
                //210 - Atonement of a witch
                //216 - Rance 6
                //219 - Dungeons and dolls
                //221 - Popolytan

                //versions that matter: 206, 216, 219, 221
                detectedVersion = 221;

                //Guess game based on libraries used
                if (_LibrariesContains("Gpx2Plus"))
                {
                    bool anyFuncTypes = this.FunctionTypes.Count > 0;
                    bool anySystemFunctions = _AnySystemFunctions();

                    if (anyFuncTypes == false && anySystemFunctions == false)
                    {
                        //Daibanchou
                        detectedVersion = 206;
                    }
                }

                if (_LibrariesContains("DrawDungeon") && _LibrariesContains("AliceLogo"))
                {
                    //Rance 6
                    detectedVersion = 216;
                }
                else if (_LibrariesContains("DrawDungeon2") && _LibrariesContains("AliceLogo2"))
                {
                    //Dungeons and Dolls
                    detectedVersion = 219;
                }
                else if (_LibrariesContains("DrawRain") && _LibrariesContains("DrawRipple") && _LibrariesContains("DataFile") && _LibrariesContains("File") && _LibrariesContains("AliceLogo2"))
                {
                    //atonement of a witch
                    detectedVersion = 210;
                }
                else if (_LibrariesContains("Data") && _LibrariesContains("File") && _LibrariesContains("SACT2") && _LibrariesContains("String"))
                {
                    //prince-sama LV2 trial (Alice Blue)
                    detectedVersion = 210;
                }

                if (dllFileVersion >= 200 && dllFileVersion <= 234)
                {
                    if (detectedVersion != dllFileVersion)
                    {

                    }
                    detectedVersion = dllFileVersion;
                }
            }
            if (this.Version == 5)
            {
                //known versions and games:
                //225 - galzoo island
                //226 - yokubari saboten
                //226 - yokushima gaiden
                //227 - tsumashibori, sengoku rance
                //2xx - double teacher life
                //232 - beat blades haruka
                //233 - sanbikiga
                //234 - toushin toshi 3
                //302 - Bokudake no hokenshitsu
                //don't know of any significant version differences

                detectedVersion = 227;

                if (_LibrariesContains("DrawDungeon14") && _LibrariesContains("AliceLogo2"))
                {
                    //galzoo island
                    detectedVersion = 225;
                }
                else if (_LibrariesContains("SACT2"))
                {
                    //if (_LibrariesContains("ACXLoader") && !_LibrariesContains("DrawSnow") && !_LibrariesContains("InputString"))
                    //{
                    //    //yokubari saboten?  Not sure about this check
                    //    detectedVersion = 226;
                    //}
                    detectedVersion = 227;
                }
                else if (_LibrariesContains("SACTDX"))
                {
                    detectedVersion = 232;
                    if (_LibrariesContains("PixelRestore"))
                    {
                        //Beat Blades haruka
                        detectedVersion = 232;
                    }
                    else if (_LibrariesContains("ReignEngine") && _LibrariesContains("MonsterInfo"))
                    {
                        //Toushin Toshi 3
                        detectedVersion = 234;
                    }
                    else if (_LibrariesContains("PassRegister"))
                    {
                        //sanbikiga
                        detectedVersion = 233;
                    }
                }
                else if (_LibrariesContains("StoatSpriteEngine"))
                {
                    detectedVersion = 302;
                    if (_LibrariesContains("AliceLogo4") && _LibrariesContains("Confirm3") && _LibrariesContains("ACXLoader"))
                    {
                        //Bokudake no hokenshitsu
                        detectedVersion = 302;
                    }
                }

                if (dllFileVersion >= 225 && dllFileVersion <= 302)
                {
                    if (dllFileVersion != detectedVersion)
                    {

                    }
                    detectedVersion = dllFileVersion;
                }
            }
            if (this.Version == 6)
            {
                //known versions and games:
                //304 - alice2010
                //403 - Daiteikoku
                //fake made-up version numbers:
                //405 - Rance Quest, Rance Quest Magnum
                //410 - Oyakorankan, drapeko, rance 01
                //420 - Rance 9, blade briders
                //430 - evenicle

                detectedVersion = 405;

                if (!_ContainsDelgTag)
                {
                    if (_CheckFunctionsForInstruction(Instruction.PAGE_REF))
                    {
                        //alice2010
                        detectedVersion = 304;
                        if (dllFileVersion >= 300 && dllFileVersion <= 399)
                        {
                            detectedVersion = dllFileVersion;
                        }
                    }

                    //daiteikoku is the last game to use AliceLogo
                    if (_LibrariesContains("AliceLogo5"))
                    {
                        detectedVersion = 403;
                    }
                }
                else
                {
                    //oyakorankan, draopeko, rance 01
                    detectedVersion = 410;
                    if (UsesMsg1)
                    {
                        //rance 9, blade briders
                        detectedVersion = 420;
                        if (!_CheckFunctionsForInstruction(Instruction.SH_LOCALREF, Instruction.SH_GLOBALREF, Instruction.SH_STRUCTREF))
                        {
                            //evenicle
                            this.UseSimplifiedCode = true;
                            detectedVersion = 430;
                        }
                    }
                }

                if (dllFileVersion >= 400 && dllFileVersion <= 409 && detectedVersion >= 400)
                {
                    detectedVersion = dllFileVersion;
                }
            }
            this.TargetVMVersion = detectedVersion;
            if (detectedVersion == 206)
            {
                //delete this later
                //this.IsOldVersion = true;
            }
        }

        static void _EnsureIndexWithinArrayBounds<T>(IEnumerable<T> arr, ref int index)
        {
            if (index < 0)
            {
                index = 0;
                return;
            }

            int count = arr.Count();

            if (index >= count)
            {
                index = count;
            }
        }

        bool _CheckFunctionsForInstruction(params Instruction[] instructionsToFindArr)
        {
            HashSet<int> instructionsToFind = new HashSet<int>(instructionsToFindArr.Select(ins => (int)ins));
            return _CheckFunctionsForInstruction(0, 100, instructionsToFind);
        }

        bool _CheckFunctionsForInstruction(int firstFunction, int lastFunction, HashSet<int> instructionsToFind)
        {
            _EnsureIndexWithinArrayBounds(Functions, ref firstFunction);
            _EnsureIndexWithinArrayBounds(Functions, ref lastFunction);

            var decompiler = new Decompiler(this);
            for (int i = firstFunction; i < lastFunction; i++)
            {
                var function = this.Functions[i];
                int startAddress = function.Address;
                int endAddress = decompiler.FindEndAddress(startAddress);
                int address = startAddress;
                while (address < endAddress)
                {
                    var ins = Peek(address);
                    address = ins.nextAddress;

                    if (instructionsToFind.Contains((int)ins.instruction))
                    {
                        return true;
                    }
                }
            }
            return false;

        }


        private void DetectOldVersion()
        {
            if (this.Version <= 4)
            {
                //any filenames start with "system" or "システム"?  It's not an old version.
                if (Filenames.Any(f =>
                    f.StartsWith("system\\", StringComparison.OrdinalIgnoreCase) ||
                    f.StartsWith("システム\\")))
                {
                    //this.IsOldVersion = false;
                }
                else
                {
                    if (this.Version == 4 && this.TargetVMVersion == 0)
                    {
                        this.TargetVMVersion = 206;
                    }
                    //this.IsOldVersion = true;
                }
            }

            if (this.Version == 6 && this.UsesMsg1 && this.FunctionTypes.Count == 0)
            {
                //look through last 50 function for SH_LOCALREF, SH_GLOBALREF, or SH_STRUCTREF, if they don't exist, it's simplified
                //Rance 9 and Blade Briders are detected on function #5
                int functionCount = Math.Max(this.Functions.Count, 20);
                int firstFunction = 0;
                if (firstFunction < 0) firstFunction = 0;

                for (int i = firstFunction; i < functionCount; i++)
                {
                    var decompiler = new Decompiler(this);

                    var function = this.Functions[i];
                    int startAddress = function.Address;
                    int endAddress = decompiler.FindEndAddress(startAddress);
                    int address = startAddress;
                    while (address < endAddress)
                    {
                        var ins = Peek(address);
                        address = ins.nextAddress;

                        switch (ins.instruction)
                        {
                            case Instruction.SH_LOCALREF:
                            case Instruction.SH_GLOBALREF:
                            case Instruction.SH_STRUCTREF:
                                goto leaveForLoop;
                                break;
                        }
                    }
                }
                this.UseSimplifiedCode = true;
            leaveForLoop:
                ;

            }
        }

        /// <summary>
        /// Wrties an AIN file to the disk.
        /// </summary>
        /// <param name="fileName">The filename to save to.</param>
        public void WriteAinFile(string fileName)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            WriteAinFile(bw);
            File.WriteAllBytes(fileName, ms.ToArray());
        }

        /// <summary>
        /// Writes an AIN file to the disk given a binary writer.
        /// </summary>
        /// <param name="bw">The binary writer to write to.</param>
        public void WriteAinFile(BinaryWriter bw)
        {
            //These are the tags we want to save, and the order to save them in.
            string[] tags = new string[]
            {
                "VERS","KEYC","CODE","FUNC","GLOB","GSET","STRT","MSG0","MAIN","MSGF","HLL0","SWI0","GVER","STR0","FNAM","OJMP","FNCT","DELG","OBJG",
            };

            foreach (var tagName in tags)
            {
                if (ShouldSaveTag(tagName))
                {
                    WriteTag(tagName, bw);
                }
            }
        }

        private bool ShouldSaveTag(string tagName)
        {
            bool shouldSaveTag = true;
            if (tagName == "OBJG")
            {
                if (this.Version < 5)
                {
                    shouldSaveTag = false;
                }
            }
            else if (tagName == "DELG")
            {
                if (this._ContainsDelgTag || this.TargetVMVersion >= 410)
                {
                    shouldSaveTag = true;
                }
                else if (this.Delegates == null || this.Delegates.Count == 0)
                {
                    shouldSaveTag = false;
                }
                else if (this.Version < 6)
                {
                    shouldSaveTag = false;
                }
                else if (this.Delegates.Count > 0)
                {
                    shouldSaveTag = true;
                }
            }
            return shouldSaveTag;
        }

        /// <summary>
        /// Creates the lookup tables (dictionaries) used to find Functions and Structs by name.
        /// </summary>
        public void CreateLookups()
        {
            //Create the lookup table for Structs
            StructNameToIndex.Clear();
            for (int i = 0; i < Structs.Count; i++)
            {
                var structInfo = Structs[i];
                StructNameToIndex.Set(structInfo.Name, i);
            }

            //Create the lookup table for Functions
            FunctionNameToIndex.Clear();
            for (int i = 0; i < Functions.Count; i++)
            {
                var function = Functions[i];
                FunctionNameToIndex.Set(function.Name, i);
            }

            //Create the lookup table for globals
            GlobalNameToIndex.Clear();
            for (int i = 0; i < Globals.Count; i++)
            {
                var global = Globals[i];
                GlobalNameToIndex.Set(global.Name, i);
                if (global.GroupName != null)
                {
                    GlobalNameToIndex.Set(global.GroupName + "." + global.Name, i);
                }
            }

        }

        /// <summary>
        /// Creates the list of System calls.  This applies to all AIN files.
        /// </summary>
        /// <returns>The list of system calls.</returns>
        private static Function[] CreateSystemCalls()
        {
            var systemCalls = new Function[]{
                //These are official names from the SDK documentation
                Function.Create(DataType.Void, "system.Exit", DataType.Int, "nResult"),
                Function.Create(DataType.Int, "system.GlobalSave", DataType.String, "szKeyName", DataType.String, "szFileName"),
                Function.Create(DataType.Int, "system.GlobalLoad", DataType.String, "szKeyName", DataType.String, "szFileName"),
                Function.Create(DataType.Int, "system.LockPeek"),
                Function.Create(DataType.Int, "system.UnlockPeek"),
                Function.Create(DataType.Void, "system.Reset"),
                Function.Create(DataType.String, "system.Output", DataType.String, "szText"),
                Function.Create(DataType.String, "system.MsgBox", DataType.String, "szText"),
                Function.Create(DataType.Int, "system.ResumeSave", DataType.String, "szKeyName", DataType.String, "szFileName", DataType.RefInt, "nResult", DataType.Void,"<void>"),
                Function.Create(DataType.Void, "system.ResumeLoad", DataType.String, "szKeyName", DataType.String, "szFileName"),
                Function.Create(DataType.Int, "system.ExistFile", DataType.String, "szFileName"),
                Function.Create(DataType.Void, "system.OpenWeb", DataType.String, "szURL"),
                Function.Create(DataType.String, "system.GetSaveFolderName"),
                Function.Create(DataType.Int, "system.GetTime"),
                Function.Create(DataType.String, "system.GetGameName"),
                Function.Create(DataType.String, "system.Error", DataType.String, "szText"),
                Function.Create(DataType.Int, "system.ExistSaveFile", DataType.String, "szFileName"),
                Function.Create(DataType.Int, "system.IsDebugMode"),
                Function.Create(DataType.Int, "system.MsgBoxOkCancel", DataType.String, "szText"),
                Function.Create(DataType.String, "system.GetFuncStackName", DataType.Int, "nIndex"),
                Function.Create(DataType.Void, "system.Peek"),
                Function.Create(DataType.Void, "system.Sleep", DataType.Int, "nSleep"),
                Function.Create(DataType.Bool, "system.ResumeWriteComment", DataType.String, "szKeyName", DataType.String, "szFileName", DataType.RefArrayString, "aszComment"),
                Function.Create(DataType.Bool, "system.ResumeReadComment", DataType.String, "szKeyName", DataType.String, "szFileName", DataType.RefArrayString, "aszComment"),
                //Past this point, parameter names are guesses
                Function.Create(DataType.Int, "system.GroupSave", DataType.String, "szKeyName", DataType.String, "szFileName", DataType.String, "szGroupName", DataType.RefInt, "nSaveNumber",DataType.Void,"<void>"),
                Function.Create(DataType.Int, "system.GroupLoad", DataType.String, "szKeyName", DataType.String, "szFileName", DataType.String, "szGroupName", DataType.RefInt, "nSaveNumber",DataType.Void,"<void>"),
                Function.Create(DataType.Int, "system.DeleteSaveFile", DataType.String, "szFileName"),
                Function.Create(DataType.Bool, "system.ExistFunc", DataType.String, "szFuncName"),
                Function.Create(DataType.Int, "system.CopySaveFile", DataType.String, "szDestFileName", DataType.String, "szSourceFileName"),
            };
            for (int i = 0; i < systemCalls.Length; i++)
            {
                systemCalls[i].Index = i;
            }
            return systemCalls;
        }

        /// <summary>
        /// Creates the list of built in functions, such as string or array functions.  Used when compiling.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<Function> CreateBuiltInFunctions()
        {
            var arrayNumOf = Function.Create(DataType.Int, "array.Numof", DataType.Int, "dimensionNumber");
            var arraySort = Function.Create(DataType.Void, "array.Sort", DataType.Int, "sortFunction");
            var stringErase = Function.Create(DataType.Void, "string.Erase", DataType.Int, "nIndex", DataType.Int, "dummy");

            //DON'T DO THIS!
            //arrayNumOf.Parameters[0].GetMetadata().DefaultValue = new InitialValue(0);
            //arraySort.Parameters[0].GetMetadata().DefaultValue = new InitialValue(0);
            //stringErase.Parameters[1].GetMetadata().DefaultValue = new InitialValue(1);

            Function[] functions = new Function[]{
                Function.Create(DataType.String, "int.String", DataType.Int, "number"),
                Function.Create(DataType.String, "int_string", DataType.Int, "number"),
                Function.Create(DataType.String, "float_string", DataType.Float, "number"),
                Function.Create(DataType.String, "float.String", DataType.Float, "number", DataType.Int, "decimalPlaces"),
                Function.Create(DataType.Float, "int_float", DataType.Int, "number"),
                //Function.Create(DataType.Float, "string_float", DataType.String, "stringValue"),
                Function.Create(DataType.Int, "string_int", DataType.String, "stringValue"),
                Function.Create(DataType.Int, "float_int", DataType.Float, "number"),
                Function.Create(DataType.Int, "string.Int"),
                Function.Create(DataType.Int, "string.Length"),
                Function.Create(DataType.Int, "string.LengthByte"),
                Function.Create(DataType.Int, "string.Empty"),
                Function.Create(DataType.Int, "string.Find", DataType.String, "szKey"),
                Function.Create(DataType.String, "string.GetPart", DataType.Int, "nIndex", DataType.Int, "nLength"),
                Function.Create(DataType.Void, "string.PushBack", DataType.Int, "nChara"),
                Function.Create(DataType.Void, "string.PopBack"),
                stringErase,
                Function.Create(DataType.Void, "array.Alloc", DataType.Int, "numberOfElements"),
                Function.Create(DataType.Void, "array.Realloc", DataType.Int, "numberOfElements"),
                Function.Create(DataType.Void, "array.Free"),
                arrayNumOf,
                Function.Create(DataType.Int, "array.Copy", DataType.Int, "nDestIndex", DataType.RefArrayInt, "a", DataType.Int, "nSourceIndex", DataType.Int, "nCount"),
                Function.Create(DataType.Int, "array.Fill", DataType.Int, "nIndex", DataType.Int, "nCount", DataType.Int, "value"),
                Function.Create(DataType.Void, "array.PushBack", DataType.Int, "value"),
                Function.Create(DataType.Void, "array.PopBack"),
                Function.Create(DataType.Int, "array.Empty"),
                Function.Create(DataType.Int, "array.Erase", DataType.Int, "nIndex"),
                Function.Create(DataType.Void, "array.Insert", DataType.Int, "nIndex", DataType.Int, "value"),
                arraySort,
                Function.Create(DataType.Int, "array.Find", DataType.Int, "startIndex", DataType.Int, "endIndex", DataType.Int, "lookFor", DataType.Int, "comparisonFunction"),
                Function.Create(DataType.Void, "array.Reverse"),
                Function.Create(DataType.Int, "delegate.Numof"),
                Function.Create(DataType.Void, "delegate.Clear"),
                Function.Create(DataType.Void, "delegate.Add", DataType.Delegate, "value"),
                Function.Create(DataType.Void, "delegate.Erase", DataType.Delegate, "value"),
                Function.Create(DataType.Bool, "delegate.Exist", DataType.Delegate, "value"),
            };
            return functions;
        }

        /// <summary>
        /// Builds the dictionary mapping system call names to their indexes
        /// </summary>
        /// <returns>The dictionary that maps system call names to their indexes</returns>
        private static Dictionary<string, int> CreateSystemCallsDictionary()
        {
            Dictionary<string, int> dic = new Dictionary<string, int>();
            var calls = CreateSystemCalls();
            for (int i = 0; i < calls.Length; i++)
            {
                dic.Add(calls[i].Name, i);
            }
            return dic;
        }

        /// <summary>
        /// Reads a tag from the AIN file.  If it can not find a known tag, returns false.
        /// </summary>
        /// <param name="br">The binary reader</param>
        /// <returns>True if the tag was read, False if the tag was unknown or failed to read.</returns>
        private bool ReadTag(BinaryReader br)
        {
            int length;
            int count;
            string tagName = br.ReadStringFixedSize(4);
            switch (tagName)
            {
                case "VERS":
                    Version = br.ReadInt32();
                    break;
                case "KEYC":
                    KeyCode = br.ReadInt32();
                    break;
                case "CODE":
                    length = br.ReadInt32();
                    Code = br.ReadBytes(length);
                    break;
                case "FUNC":
                    length = br.ReadInt32();
                    Functions.Clear();
                    Functions.AddRange(Util.Read<Function>(br, length, Version));
                    break;
                case "GLOB":
                    length = br.ReadInt32();
                    Globals.Clear();
                    Globals.AddRange(Util.Read<Global>(br, length, Version));
                    break;
                case "GSET":
                    length = br.ReadInt32();
                    GlobalInitialValues.Clear();
                    GlobalInitialValues.AddRange(Util.Read<GlobalInitialValue>(br, length));
                    break;
                case "STRT":
                    length = br.ReadInt32();
                    Structs.Clear();
                    Structs.AddRange(Util.Read<Struct>(br, length));
                    break;
                case "MSG0":
                    length = br.ReadInt32();
                    Messages.Clear();
                    Messages.AddRange(Util.ReadStrings(br, length));
                    break;
                case "MAIN":
                    MAIN = br.ReadInt32();
                    break;
                case "MSGF":
                    MSGF = br.ReadInt32();
                    break;
                case "HLL0":
                    length = br.ReadInt32();
                    Libraries.Clear();
                    Libraries.AddRange(Util.Read<HllLibrary>(br, length));
                    break;
                case "SWI0":
                    length = br.ReadInt32();
                    Switches.Clear();
                    Switches.AddRange(Util.Read<SwitchBlock>(br, length));
                    break;
                case "GVER":
                    GameVersion = br.ReadInt32();
                    break;
                case "STR0":
                    length = br.ReadInt32();
                    Strings.Clear();
                    Strings.AddRange(Util.ReadStrings(br, length));
                    break;
                case "FNAM":
                    length = br.ReadInt32();
                    Filenames.Clear();
                    Filenames.AddRange(Util.ReadStrings(br, length));
                    break;
                case "OJMP":
                    OJMP = br.ReadInt32();
                    break;
                case "FNCT":
                    length = br.ReadInt32() - 4;
                    count = br.ReadInt32();
                    FunctionTypes.Clear();
                    FunctionTypes.AddRange(Util.Read<FunctionType>(br, count));
                    break;
                case "DELG":
                    length = br.ReadInt32() - 4;
                    count = br.ReadInt32();
                    this._ContainsDelgTag = true;
                    this.Delegates.Clear();
                    this.Delegates.AddRange(Util.Read<FunctionType>(br, count));
                    break;
                case "OBJG":
                    length = br.ReadInt32();
                    GlobalGroupNames.Clear();
                    GlobalGroupNames.AddRange(Util.ReadStrings(br, length));
                    break;
                case "MSG1":
                    ReadMsg1(br);
                    break;
                default:
                    if (this.Version != -1)
                    {
                        MessageBox.Show("Unsupported tag: " + tagName, "AinDecompiler", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    }
                    //TODO: seek ahead to a known tag?
                    return false;
            }
            return true;
        }

        public bool UsesMsg1 = false;

        private void WriteMsg1(BinaryWriter bw)
        {
            int count = this.Messages.Count;
            bw.Write(count);
            bw.Write(this.Msg1Unknown);
            for (int i = 0; i < count; i++)
            {
                var bytes = Extensions.BinaryEncoding.GetBytes(this.Messages[i]);
                for (int b = 0; b < bytes.Length; b++)
                {
                    bytes[b] += (byte)b;
                    bytes[b] += 0x60;
                }
                bw.Write((int)bytes.Length);
                bw.Write(bytes);
            }
        }

        public int Msg1Unknown = 0;

        private void ReadMsg1(BinaryReader br)
        {
            this.UsesMsg1 = true;
            int count = br.ReadInt32();
            this.Msg1Unknown = br.ReadInt32();
            this.Messages.Clear();
            for (int i = 0; i < count; i++)
            {
                int length = br.ReadInt32();
                var bytes = br.ReadBytes(length);
                for (int b = 0; b < bytes.Length; b++)
                {
                    bytes[b] -= (byte)b;
                    bytes[b] -= 0x60;
                }
                string message = Extensions.BinaryEncoding.GetString(bytes);
                Messages.Add(message);
            }
        }

        /// <summary>
        /// Writes a TAG to the AIN file.
        /// </summary>
        /// <param name="tagName">The name of the tag, also selects what tag data gets written.</param>
        /// <param name="bw">The binary writer to write to.</param>
        private void WriteTag(string tagName, BinaryWriter bw)
        {
            bw.WriteStringFixedSize(tagName, 4);
            switch (tagName)
            {
                case "VERS":
                    bw.Write(Version);
                    break;
                case "KEYC":
                    bw.Write(KeyCode);
                    break;
                case "CODE":
                    bw.Write(Code.Length);
                    bw.Write(Code);
                    break;
                case "FUNC":
                    bw.Write(Functions.Count);
                    Util.Write(bw, Functions, Version);
                    break;
                case "GLOB":
                    bw.Write(Globals.Count);
                    Util.Write(bw, Globals, Version);
                    break;
                case "GSET":
                    bw.Write(GlobalInitialValues.Count);
                    Util.Write(bw, GlobalInitialValues);
                    break;
                case "STRT":
                    bw.Write(Structs.Count);
                    Util.Write(bw, Structs);
                    break;
                case "MSG0":
                    if (this.UsesMsg1)
                    {
                        bw.BaseStream.Position -= 4;
                        bw.WriteStringFixedSize("MSG1", 4);
                        WriteMsg1(bw);
                    }
                    else
                    {
                        bw.Write(Messages.Count);
                        Util.WriteStrings(bw, Messages);
                    }
                    break;
                case "MAIN":
                    bw.Write(MAIN);
                    break;
                case "MSGF":
                    bw.Write(MSGF);
                    break;
                case "HLL0":
                    bw.Write(Libraries.Count);
                    Util.Write(bw, Libraries);
                    break;
                case "SWI0":
                    bw.Write(Switches.Count);
                    Util.Write(bw, Switches);
                    break;
                case "GVER":
                    bw.Write(GameVersion);
                    break;
                case "STR0":
                    bw.Write(Strings.Count);
                    Util.WriteStrings(bw, Strings);
                    break;
                case "FNAM":
                    bw.Write(Filenames.Count);
                    Util.WriteStrings(bw, Filenames);
                    break;
                case "OJMP":
                    bw.Write(OJMP);
                    break;
                case "FNCT":
                    {
                        if (this.Version >= 6 && this.UsesMsg1 && this.FunctionTypes.Count == 0 && this.Delegates.Count > 0)
                        {
                            bw.BaseStream.Position -= 4;
                        }
                        else
                        {
                            WriteFunctionTypes(bw);
                        }
                    }
                    break;
                case "DELG":
                    {
                        WriteDelegates(bw);
                    }
                    break;
                case "OBJG":
                    bw.Write(GlobalGroupNames.Count);
                    Util.WriteStrings(bw, GlobalGroupNames);
                    break;
            }
        }

        private void WriteDelegates(BinaryWriter bw)
        {
            WriteFunctionTypesCommon(bw, this.Delegates);
        }

        private void WriteFunctionTypes(BinaryWriter bw)
        {
            WriteFunctionTypesCommon(bw, this.FunctionTypes);
        }

        private static void WriteFunctionTypesCommon<T>(BinaryWriter bw, IList<T> functionTypes) where T : IReadable
        {
            int startPosition = (int)bw.BaseStream.Position;
            bw.Write(0); //write zero, later change this to the actual length - 4
            bw.Write(functionTypes.Count);
            Util.Write(bw, functionTypes);
            int endPosition = (int)bw.BaseStream.Position;
            bw.BaseStream.Position = startPosition;
            bw.Write(endPosition - startPosition);
            bw.BaseStream.Position = endPosition;
        }

        public static CompressionLevel DefaultCompressionLevel = CompressionLevel.Level9;

        /// <summary>
        /// Writes an AIN file, and encrypts or compresss it.
        /// </summary>
        /// <param name="fileName">The filename to write to.</param>
        public void WriteAndEncryptAinFile(string fileName)
        {
            WriteAndEncryptAinFile(fileName, DefaultCompressionLevel);
        }

        /// <summary>
        /// Writes an AIN file, and encrypts or compresss it.
        /// </summary>
        /// <param name="fileName">The filename to write to.</param>
        public void WriteAndEncryptAinFile(string fileName, CompressionLevel compressionLevel)
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".ain_")
            {
                //force creating non-encoded file if user specifies .ain_ as the extension
                WriteAinFile(fileName);
                return;
            }

            bool wantAi2File = this.IsAi2File || this.Version >= 6;
            //new code: compress and save AI2 file in place
            if (wantAi2File)
            {
                WriteAi2File(fileName, compressionLevel);
                return;
            }
            else
            {
                MemoryStream ms = new MemoryStream();
                BinaryWriter bw = new BinaryWriter(ms);
                WriteAinFile(bw);
                byte[] bytes = ms.ToArray();
                DecodeAin.Decode(bytes);
                File.WriteAllBytes(fileName, bytes);
            }
        }

        private void WriteAi2File(string fileName, CompressionLevel compressionLevel)
        {
            int compressedSize, decompressedSize;
            var ms2 = new MemoryStream();
            {
                var ms = new MemoryStream();
                {
                    var bw = new BinaryWriter(ms);
                    WriteAinFile(bw);
                    decompressedSize = (int)bw.BaseStream.Length;
                }
                ms.Position = 0;
                var zlibStream = new ZLibStream(ms2, CompressionMode.Compress, compressionLevel);
                zlibStream.WriteFromStream(ms);
                zlibStream.Flush();
                compressedSize = (int)ms2.Length;
            }

            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                var bw2 = new BinaryWriter(fs);
                bw2.WriteStringNullTerminated("AI2");
                bw2.Write((int)0);
                bw2.Write((int)decompressedSize);
                bw2.Write((int)compressedSize);
                ms2.Position = 0;
                bw2.BaseStream.WriteFromStream(ms2);
                bw2.Flush();
                fs.Flush();
                fs.Close();
            }
        }

        /// <summary>
        /// Sets the index for every element of an array.
        /// </summary>
        /// <param name="list">The list to set indexes of</param>
        void SetIndexes<T>(IEnumerable<T> list) where T : IWithIndex
        {
            if (list != null)
            {
                int i = 0;
                foreach (var item in list)
                {
                    item.Index = i;
                    i++;
                }
            }
        }

        /// <summary>
        /// Sets the Parent for every element of an array.
        /// </summary>
        /// <param name="list">The list to set the parents of</param>
        /// <param name="parent">The parent object to use</param>
        void SetParents<T>(IEnumerable<T> list, object parent) where T : IWithParent
        {
            if (list != null)
            {
                foreach (var item in list)
                {
                    item.Parent = parent;
                }
            }
        }

        /// <summary>
        /// Sets the root for every element of an array.  (Root is the AinFile object)
        /// </summary>
        /// <param name="list">The list to set the roots of.</param>
        /// <param name="rootObject">The root object to use</param>
        void SetRoots<T>(IEnumerable<T> list, AinFile rootObject) where T : IWithRoot<AinFile>
        {
            if (list != null)
            {
                foreach (var item in list)
                {
                    item.Root = rootObject;
                }
            }
        }

        /// <summary>
        /// Sets the index and parents to a list of items recursively (base list has no parent)
        /// </summary>
        /// <typeparam name="T">The type of the list item</typeparam>
        /// <param name="list">The list of items to set.</param>
        void ApplyIndexesAndParents<T>(IEnumerable<T> list)
        {
            ApplyIndexesAndParents(list, null);
        }

        /// <summary>
        /// Sets the index, parents, and Root to a list of items recursively
        /// </summary>
        /// <typeparam name="T">The type of the list item</typeparam>
        /// <param name="list">The list of items t oset.</param>
        /// <param name="parentObject">The parent object to set (possibly null)</param>
        void ApplyIndexesAndParents<T>(IEnumerable<T> list, object parentObject)
        {
            //Set indexes, parents, and roots of the current list.
            SetIndexes(list.OfType<IWithIndex>());
            SetParents(list.OfType<IWithParent>(), parentObject);
            SetRoots(list.OfType<IWithRoot<AinFile>>(), this);

            //If each element of the list has has child elements (is a 'function'), apply to those too.
            var functions = list.OfType<IFunction>();
            if (functions != null)
            {
                foreach (var function in functions)
                {
                    ApplyIndexesAndParents(function.Parameters, function);
                }
            }
        }

        /// <summary>
        /// Sets indexes, parents, and Root to all the arrays of this class.
        /// </summary>
        public void ApplyIndexesAndParents()
        {
            ApplyIndexesAndParents(this.Functions);
            ApplyIndexesAndParents(this.FunctionTypes);
            ApplyIndexesAndParents(this.GlobalInitialValues);
            ApplyIndexesAndParents(this.Globals);
            ApplyIndexesAndParents(this.Libraries);
            ApplyIndexesAndParents(this.Structs);
            ApplyIndexesAndParents(this.Switches);
            ApplyIndexesAndParents(this.Delegates);
            foreach (var switchBlock in this.Switches)
            {
                ApplyIndexesAndParents(switchBlock.SwitchCases);
            }
        }

        public AinFile ShallowClone()
        {
            var clone = this.MemberwiseClone() as AinFile;
            return clone;
        }

        /// <summary>
        /// Clones an AIN file, but does not clone the code.  Used when replacing text with a translation, so the original copy remains in memory when exporting.
        /// </summary>
        /// <returns>A copy of the AIN file.</returns>
        public AinFile Clone()
        {
            //does not clone code
            var clone = new AinFile();
            clone.Code = this.Code;
            //clone.Dummy = this.Dummy;
            clone.Filenames.AddRange(this.Filenames.ToList());
            clone.FunctionNameToIndex = new Dictionary<string, int>();
            clone.GlobalNameToIndex = new Dictionary<string, int>();
            clone.Functions.AddRange(this.Functions.Select(f => f.Clone()));
            clone.FunctionTypes.AddRange(this.FunctionTypes.Select(f => f.Clone()));
            clone.Delegates.AddRange(this.Delegates.Select(f => f.Clone()));
            clone.GameVersion = this.GameVersion;
            clone.GlobalInitialValues.AddRange(this.GlobalInitialValues.Select(g => g.Clone()));
            clone.GlobalGroupNames.AddRange(this.GlobalGroupNames);
            clone.Globals.AddRange(this.Globals.Select(g => g.Clone()));
            clone.KeyCode = this.KeyCode;
            clone.Libraries.AddRange(this.Libraries.Select(l => l.Clone()));
            clone.MAIN = this.MAIN;
            clone.Messages.AddRange(this.Messages);
            clone.MSGF = this.MSGF;
            clone.OJMP = this.OJMP;
            clone.OriginalFilename = Path.Combine(Path.GetDirectoryName(this.OriginalFilename), Path.GetFileNameWithoutExtension(this.OriginalFilename) + "_replaced" + Path.GetExtension(this.OriginalFilename));
            clone.Strings.AddRange(this.Strings);
            clone.StructNameToIndex = new Dictionary<string, int>();
            clone.Structs.AddRange(this.Structs.Select(s => s.Clone()));
            clone.Switches.AddRange(this.Switches.Select(s => s.Clone()));
            clone.Version = this.Version;
            clone.ApplyIndexesAndParents();
            clone.CreateLookups();

            clone.AlternativeNames = this.AlternativeNames;
            clone.MetadataFile = this.MetadataFile.Clone(clone);
            clone.DisplayAlternativeNames = this.DisplayAlternativeNames;

            clone.IsAi2File = this.IsAi2File;
            //clone.IsOldVersion = this.IsOldVersion;
            clone.TargetVMVersion = this.TargetVMVersion;
            clone.UseSimplifiedCode = this.UseSimplifiedCode;
            clone._ContainsDelgTag = this._ContainsDelgTag;
            clone.Msg1Unknown = this.Msg1Unknown;
            clone.UsesMsg1 = this.UsesMsg1;

            return clone;
        }

        #region Getters
        /// <summary>
        /// Returns the string from the Strings array, or null if the string does not exist.
        /// </summary>
        /// <param name="stringNumber">index of the Strings array</param>
        /// <returns>A string from the strings array, or null</returns>
        public string GetString(int stringNumber)
        {
            if (stringNumber >= 0 && stringNumber < this.Strings.Count)
            {
                return this.Strings[stringNumber];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the global variable from the Globals array, or null if the global variable does not exist.
        /// </summary>
        /// <param name="globalNumber">index of the Globals array</param>
        /// <returns>A global variable from the Globals array, or null</returns>
        public Global GetGlobal(int globalNumber)
        {
            if (globalNumber >= 0 && globalNumber < this.Globals.Count)
            {
                return this.Globals[globalNumber];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the global variable from the Globals array by name, or null if the global variable is not found.
        /// </summary>
        /// <param name="globalName">Name of the Global variable</param>
        /// <returns>A global variable from the Globals array, or null</returns>
        public Global GetGlobal(string globalName)
        {
            if (globalName == null)
            {
                return null;
            }
            if (this.GlobalNameToIndex.ContainsKey(globalName))
            {
                int index = this.GlobalNameToIndex[globalName];
                return GetGlobal(index);
            }
            return null;
        }

        /// <summary>
        /// Returns the function from the Functions array, or null if the function does not exist.
        /// </summary>
        /// <param name="functionNumber">index of the Functions array</param>
        /// <returns>A function from the Functions array, or null</returns>
        public Function GetFunction(int functionNumber)
        {
            if (functionNumber >= 0 && functionNumber < this.Functions.Count)
            {
                return this.Functions[functionNumber];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the function from the Functions array by looking up its name in FunctionNameToIndex, or null if the function does not exist.
        /// </summary>
        /// <param name="functionName">Name of the function to look up</param>
        /// <returns>A function from the Functions array, or null</returns>
        public Function GetFunction(string functionName)
        {
            if (functionName == null)
            {
                return null;
            }
            if (this.FunctionNameToIndex.ContainsKey(functionName))
            {
                return GetFunction(this.FunctionNameToIndex[functionName]);
            }
            else
            {
                if (functionName.Contains("::"))
                {
                    //find last index of ::
                    int lastIndex = functionName.LastIndexOf("::");
                    if (lastIndex != -1)
                    {
                        //fix formal function name to "@" function name
                        //TODO: Namespaces from AIN version 7

                        string structName = functionName.Substring(0, lastIndex);
                        string funcName = functionName.Substring(lastIndex + 2);
                        if (funcName == structName)
                        {
                            funcName = "0";
                        }
                        if (funcName.StartsWith("~"))
                        {
                            funcName = "1";
                        }
                        functionName = structName + "@" + funcName;


                        var function = GetFunction(functionName);
                        if (function == null)
                        {

                        }
                        return function;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Returns the struct from the Structs array, or null if the struct does not exist.
        /// </summary>
        /// <param name="structNumber">index of the Structs array</param>
        /// <returns>A struct from the Structs array, or null</returns>
        public Struct GetStruct(int structNumber)
        {
            if (structNumber >= 0 && structNumber < this.Structs.Count)
            {
                return this.Structs[structNumber];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the struct from the Structs array by looking up its name in StructNameToIndex, or null if the struct does not exist.
        /// </summary>
        /// <param name="sturctName">Name of the struct to look up</param>
        /// <returns>A struct from the Structs array, or null</returns>
        public Struct GetStruct(string sturctName)
        {
            if (sturctName == null)
            {
                return null;
            }
            if (this.StructNameToIndex.ContainsKey(sturctName))
            {
                return GetStruct(this.StructNameToIndex[sturctName]);
            }
            else
            {
                return null;
            }
        }

        public Variable GetStructMember(Struct structInfo, string memberName)
        {
            if (structInfo == null || memberName == null)
            {
                return null;
            }
            foreach (var memberInfo in structInfo.Members)
            {
                if (memberInfo.Name == memberName)
                {
                    return memberInfo;
                }
            }
            return null;
        }

        public Variable GetStructMember(Struct structInfo, int memberIndex)
        {
            if (structInfo == null)
            {
                return null;
            }
            if (memberIndex >= 0 && memberIndex < structInfo.Members.Count)
            {
                return structInfo.Members[memberIndex];
            }
            return null;
        }

        public Variable GetStructMember(string structName, string memberName)
        {
            return GetStructMember(GetStruct(structName), memberName);
        }

        public Variable GetStructMember(string structName, int memberIndex)
        {
            return GetStructMember(GetStruct(structName), memberIndex);
        }

        public Variable GetStructMember(int structIndex, int memberIndex)
        {
            return GetStructMember(GetStruct(structIndex), memberIndex);
        }

        public Variable GetStructMember(int structIndex, string memberName)
        {
            return GetStructMember(GetStruct(structIndex), memberName);
        }

        /// <summary>
        /// Returns a function parameter or local variable from a given function, or null if either parameter is out of bounds.
        /// </summary>
        /// <param name="functionNumber">index of the Functions array</param>
        /// <param name="index">index in the Parameters array</param>
        /// <returns>A variable from the function, or null</returns>
        public Variable GetFunctionParameter(int functionNumber, int index)
        {
            var function = GetFunction(functionNumber);
            return GetFunctionParameter(function, index);
        }

        /// <summary>
        /// Returns a function parameter or local variable from a given function, or null if either parameter is out of bounds.
        /// </summary>
        /// <param name="function">Function to get it from</param>
        /// <param name="index">index in the Parameters array</param>
        /// <returns>A variable from the function, or null</returns>
        public Variable GetFunctionParameter(Function function, int index)
        {
            if (function != null && index >= 0 && index < function.Parameters.Count)
            {
                return function.Parameters[index];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns a function parameter or local variable from a given function, or null if the name is not found.  Stops at the first duplicate.
        /// </summary>
        /// <param name="function">Function to get it from</param>
        /// <param name="parameterName">Name of the parameter or variable</param>
        /// <returns>A variable from the function, or null</returns>
        public Variable GetFunctionParameter(Function function, string parameterName)
        {
            if (function == null)
            {
                return null;
            }
            foreach (var parameter in function.Parameters)
            {
                if (parameter.Name == parameterName)
                {
                    return parameter;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the first function parameter or local variable from a given function which matches the name, or null if the name is not found.
        /// </summary>
        /// <param name="function">Function to get it from</param>
        /// <param name="index">index in the Parameters array</param>
        /// <returns>The first matching variable from the function, or null</returns>
        public static Variable GetFunctionParameterByName(Function function, string name)
        {
            if (function == null)
            {
                return null;
            }
            for (int i = 0; i < function.Parameters.Count; i++)
            {
                var parameter = function.Parameters[i];
                if (parameter.Name == name)
                {
                    return parameter;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a function parameter or local variable from a given function, or null if either parameter is out of bounds.
        /// </summary>
        /// <param name="functionName">Name of the function to look up</param>
        /// <param name="index">index in the Parameters array</param>
        /// <returns>A variable from the function, or null</returns>
        public Variable GetFunctionParameter(string functionName, int index)
        {
            var function = GetFunction(functionName);
            return GetFunctionParameter(function, index);
        }

        /// <summary>
        /// Returns a function parameter or local variable from a given function, or null if either parameter is not found.
        /// </summary>
        /// <param name="functionName">Name of the function to look up</param>
        /// <param name="parameterName">Name of the function parameter</param>
        /// <returns>A variable from the function, or null</returns>
        public Variable GetFunctionParameter(string functionName, string parameterName)
        {
            var function = GetFunction(functionName);
            return GetFunctionParameter(function, parameterName);
        }

        /// <summary>
        /// Returns a system call from the SystemCalls array, or null if the system call does not exist.
        /// </summary>
        /// <param name="sysCallNumber">index of the system call number</param>
        /// <returns>A system call, or null</returns>
        public static Function GetSystemCall(int sysCallNumber)
        {
            if (sysCallNumber >= 0 && sysCallNumber < AinFile.SystemCalls.Count)
            {
                return AinFile.SystemCalls[sysCallNumber];
            }
            return null;
        }

        /// <summary>
        /// Returns a system call from the SystemCalls array, or null if the system call does not exist.
        /// </summary>
        /// <param name="sysCallNumber">index of the system call number</param>
        /// <returns>A system call, or null</returns>
        public static Variable GetSystemCallParameter(int sysCallNumber, int parameterIndex)
        {
            var systemCall = GetSystemCall(sysCallNumber);
            if (systemCall != null)
            {
                if (parameterIndex >= 0 && parameterIndex < systemCall.ParameterCount)
                {
                    return systemCall.Parameters[parameterIndex];
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a system call from the SystemCalls array by name, or null if the system call does not exist.
        /// </summary>
        /// <param name="sysCallName">The name of the system call</param>
        /// <returns>A system call, or null</returns>
        public static Function GetSystemCall(string sysCallName)
        {
            if (SystemCallNameToIndex.ContainsKey(sysCallName))
            {
                var systemCall = SystemCalls[SystemCallNameToIndex[sysCallName]];
                return systemCall;
            }
            sysCallName = "system." + sysCallName;
            if (SystemCallNameToIndex.ContainsKey(sysCallName))
            {
                var systemCall = SystemCalls[SystemCallNameToIndex[sysCallName]];
                return systemCall;
            }
            return null;
        }

        /// <summary>
        /// Returns a library function from the LibraryFunctions array by indexes, or null if the library function does not exist.
        /// </summary>
        /// <param name="libraryNumber">Index of the library</param>
        /// <param name="libraryFunctionNumber">Index of the library function</param>
        /// <returns>A library call, or null</returns>
        public HllFunction GetLibraryFunction(int libraryNumber, int libraryFunctionNumber)
        {
            if (libraryNumber >= 0 && libraryNumber < Libraries.Count)
            {
                var library = Libraries[libraryNumber];
                if (libraryFunctionNumber >= 0 && libraryFunctionNumber < library.Functions.Count)
                {
                    return library.Functions[libraryFunctionNumber];
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a library function from the LibraryFunctions array by linear search, or null if the library function does not exist.
        /// </summary>
        /// <param name="libraryName">Name of the library</param>
        /// <param name="libraryFunctionName">Name of the library function</param>
        /// <returns>A library call, or null</returns>
        public HllFunction GetLibraryFunction(string libraryName, string libraryFunctionName)
        {
            if (String.IsNullOrEmpty(libraryName) || String.IsNullOrEmpty(libraryFunctionName))
            {
                return null;
            }
            foreach (var library in this.Libraries)
            {
                if (library.LibraryName == libraryName)
                {
                    foreach (var libraryFunction in library.Functions)
                    {
                        if (libraryFunction.Name == libraryFunctionName)
                        {
                            return libraryFunction;
                        }
                    }
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a parameter of a library function, or null if the parameter does not exist.
        /// </summary>
        /// <param name="libraryNumber">Index of the library</param>
        /// <param name="libraryFunctionNumber">Index of the library function</param>
        /// <param name="parameterIndex">Index of the parameter</param>
        /// <returns>The parameter of the library function, or null</returns>
        public HllFunctionParameter GetLibraryFunctionParameter(int libraryNumber, int libraryFunctionNumber, int parameterIndex)
        {
            var libraryFunction = GetLibraryFunction(libraryNumber, libraryFunctionNumber);
            if (libraryFunction != null)
            {
                if (parameterIndex >= 0 && parameterIndex < libraryFunction.ParameterCount)
                {
                    return libraryFunction.Parameters[parameterIndex];
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a parameter of a library function, or null if the parameter does not exist.
        /// </summary>
        /// <param name="libraryName">Name of the library</param>
        /// <param name="libraryFunctionName">Name of the library function</param>
        /// <param name="parameterIndex">Index of the parameter</param>
        /// <returns>The parameter of the library function, or null</returns>
        public HllFunctionParameter GetLibraryFunctionParameter(string libraryName, string libraryFunctionName, int parameterIndex)
        {
            var libraryFunction = GetLibraryFunction(libraryName, libraryFunctionName);
            if (libraryFunction != null)
            {
                if (parameterIndex >= 0 && parameterIndex < libraryFunction.ParameterCount)
                {
                    return libraryFunction.Parameters[parameterIndex];
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a function type from the FuncitonTypes array, or null if the function type does not exist.
        /// </summary>
        /// <param name="funcTypeIndex">Index of the function type</param>
        /// <returns>The function type, or null</returns>
        public FunctionType GetFuncType(int funcTypeIndex)
        {
            if (funcTypeIndex >= 0 && funcTypeIndex < this.FunctionTypes.Count)
            {
                return FunctionTypes[funcTypeIndex];
            }
            return null;
        }

        /// <summary>
        /// Returns a function type from the FuncitonTypes array by linear search, or null if the function type does not exist.
        /// </summary>
        /// <param name="funcTypeName">Name of the function type</param>
        /// <returns>The function type, or null</returns>
        public FunctionType GetFuncType(string funcTypeName)
        {
            if (String.IsNullOrEmpty(funcTypeName))
            {
                return null;
            }
            foreach (var funcType in this.FunctionTypes)
            {
                if (funcType.Name == funcTypeName)
                {
                    return funcType;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a function type from the FuncitonTypes array, or null if the function type does not exist.
        /// </summary>
        /// <param name="funcTypeIndex">Index of the function type</param>
        /// <returns>The function type, or null</returns>
        public FunctionType GetDelegateType(int delegateTypeIndex)
        {
            if (delegateTypeIndex >= 0 && delegateTypeIndex < this.Delegates.Count)
            {
                return Delegates[delegateTypeIndex];
            }
            return null;
        }

        /// <summary>
        /// Returns a parameter from a function type, or null if the parameter does not exist.
        /// </summary>
        /// <param name="funcTypeIndex">Index of the funciton type</param>
        /// <param name="parameterIndex">Index of the parameter</param>
        /// <returns>The function type parameter, or null</returns>
        public Variable GetFuncTypeParameter(int funcTypeIndex, int parameterIndex)
        {
            var funcType = GetFuncType(funcTypeIndex);
            if (funcType != null)
            {
                if (parameterIndex >= 0 && parameterIndex < funcType.ParameterCount)
                {
                    return funcType.Parameters[parameterIndex];
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a parameter from a function type by linear search, or null if the parameter does not exist.
        /// </summary>
        /// <param name="funcTypeName">Name of the funciton type</param>
        /// <param name="parameterIndex">Index of the parameter</param>
        /// <returns>The function type parameter, or null</returns>
        public Variable GetFuncTypeParameter(string funcTypeName, int parameterIndex)
        {
            var funcType = GetFuncType(funcTypeName);
            if (funcType != null)
            {
                if (parameterIndex >= 0 && parameterIndex < funcType.ParameterCount)
                {
                    return funcType.Parameters[parameterIndex];
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a parameter from a function type, or null if the parameter does not exist.
        /// </summary>
        /// <param name="funcTypeIndex">Index of the funciton type</param>
        /// <param name="parameterIndex">Index of the parameter</param>
        /// <returns>The function type parameter, or null</returns>
        public Variable GetDelegateParameter(int delegateIndex, int parameterIndex)
        {
            var delegateType = GetDelegateType(delegateIndex);
            if (delegateType != null)
            {
                if (parameterIndex >= 0 && parameterIndex < delegateType.ParameterCount)
                {
                    return delegateType.Parameters[parameterIndex];
                }
            }
            return null;
        }

        #endregion

        /// <summary>
        /// Returns information of the instruction at the specified address
        /// </summary>
        /// <param name="address">The address to peek at</param>
        /// <returns>Information of the instruction</returns>
        public InstructionInfo Peek(int address)
        {
            return Decompiler.Peek(this.Code, address);
        }

        /// <summary>
        /// Returns a message from the Messages array, or null if the message does not exist
        /// </summary>
        /// <param name="messageNumber">Index of the array</param>
        /// <returns>The message, or null</returns>
        public string GetMessage(int messageNumber)
        {
            if (messageNumber >= 0 && messageNumber < this.Messages.Count)
            {
                return this.Messages[messageNumber];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Loads the metadata (variable alternative names, enumeration types, default arguments, etc) from a file named like "ainfile_metadata.xml"
        /// </summary>
        /// <returns>True if the file was loaded, otherwise false</returns>
        public bool LoadMetadata()
        {
            string xmlFileName = GetXmlFilename();
            if (xmlFileName != null)
            {
                if (File.Exists(xmlFileName))
                {
                    try
                    {
                        this.MetadataFile.LoadFile(xmlFileName);
                        return true;
                    }
                    catch (IOException)
                    {

                    }
                    catch (UnauthorizedAccessException)
                    {

                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Saves the metadata (variable alternative names, enumeration types, default arguments, etc) to a file named like "ainfile_metadata.xml".
        /// </summary>
        /// <returns>True if the file was saved, otherwise false</returns>
        public bool SaveMetadata()
        {
            string xmlFileName = GetXmlFilename();
            if (xmlFileName != null)
            {
                try
                {
                    this.MetadataFile.SaveFile(xmlFileName);
                    return true;
                }
                catch (IOException)
                {

                }
                catch (UnauthorizedAccessException)
                {

                }
            }
            return false;
        }

        /// <summary>
        /// Returns a filename like "ainfile_metadata.xml".
        /// </summary>
        /// <returns>The filename</returns>
        private string GetXmlFilename()
        {
            string fileName = this.OriginalFilename;
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }
            string directoryName = Path.GetDirectoryName(fileName);
            string baseFileName = Path.GetFileNameWithoutExtension(fileName);
            string xmlFileName = Path.Combine(directoryName, baseFileName + "_metadata.xml");

            return xmlFileName;
        }

        /// <summary>
        /// Loads the alternative names from a file named like "ainfile_names.txt"
        /// </summary>
        /// <returns>True if loading succeeded, otherwise false.</returns>
        public bool LoadAlternativeNames()
        {
            string fileName = GetAlternativeNamesFilename();

            AlternativeNames.Clear();
            return AlternativeNames.LoadFile(fileName);
        }

        /// <summary>
        /// Returns a filename like "ainfile_names.txt"
        /// </summary>
        /// <returns>The filename</returns>
        private string GetAlternativeNamesFilename()
        {
            string fileName = this.OriginalFilename;
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }
            string directoryName = Path.GetDirectoryName(fileName);
            string baseFileName = Path.GetFileNameWithoutExtension(fileName);
            string alternativeNamesFileName = Path.Combine(directoryName, baseFileName + "_names.txt");

            return alternativeNamesFileName;
        }

        #region Function type / Delegate matching
        internal bool functionTypesKnown { get; private set; }

        public void FindFunctionTypes()
        {
            if (FunctionTypes.Count == 0 && Delegates.Count == 0)
            {
                this.functionTypesKnown = true;
                return;
            }

            if (ExpressionDisplayer.DefaultFuncType == -1 && FunctionTypes.Count > 0)
            {
                foreach (var funcType in FunctionTypes)
                {
                    if (funcType.ParameterCount == 0 && funcType.DataType == DataType.Void)
                    {
                        ExpressionDisplayer.DefaultFuncType = funcType.Index;
                        break;
                    }
                }
            }
            if (ExpressionDisplayer.DefaultDelegateType == -1 && Delegates.Count > 0)
            {
                //int invalidDelegateIndex = Delegates.Count;
                //Delegates.Add(new FunctionType() { DataType = DataType.Void, Index = invalidDelegateIndex, Name = "INVALID_DELEGATE", ParameterCount = 0, Parameters = new VariableCollection(), Root = this, StructType = -1 });
                //ExpressionDisplayer.DefaultDelegateType = invalidDelegateIndex;

                foreach (var funcType in Delegates)
                {
                    if (funcType.ParameterCount == 0 && funcType.DataType == DataType.Void)
                    {
                        ExpressionDisplayer.DefaultDelegateType = funcType.Index;
                        break;
                    }
                }
            }
            if (this.functionTypesKnown == true)
            {
                return;
            }
            foreach (var pair in this.MetadataFile.Metadata)
            {
                if (pair.Value.FuncTypeIndex != -1)
                {
                    this.functionTypesKnown = true;
                    return;
                }
            }
            var tracer = new VariableTracer(this);
            tracer.FindFunctionTypes();
            this.functionTypesKnown = true;
        }

        static bool FunctionTypeUnknown(IVariable variable)
        {
            if (variable.DataType.IsFuncTypeOrDelegate())
            {
                var metaData = variable.GetMetadata();
                if (metaData != null)
                {
                    if (metaData.FuncTypeIndex != -1)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public IEnumerable<IVariable> GetVariablesWithUnknownFunctypes()
        {
            if (FunctionTypes.Count == 0 && Delegates.Count == 0)
            {
                yield break;
            }

            foreach (var function in Functions)
            {
                if (FunctionTypeUnknown(function)) yield return function;
                foreach (var parameter in function.Parameters)
                {
                    if (FunctionTypeUnknown(parameter)) yield return parameter;
                }
            }
            foreach (var structInfo in Structs)
            {
                foreach (var member in structInfo.Members)
                {
                    if (FunctionTypeUnknown(member)) yield return member;
                }
            }
            foreach (var global in Globals)
            {
                if (FunctionTypeUnknown(global)) yield return global;
            }
            foreach (var funcType in FunctionTypes)
            {
                if (FunctionTypeUnknown(funcType)) yield return funcType;
                foreach (var parameter in funcType.Parameters)
                {
                    if (FunctionTypeUnknown(parameter)) yield return parameter;
                }
            }
            foreach (var delg in Delegates)
            {
                if (FunctionTypeUnknown(delg)) yield return delg;
                foreach (var parameter in delg.Parameters)
                {
                    if (FunctionTypeUnknown(parameter)) yield return parameter;
                }
            }
        }

        /// <summary>
        /// Returns the first FuncType that matches the signature of the provided function.
        /// </summary>
        /// <param name="otherFunction">The function to match against</param>
        public FunctionType GetFuncType(Function otherFunction)
        {
            return MatchingFunctionTypes(otherFunction).FirstOrDefault();
        }

        /// <summary>
        /// Returns the first FuncType that matches the signature of the provided function.
        /// </summary>
        /// <param name="otherFunction">The function to match against</param>
        public FunctionType GetDelegateType(Function otherFunction)
        {
            return MatchingDelegates(otherFunction).FirstOrDefault();
        }

        /// <summary>
        /// Returns the sequence of function types that match the parameter types.
        /// </summary>
        /// <param name="outerDataType">The data type the function would return, or DataType.AnyDataType, or DataType.AnyNonVoidType.</param>
        /// <param name="outerStructType">The struct type, or -1 for a variable without a struct type.</param>
        /// <param name="parameters">The list of parameters to match</param>
        /// <param name="parameterCount">The number of parameters to look at</param>
        /// <returns>A sequence of function types which match the signature provided</returns>
        internal IEnumerable<FunctionType> MatchingFunctionTypes(DataType outerDataType, int outerStructType, IList<IVariable> parameters, int parameterCount)
        {
            return MatchingFunctionTypes(this.FunctionTypes, outerDataType, outerStructType, parameters, parameterCount);
        }

        private static IEnumerable<FunctionType> MatchingFunctionTypes(IEnumerable<FunctionType> functionTypes, DataType outerDataType, int outerStructType, IList<IVariable> parameters, int parameterCount)
        {
            foreach (var funcType in functionTypes)
            {
                if (outerDataType == DataType.AnyDataType || (outerDataType == DataType.AnyNonVoidType && funcType.DataType != DataType.Void) || (funcType.DataType == outerDataType && funcType.StructType == outerStructType))
                {
                    if (parameterCount == 0 && funcType.ParameterCount == 0)
                    {
                        yield return funcType;
                    }
                    if (parameterCount != funcType.ParameterCount)
                    {
                        continue;
                    }

                    bool matched = true;
                    for (int i = 0; i < parameterCount; i++)
                    {
                        var functionParameter = parameters[i];
                        var funcTypeParameter = funcType.Parameters[i];

                        if (functionParameter.DataType == funcTypeParameter.DataType &&
                            functionParameter.StructType == funcTypeParameter.StructType &&
                            functionParameter.ArrayDimensions == funcTypeParameter.ArrayDimensions)
                        {
                            //keep going
                        }
                        else
                        {
                            matched = false;
                            break;
                        }
                    }
                    if (matched)
                    {
                        yield return funcType;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the sequence of delegate types that match the parameter types.
        /// </summary>
        /// <param name="outerDataType">The data type the function would return, or DataType.AnyDataType, or DataType.AnyNonVoidType.</param>
        /// <param name="outerStructType">The struct type, or -1 for a variable without a struct type.</param>
        /// <param name="parameters">The list of parameters to match</param>
        /// <param name="parameterCount">The number of parameters to look at</param>
        /// <returns>A sequence of delegate types which match the signature provided</returns>
        internal IEnumerable<FunctionType> MatchingDelegates(DataType outerDataType, int outerStructType, IList<IVariable> parameters, int parameterCount)
        {
            return MatchingFunctionTypes(this.Delegates, outerDataType, outerStructType, parameters, parameterCount);
        }

        internal IEnumerable<FunctionType> MatchingFunctionTypes(Function otherFunction)
        {
            if (otherFunction == null) yield break;
            if (otherFunction.Name == "NULL" && otherFunction.Index == 0) yield break;
            var outerDataType = otherFunction.DataType;
            int outerStructType = otherFunction.StructType;
            int parameterCount = otherFunction.ParameterCount;
            var parameters = ((IFunction)otherFunction).Parameters;
            foreach (var match in MatchingFunctionTypes(outerDataType, outerStructType, parameters, parameterCount))
            {
                yield return match;
            }
        }

        internal IEnumerable<FunctionType> MatchingDelegates(Function otherFunction)
        {
            if (otherFunction == null) yield break;
            if (otherFunction.Name == "NULL" && otherFunction.Index == 0) yield break;
            var outerDataType = otherFunction.DataType;
            int outerStructType = otherFunction.StructType;
            int parameterCount = otherFunction.ParameterCount;
            var parameters = ((IFunction)otherFunction).Parameters;
            foreach (var match in MatchingDelegates(outerDataType, outerStructType, parameters, parameterCount))
            {
                yield return match;
            }
        }

        /// <summary>
        /// Returns the FuncType that matches the signature of the provided function if there is only one match.
        /// </summary>
        /// <param name="otherFunction">The function to match against</param>
        public FunctionType GetFuncTypeUnique(Function otherFunction)
        {
            var matchingFunctionTypes = MatchingFunctionTypes(otherFunction);
            FunctionType first = null;
            foreach (var funcType in matchingFunctionTypes)
            {
                if (first == null)
                {
                    first = funcType;
                }
                else
                {
                    return null;
                }
            }
            return first;
        }

        /// <summary>
        /// Returns the FuncType that matches the signature of the provided function if there is only one match.
        /// </summary>
        /// <param name="otherFunction">The function to match against</param>
        public FunctionType GetDelegateUnique(Function otherFunction)
        {
            var matchingDelegates = MatchingDelegates(otherFunction);
            FunctionType first = null;
            foreach (var funcType in matchingDelegates)
            {
                if (first == null)
                {
                    first = funcType;
                }
                else
                {
                    return null;
                }
            }
            return first;
        }
        #endregion

        public int ComputeFunctionHash(Function function)
        {
            //note: currently does not include Labels, which are supposed to be hashed in the CRC like this: 0x406, "FunctionName::labelName", but with each character being a separate 32-bit word
            if (function == null)
            {
                throw new ArgumentNullException("function");
            }
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            //function return type
            bw.Write((int)function.DataType);
            for (int i = 0; i < function.ParameterCount; i++)
            {
                var parameter = function.Parameters[i];
                if (parameter.DataType != DataType.Void)
                {
                    bw.Write((int)parameter.DataType);
                }
            }
            int address = function.Address;
            while (true)
            {
                var ins = Peek(address);
                if (ins.instruction == Instruction.ENDFUNC || ins.instruction == Instruction.FUNC || ins.instruction == Instruction.EOF)
                {
                    break;
                }
                bw.Write((int)ins.instruction);
                address = ins.nextAddress;
            }
            return (int)Crc32.CalculateCrc32(ms.ToArray());
        }

        public int ComputeFunctionHash(int functionNumber)
        {
            var function = GetFunction(functionNumber);
            return ComputeFunctionHash(function);
        }

        /// <summary>
        /// Computes function hashes if there is no existing hash already
        /// </summary>
        public void ComputeFunctionHashes()
        {
            foreach (var function in this.Functions)
            {
                if (function.Crc == 0)
                {
                    function.Crc = ComputeFunctionHash(function);
                }
            }
        }
    }

    /// <summary>
    /// Utility class to output all the members of a class as a string.
    /// </summary>
    public static class ReflectionUtil
    {
        /// <summary>
        /// Gets a string containing the values of the fields of an object.
        /// </summary>
        /// <param name="obj">The object to get the fields of</param>
        /// <returns>A string containing the values of the object's fields</returns>
        public static string ToString(object obj)
        {
            StringBuilder sb = new StringBuilder();

            //get the type
            var type = obj.GetType();

            //get the public fields that aren't static
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public);

            //display them all
            bool needComma = false;
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(int) || field.FieldType == typeof(string) || field.FieldType.IsEnum)
                {
                    Util.PrintComma(sb, ref needComma);
                    sb.Append(field.Name + " = " + (field.GetValue(obj) ?? "null").ToString());
                }
            }
            return sb.ToString();

        }

        /// <summary>
        /// Assigns all the public writable fields and properties from one object to another
        /// </summary>
        /// <typeparam name="TObj">Type of the object</typeparam>
        /// <param name="assignTo">Object to assign to</param>
        /// <param name="readFrom">Object to read from</param>
        public static void MemberwiseAssign<TObj>(TObj assignTo, TObj readFrom)
        {
            var fields = typeof(TObj).GetFields(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public);
            var properties = typeof(TObj).GetProperties(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public);

            foreach (var field in fields)
            {
                if (!field.IsInitOnly)
                {
                    object value = field.GetValue(readFrom);
                    field.SetValue(assignTo, value);
                }
            }
            foreach (var property in properties)
            {
                if (property.CanRead && property.CanWrite)
                {
                    object value = property.GetValue(readFrom, null);
                    property.SetValue(assignTo, value, null);
                }
            }
        }
    }
}
