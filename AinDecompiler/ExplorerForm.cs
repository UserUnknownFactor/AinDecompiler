using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.CodeDom.Compiler;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Document = ScintillaNET.Document;
using Clipboard = System.Windows.Forms.Clipboard;

namespace AinDecompiler
{
    public partial class ExplorerForm : Form
    {
        //Dictionary<Document, ExplorerHistoryItem> openDocuments = new Dictionary<Document, ExplorerHistoryItem>();

        Document defaultDocument1, defaultDocument2, defaultDocument3;
        public AinFile ainFile;
        public AinFile originalAinFile;

        public Decompiler decompiler;
        public ExpressionMap expressionMap = null;
        PopupForm popupForm = null;
        Expression popupExpression = null;
        CodeDisplayState codeDisplayState = null;
        int currentTabIndex = 0;
        ScintillaState nextScintillaState;
        ScintillaState nextScintillaState2;
        ScintillaState nextScintillaState3;
        bool suspendTabEvents = false;
        bool suspendTreeViewEvents = false;

        //public AlternativeNames AlternativeNames = new AlternativeNames();

        MyBackgroundWorker bw;

        Expression contextMenuExpression = null;

        List<List<ExplorerHistoryItem>> History = new List<List<ExplorerHistoryItem>>();
        List<int> HistoryIndex = new List<int>();


        public ExplorerForm()
        {
            InitializeComponent();
            CreateScintillaDefaultDocument();
        }

        public ExplorerForm(string fileName)
            : this()
        {
            WhenIdle(() => OpenFile(fileName));
        }

        private static void WhenIdle(Action action)
        {
            EventHandler appIdle = null;
            appIdle = (sender, e) =>
            {
                Application.Idle -= appIdle;
                action();
            };
            Application.Idle += appIdle;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Disposed += new EventHandler(ExplorerForm_Disposed);
            //Application.AddMessageFilter(this);

            DecompiledCodeTextBox.ConfigurationManager.Language = "cs";
            DecompiledCodeTextBox.Font = new System.Drawing.Font("Consolas", 10.75F);
            DecompiledCodeTextBox.UpdateFonts();

            DisassembledCodeTextBox.ConfigurationManager.Language = "cs";
            DisassembledCodeTextBox.ConfigurationManager.Configure();

            CombinedViewTextBox.ConfigurationManager.Language = "cs";
            CombinedViewTextBox.ConfigurationManager.Configure();

            NewTab();
            EnableDisableNavigationButtons();

            //int funcNumber = 0x325;
            //int funcNumber = 0x4;

            //int funcNumber = 0x42f;  //character maker for sengoku rance
            //int funcNumber = 0x30d;  //sat bonus for sengoku rance
            //int funcNumber = 0x18fd;  //stop the killer dango
            //int funcNumber = 0x18fe; //teru cleans tokugawa racoon dogs room
            //int funcNumber = 0x100;
            //int funcAddress = ainFile.FUNC[funcNumber].address - 6;
            //int funcEnd = ainFile.FUNC[funcNumber + 1].address;

            //disassembler.Disassemble(ainFile, stringWriter);
            //var expression = decompiler.DecompileRange(funcAddress, funcEnd);
            //expression = new Expression(Instruction.func, expression, funcNumber);

            //string code = displayer.PrintExpression2(expression);

            LoadCodeDisplayOptions();

            if (Debugger.IsAttached)
            {
                this.debugToolStripMenuItem.Visible = true;
            }

            this.useModifiedShiftJISEncodingForEuropeanTextToolStripMenuItem.Checked = RegistryUtility.GetSetting("UseModifiedShiftJis", false);
            this.useModifiedShiftJISEncodingForAccentedTextNEWToolStripMenuItem.Checked = RegistryUtility.GetSetting("UseModifiedShiftJis2", false);
            int compressionLevel = RegistryUtility.GetSetting("ZlibCompressionLevel", 9);
            if (compressionLevel < 0)
            {
                compressionLevel = 0;
            }
            if (compressionLevel > 9)
            {
                compressionLevel = 9;
            }
            AinFile.DefaultCompressionLevel = (ZLibNet.CompressionLevel)compressionLevel;

            //force shortcut keys to properly be disabled
            this.fileToolStripMenuItem_DropDownOpening(this.fileToolStripMenuItem, EventArgs.Empty);
        }

        private void CreateScintillaDefaultDocument()
        {
            this.defaultDocument1 = this.DecompiledCodeTextBox.DocumentHandler.Create();
            this.DecompiledCodeTextBox.DocumentHandler.Current = defaultDocument1;
            this.DecompiledCodeTextBox.Encoding = UTF8Encoding.UTF8;
            this.DecompiledCodeTextBox.ConfigurationManager.Language = "cs";
            this.DecompiledCodeTextBox.Font = new System.Drawing.Font("Consolas", 10.75F);
            this.DecompiledCodeTextBox.UpdateFonts();
            this.DecompiledCodeTextBox.IsBraceMatching = true;
            this.DecompiledCodeTextBox.Styles.BraceBad.Size = 9F;
            this.DecompiledCodeTextBox.Styles.BraceLight.Size = 9F;
            this.DecompiledCodeTextBox.Styles.ControlChar.Size = 9F;
            this.DecompiledCodeTextBox.Styles.Default.BackColor = System.Drawing.SystemColors.Window;
            this.DecompiledCodeTextBox.Styles.Default.Size = 9F;
            this.DecompiledCodeTextBox.Styles.IndentGuide.Size = 9F;
            this.DecompiledCodeTextBox.Styles.LastPredefined.Size = 9F;
            this.DecompiledCodeTextBox.Styles.LineNumber.Size = 9F;
            this.DecompiledCodeTextBox.Styles.Max.Size = 9F;
            this.DecompiledCodeTextBox.Modified = false;

            this.defaultDocument2 = this.DisassembledCodeTextBox.DocumentHandler.Current;
            this.defaultDocument3 = this.CombinedViewTextBox.DocumentHandler.Current;
        }

        #region Popup Form and expression context menu code

        void ExplorerForm_Disposed(object sender, EventArgs e)
        {
            if (this.popupForm != null && !this.popupForm.IsDisposed)
            {
                this.popupForm.Close();
            }
        }

        private void DecompiledCodeTextBox_MouseMove(object sender, MouseEventArgs e)
        {
            var control = sender as ScintillaNET.Scintilla;
            if (control != null)
            {
                int position = control.PositionFromPoint(e.X, e.Y);
                if (expressionMap != null)
                {
                    var items = this.expressionMap.GetItemsAt(position);
                    var firstItem = items.FirstOrDefault();
                    int x = control.PointXFromPosition(position);
                    int y = control.PointYFromPosition(position);
                    var line = control.Lines.FromPosition(position);
                    int y2 = control.PointYFromPosition(line.Next.StartPosition);

                    var screenPoint = control.PointToScreen(new Point(x, y2));
                    if (popupExpression != firstItem)
                    {
                        popupExpression = firstItem;
                        ShowPopup(screenPoint, firstItem);
                    }
                }
            }
        }

        static HashSet<Instruction> functionTypesAndComma = new HashSet<Instruction>(new Instruction[]
        {
            Instruction.Comma, Instruction.CALLFUNC, Instruction.CALLMETHOD, Instruction.CALLHLL, Instruction.CALLSYS,
        });

        static HashSet<Instruction> functionTypes = new HashSet<Instruction>(new Instruction[]
        {
            Instruction.CALLFUNC, Instruction.CALLMETHOD, Instruction.CALLHLL, Instruction.CALLSYS,
        });

        private void ShowPopup(Point screenPoint, Expression expression)
        {
            StringBuilder text = new StringBuilder();
            if (expression != null)
            {
                var displayer = this.expressionMap.displayer;

                var variable = displayer.GetReferencedVariableType(expression);
                string declaration = "";
                string alternativeDeclaration = "";

                if (variable is IFunction && !(variable is Struct))
                {
                    declaration = ExpressionDisplayer.GetFunctionDeclaration((IFunction)variable, false);
                    alternativeDeclaration = ainFile.AlternativeNames.GetAlternativeFunctionDeclaration((IFunction)variable, false);
                }
                else
                {
                    declaration = displayer.GetVariableDeclaration(variable);
                    alternativeDeclaration = ainFile.AlternativeNames.GetAlternativeVariableDeclaration(variable);
                }

                text.Append(declaration);
                if (declaration != alternativeDeclaration)
                {
                    text.AppendLine();
                    text.Append("(" + alternativeDeclaration + ")");
                }
                //is this a function argument?
                {
                    var funcExpression = expression.GetAncestorOfType(functionTypes);
                    //int depth = -1;
                    if (funcExpression != null)
                    {
                        int depth;
                        var parameter = expression.GetFunctionCallParameter(out depth);
                        var funcType = displayer.GetReferencedVariableType(funcExpression);

                        var func = funcType as IFunction;
                        if (func != null)
                        {
                            text.AppendLine();
                            if (parameter != null)
                            {
                                string variableDeclaration = displayer.GetVariableDeclaration(parameter);
                                string alternativeVariableDeclaration = ainFile.AlternativeNames.GetAlternativeVariableDeclaration(parameter);
                                text.Append("Argument #" + depth.ToString() + ": " + variableDeclaration);
                                if (variableDeclaration != alternativeVariableDeclaration)
                                {
                                    text.Append(" (" + alternativeVariableDeclaration + ")");
                                }
                            }
                            else if (depth == -1)
                            {
                                text.Append("Object the method is being called on");
                            }
                            text.AppendLine();
                            string functionDeclaration = ExpressionDisplayer.GetFunctionDeclaration(func, false);
                            text.Append("For function: " + functionDeclaration);
                            string alternativeFunctionDeclaration = ainFile.AlternativeNames.GetAlternativeFunctionDeclaration(func, false);
                            if (functionDeclaration != alternativeFunctionDeclaration)
                            {
                                text.AppendLine();
                                text.Append("(" + alternativeFunctionDeclaration + ")");
                            }
                        }
                    }
                }
                //append address
                if (expression.address >= 0)
                {
                    text.AppendLine();
                    text.Append("Address = 0x" + expression.address.ToString("X"));
                }
            }
            //show the window
            if (popupForm == null)
            {
                popupForm = new PopupForm();
                //popupForm.Owner = this;
            }
            popupForm.Hide();
            popupForm.Location = screenPoint;
            popupForm.PopupLabel.Text = text.ToString();
            popupForm.Size = popupForm.PopupLabel.Size;
            if (text.Length == 0)
            {
                popupForm.Hide();
            }
            else
            {
                NativeMethods.ShowInactiveTopmost(popupForm);
            }
        }

        private void contextMenuStrip2_Opening(object sender, CancelEventArgs e)
        {
            var menuStrip = sender as ContextMenuStrip;
            if (menuStrip != null && ainFile != null)
            {
                HandleCodeContextMenu(e, menuStrip);
            }
        }

        private void HandleCodeContextMenu(CancelEventArgs e, ContextMenuStrip menuStrip)
        {
            var expression = popupExpression;
            menuStrip.Items.Clear();
            contextMenuExpression = expression;

            IVariable functionParameter = null;
            if (popupExpression != null)
            {
                functionParameter = popupExpression.GetFunctionCallParameter();
            }

            IVariable variable = null;
            if (expression != null)
            {
                variable = expression.Variable;
                if (variable != null)
                {
                    variable = variable.Canonicalize();
                }
            }
            if (variable != null)
            {
                var editMetadata = new ToolStripMenuItem("&Edit Metadata...");
                editMetadata.Tag = variable;
                editMetadata.Click += new EventHandler(editMetadata_Click);
                menuStrip.Items.AddWithSeparator(editMetadata);
            }

            if (popupExpression != null)
            {
                switch (popupExpression.ExpressionType)
                {
                    case Instruction.CALLFUNC:
                    case Instruction.CALLMETHOD:
                    case Instruction.FUNC:
                        {
                            var function = ainFile.GetFunction(expression.Value);
                            if (function == null) break;
                            AddFunctionItemsToMenu(menuStrip, function);
                        }
                        break;
                    case Instruction.PUSH:
                        {
                            int value = expression.Value;
                            var findAllReferences = new ToolStripMenuItem("&Find all references of int " + value.ToString() + "...");
                            findAllReferences.Tag = value;
                            findAllReferences.Click += new EventHandler(findAllReferences_Click);
                            menuStrip.Items.AddWithSeparator(findAllReferences);
                        }
                        break;
                    case Instruction.S_PUSH:
                        {
                            string str = ainFile.GetString(expression.Value);
                            if (str == null) break;
                            var findAllReferences = new ToolStripMenuItem("&Find all references of string " + str + "...");
                            findAllReferences.Tag = str;
                            findAllReferences.Click += new EventHandler(findAllReferences_Click);
                            menuStrip.Items.AddWithSeparator(findAllReferences);

                            var function = ainFile.GetFunction(str);
                            var structInfo = ainFile.GetStruct(str);

                            if (function != null)
                            {
                                AddFunctionItemsToMenu(menuStrip, function);
                            }

                            if (structInfo != null)
                            {
                                AddStructItemsToMenu(menuStrip, structInfo);
                            }
                        }
                        break;
                    case Instruction.CALLHLL:
                        {
                            var libraryFunction = ainFile.GetLibraryFunction(expression.Value, expression.Value2);
                            if (libraryFunction == null) break;
                            string libraryName = libraryFunction.FullName;
                            var findAllReferences = new ToolStripMenuItem("&Find all references of library call " + libraryName + "...");
                            findAllReferences.Tag = libraryFunction;
                            findAllReferences.Click += new EventHandler(findAllReferences_Click);
                            menuStrip.Items.AddWithSeparator(findAllReferences);
                        }
                        break;
                    case Instruction.CALLSYS:
                        {
                            var systemCall = AinFile.GetSystemCall(expression.Value);
                            if (systemCall == null) break;
                            string systemCallName = systemCall.Name;
                            var findAllReferences = new ToolStripMenuItem("&Find all references of system call " + systemCallName + "...");
                            findAllReferences.Tag = systemCall;
                            findAllReferences.Click += new EventHandler(findAllReferences_Click);
                            menuStrip.Items.AddWithSeparator(findAllReferences);
                        }
                        break;
                    default:
                        {
                            //var variable = expression.Variable;
                            if (variable != null)
                            {
                                var parent = variable.Parent;
                                var functionParent = parent as Function;
                                var structParent = parent as Struct;
                                var global = variable as Global;

                                if (functionParent != null)
                                {
                                    variable = GetVariableByName(functionParent, variable.Name);
                                    if (variable != null)
                                    {
                                        var goToDefinition = new ToolStripMenuItem("&Go to declaration of local variable " + variable.GetActiveName());
                                        goToDefinition.Tag = variable;
                                        goToDefinition.Click += new EventHandler(goToDefinition_Click);
                                        menuStrip.Items.AddWithSeparator(goToDefinition);

                                        var findAllReferences = new ToolStripMenuItem("&Find all references of local variable " + variable.GetActiveName());
                                        findAllReferences.Tag = variable;
                                        findAllReferences.Click += new EventHandler(findAllReferences_Click);
                                        menuStrip.Items.Add(findAllReferences);
                                    }
                                }
                                if (structParent != null)
                                {
                                    variable = GetVariableByName(structParent, variable.Name);
                                    if (variable != null)
                                    {
                                        AddStructItemsToMenu(menuStrip, structParent);

                                        //var goToClassDefinition = new ToolStripMenuItem("&Go to definition of class " + structParent.GetActiveName());
                                        //goToClassDefinition.Tag = variable;
                                        //goToClassDefinition.Click += new EventHandler(goToDefinition_Click);
                                        //menuStrip.Items.Add(goToClassDefinition);
                                        //var goToClassDefinition2 = new ToolStripMenuItem("Go to definition of class " + structParent.GetActiveName() + " in a new &tab");
                                        //goToClassDefinition2.Tag = variable;
                                        //goToClassDefinition2.Click += new EventHandler(goToDefinition2_Click);
                                        //menuStrip.Items.Add(goToClassDefinition2);

                                        var findAllReferences = new ToolStripMenuItem("&Find all references of struct member " + variable.GetActiveName());
                                        findAllReferences.Tag = variable;
                                        findAllReferences.Click += new EventHandler(findAllReferences_Click);
                                        menuStrip.Items.Add(findAllReferences);
                                    }
                                }
                                if (global != null)
                                {
                                    var findAllReferences = new ToolStripMenuItem("&Find all references of global variable " + variable.GetActiveName());
                                    findAllReferences.Tag = variable;
                                    findAllReferences.Click += new EventHandler(findAllReferences_Click);
                                    menuStrip.Items.AddWithSeparator(findAllReferences);
                                }
                                if (variable.StructType >= 0 && variable.StructType < ainFile.Structs.Count)
                                {
                                    var structInfo = ainFile.Structs[variable.StructType];
                                    AddStructItemsToMenu(menuStrip, structInfo);
                                }

                                //int number;
                                //if (int.TryParse(variable.Name, out number))
                                //{
                                //    var findAllReferences = new ToolStripMenuItem("&Find all references of int " + number.ToString() + "...");
                                //    findAllReferences.Tag = number;
                                //    findAllReferences.Click += new EventHandler(findAllReferences_Click);
                                //    menuStrip.Items.Add(findAllReferences);
                                //}
                            }
                            else
                            {
                                var builtInFunction = CodeSearchCache.BuiltInFunctions.GetOrNull((int)expression.ExpressionType);
                                if (builtInFunction != null)
                                {
                                    var findAllReferences = new ToolStripMenuItem("Find all references of &Built-in method " + builtInFunction.Name);
                                    findAllReferences.Tag = builtInFunction;
                                    findAllReferences.Click += new EventHandler(findAllReferences_Click);
                                    menuStrip.Items.Add(findAllReferences);
                                }
                            }
                        }
                        break;
                }
                if (functionParameter != null)
                {
                    var editMetadata = new ToolStripMenuItem("&Edit Metadata of function parameter " + functionParameter.GetActiveName() + "...");
                    editMetadata.Tag = functionParameter;
                    editMetadata.Click += new EventHandler(editMetadata_Click);
                    menuStrip.Items.AddWithSeparator(editMetadata);
                }
                if (menuStrip.Items.Count > 0)
                {
                    menuStrip.Items.Add(new ToolStripSeparator());
                }
            }

            var backItem = new ToolStripMenuItem("&Back", null, backButton_Click); menuStrip.Items.Add(backItem);
            var forwardItem = new ToolStripMenuItem("For&ward", null, forwardButton_Click); menuStrip.Items.Add(forwardItem);
            menuStrip.Items.Add(new ToolStripSeparator());
            var selectAllItem = new ToolStripMenuItem("Select &All", null, selectAllToolStripMenuItem_Click); menuStrip.Items.Add(selectAllItem);
            var copyItem = new ToolStripMenuItem("&Copy", null, copyToolStripMenuItem_Click); menuStrip.Items.Add(copyItem);

            backItem.Enabled = backToolStripMenuItem.Enabled;
            forwardItem.Enabled = forwardToolStripMenuItem.Enabled;
            editToolStripMenuItem_DropDownOpening(editToolStripMenuItem, EventArgs.Empty);
            selectAllItem.Enabled = selectAllToolStripMenuItem.Enabled;
            copyItem.Enabled = copyToolStripMenuItem.Enabled;

            //variableNameForContextMenu = variableNameUnderCursor;
            //if (ainFile.FunctionNameToIndex.ContainsKey(variableNameForContextMenu))
            //{
            //    var goToFunctionToolStripMenuItem = new ToolStripMenuItem();
            //    goToFunctionToolStripMenuItem.Text = "&Go to definition of " + variableNameUnderCursor;
            //    goToFunctionToolStripMenuItem.Click += new EventHandler(goToFunctionToolStripMenuItem_Click);
            //    menuStrip.Items.Add(goToFunctionToolStripMenuItem);
            //    var findAllReferencesToolStripMenuItem = new ToolStripMenuItem();
            //    findAllReferencesToolStripMenuItem.Text = "&Find all references...";
            //    findAllReferencesToolStripMenuItem.Click += new EventHandler(findAllReferencesToolStripMenuItem_Click);
            //    menuStrip.Items.Add(findAllReferencesToolStripMenuItem);
            //}
            //if (menuStrip.Items.Count == 0)
            //{
            //    e.Cancel = true;
            //}
        }

        void editMetadata_Click(object sender, EventArgs e)
        {
            IVariable variable = null;
            ToolStripItem toolStripItem = (ToolStripItem)sender;
            if (sender != null)
            {
                variable = toolStripItem.Tag as IVariable;
            }
            if (variable != null)
            {
                using (var editMetadata = new EditMetadata2(variable))
                {
                    if (editMetadata.ShowDialog() == DialogResult.OK)
                    {
                        GoRefresh();
                    }
                }
            }
        }

        private void AddFunctionItemsToMenu(ContextMenuStrip menuStrip, Function function)
        {
            if (function != null)
            {
                if (menuStrip.Items.Count > 0)
                {
                    var sep = new ToolStripSeparator();
                    menuStrip.Items.Add(sep);
                }
                var goToDefinition = new ToolStripMenuItem("&Go to definition of function " + function.GetActiveName());
                goToDefinition.Tag = function;
                goToDefinition.Click += new EventHandler(goToDefinition_Click);
                menuStrip.Items.Add(goToDefinition);
                var goToDefinition2 = new ToolStripMenuItem("Go to definition of function " + function.GetActiveName() + " in a new &tab");
                goToDefinition2.Tag = function;
                goToDefinition2.Click += new EventHandler(goToDefinition2_Click);
                menuStrip.Items.Add(goToDefinition2);
                var findAllReferences = new ToolStripMenuItem("Find all &references of function " + function.GetActiveName() + "...");
                findAllReferences.Tag = function;
                findAllReferences.Click += new EventHandler(findAllReferences_Click);
                menuStrip.Items.Add(findAllReferences);
                var structInfo = function.GetClass();
                AddStructItemsToMenu(menuStrip, structInfo);
            }
        }

        private void AddStructItemsToMenu(ContextMenuStrip menuStrip, Struct structInfo)
        {
            if (structInfo != null)
            {
                if (menuStrip.Items.Count > 0)
                {
                    var sep = new ToolStripSeparator();
                    menuStrip.Items.Add(sep);
                }
                var goToDefinition = new ToolStripMenuItem("Go to definition of &class " + structInfo.GetActiveName());
                goToDefinition.Tag = structInfo;
                goToDefinition.Click += new EventHandler(goToDefinition_Click);
                menuStrip.Items.Add(goToDefinition);

                var goToDefinition2 = new ToolStripMenuItem("Go to definition of class " + structInfo.GetActiveName() + " in a new ta&b");
                goToDefinition2.Tag = structInfo;
                goToDefinition2.Click += new EventHandler(goToDefinition2_Click);
                menuStrip.Items.Add(goToDefinition2);
                //var findAllReferences = new ToolStripMenuItem("Find &all references of class " + structInfo.GetActiveName());
                //findAllReferences.Tag = structInfo;
                //findAllReferences.Click += new EventHandler(findAllReferences_Click);
                //menuStrip.Items.Add(findAllReferences);
            }
        }

        private IVariable GetVariableByName(IFunction functionParent, string variableName)
        {
            return functionParent.Parameters.Where(v => v.Name == variableName).FirstOrDefault();
        }

        void findAllReferences_Click(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem == null)
            {
                return;
            }
            var function = menuItem.Tag as Function;
            var stringValue = menuItem.Tag as string;
            var intValue = menuItem.Tag as int?;
            var variable = menuItem.Tag as IVariable;
            var parentFunction = (variable != null) ? variable.Parent as Function : null;
            var parentStruct = (variable != null) ? variable.Parent as Struct : null;
            var global = variable as Global;
            var libraryCall = menuItem.Tag as HllFunction;

            SearchKind searchKind = 0;
            string searchQuery = "";
            int functionNumber = -1;

            if (function != null)
            {
                if (function.Root != null)
                {
                    searchKind = SearchKind.Function;
                    functionNumber = function.Index;
                    searchQuery = function.Name;
                }
                else if (function.Name.StartsWith("system."))
                {
                    searchKind = SearchKind.SystemCalls;
                    searchQuery = function.Name;
                }
                else
                {
                    searchKind = SearchKind.BuiltInMethod;
                    searchQuery = function.Name;
                }
            }
            if (stringValue != null)
            {
                searchKind = SearchKind.String;
                searchQuery = stringValue;
            }
            if (intValue != null)
            {
                searchKind = SearchKind.Int;
                searchQuery = intValue.Value.ToString();
            }
            if (parentFunction != null)
            {
                searchKind = SearchKind.Local;
                functionNumber = parentFunction.Index;
                searchQuery = variable.Name;
            }
            if (parentStruct != null)
            {
                searchKind = SearchKind.StructMember;
                searchQuery = parentStruct.Name + "." + variable.Name;
            }
            if (global != null)
            {
                searchKind = SearchKind.Global;
                var displayer = new ExpressionDisplayer(ainFile, functionNumber);
                searchQuery = displayer.GetVariableName(variable);
            }
            if (libraryCall != null)
            {
                searchKind = SearchKind.LibraryCalls;
                searchQuery = libraryCall.ParentLibrary.LibraryName + "." + libraryCall.Name;
            }

            var searchForm = new FunctionSearchForm2(this, searchQuery, searchKind, functionNumber);
            searchForm.Show();
        }

        void goToDefinition_Click(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem == null)
            {
                return;
            }
            var function = menuItem.Tag as Function;
            var stringValue = menuItem.Tag as string;
            var intValue = menuItem.Tag as int?;
            var variable = menuItem.Tag as IVariable;
            var parentFunction = (variable != null) ? variable.Parent as Function : null;
            var parentStruct = (variable != null) ? variable.Parent as Struct : null;
            var global = variable as Global;
            var structInfo = menuItem.Tag as Struct;

            if (function != null)
            {
                this.NavigateToFunction(function);
            }
            if (parentFunction != null)
            {
                var node = this.expressionMap.FindVariable(variable).FirstOrDefault();
                if (node != null)
                {
                    this.DecompiledCodeTextBox.CurrentPos = node.start;
                }
            }
            if (parentStruct != null)
            {
                this.VisitClass(parentStruct.Name, false);
            }
            if (structInfo != null)
            {
                this.VisitClass(structInfo.Name, false);
            }
        }

        void goToDefinition2_Click(object sender, EventArgs e)
        {
            NewTab();
            goToDefinition_Click(sender, e);
        }

        //void findAllReferencesToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    var functionSearchForm = new FunctionSearchForm(this, variableNameForContextMenu);
        //    functionSearchForm.Show();
        //}

        //void goToFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    if (ainFile.FunctionNameToIndex.ContainsKey(variableNameForContextMenu))
        //    {
        //        var function = ainFile.Functions[ainFile.FunctionNameToIndex[variableNameForContextMenu]];
        //        this.NavigateToFunction(function);
        //    }
        //}

        private void DecompiledCodeTextBox_MouseLeave(object sender, EventArgs e)
        {
            HidePopupForm();

        }

        private void HidePopupForm()
        {
            if (popupForm != null)
            {
                popupExpression = null;
                popupForm.Hide();
            }
        }

        private void DecompiledCodeTextBox_Scroll(object sender, ScrollEventArgs e)
        {
            HidePopupForm();
        }

        #endregion

        #region Navigation and history items

        private void VisitFunctionInNewTab()
        {
            if (popupExpression != null)
            {
                var function = popupExpression.Variable as Function;
                if (function != null && !function.Name.StartsWith("system."))
                {
                    NewTab();
                    VisitFunction(function, false);
                }
                else
                {
                    var variable = popupExpression.Variable as IVariable;
                    if (variable != null)
                    {
                        var structInfo = variable.GetStructType();
                        if (structInfo != null)
                        {
                            NewTab();
                            VisitClass(structInfo.Name, false);
                        }
                    }
                }
            }
        }

        private void SavePositionForHistory()
        {
            int currentTab = currentTabIndex;
            int index = HistoryIndex.GetOrDefault(currentTab, 0);
            var history = History.GetOrNew(currentTab);

            var historyItem = history.GetOrNew(index);
            EncodeHistoryItem(historyItem);
            history.SetOrAdd(index, historyItem);
        }

        private void VisitPageForHistory()
        {
            int currentTab = currentTabIndex;
            int index = HistoryIndex.GetOrDefault(currentTab, 0);
            var history = History.GetOrNew(currentTab);

            //remove the empty item if it exists
            if (index >= 0 && index < history.Count)
            {
                var item = history[index];
                if (item.FileNameToDisassemble == null && item.FunctionName == null && item.ClassName == null)
                {
                    index--;
                }
            }

            index++;
            //erase everything past index
            if (history.Count - index > 0)
            {
                history.RemoveRange(index, history.Count - index);
            }
            var historyItem = new ExplorerHistoryItem();
            EncodeHistoryItem(historyItem);
            history.SetOrAdd(index, historyItem);
            History.SetOrAdd(currentTab, history);
            HistoryIndex.SetOrAdd(currentTab, index);
            EnableDisableNavigationButtons();
        }

        void EnableDisableNavigationButtons()
        {
            int currentTab = currentTabIndex;
            int index = HistoryIndex.GetOrDefault(currentTab, 0);
            var history = History.GetOrNew(currentTab);

            bool enableBack = false;
            bool enableForward = false;
            bool enableClose = false;

            if (index > 0)
            {
                enableBack = true;
            }
            if (index < history.Count - 1)
            {
                enableForward = true;
            }
            if (this.ExplorerTabControl.TabPages.Count > 1)
            {
                enableClose = true;
            }

            backToolStripMenuItem.Enabled = enableBack;
            backButton.Enabled = enableBack;
            forwardToolStripMenuItem.Enabled = enableForward;
            forwardButton.Enabled = enableForward;
            closeTabToolStripMenuItem.Enabled = enableClose;
            closeTabToolStripMenuItem1.Enabled = enableClose;
            nextTabToolStripMenuItem.Enabled = enableClose;
            previousTabToolStripMenuItem.Enabled = enableClose;
        }

        void GoBack()
        {
            int currentTab = currentTabIndex;
            int index = HistoryIndex.GetOrDefault(currentTab, 0);
            GoToHistoryItem(currentTab, index - 1);
        }

        void GoToHistoryItem(int newTabIndex, int newIndex)
        {
            int oldIndex = HistoryIndex.GetOrDefault(currentTabIndex, 0);
            var oldHistory = History.GetOrNew(currentTabIndex);

            var newHistory = History.GetOrNew(newTabIndex);

            if (newIndex >= 0 && newIndex < newHistory.Count)
            {
                var currentHistoryItem = oldHistory.GetOrNew(oldIndex);
                EncodeHistoryItem(currentHistoryItem);
                oldHistory.SetOrAdd(oldIndex, currentHistoryItem);
                History.SetOrAdd(currentTabIndex, oldHistory);

                currentTabIndex = newTabIndex;

                newHistory = History.GetOrNew(newTabIndex);
                HistoryIndex.SetOrAdd(newTabIndex, newIndex);
                var nextHistoryItem = newHistory.GetOrNew(newIndex);
                NavigateToHistoryItem(nextHistoryItem);
                EnableDisableNavigationButtons();
            }
        }

        void EncodeHistoryItem(ExplorerHistoryItem explorerHistoryItem)
        {
            if (codeDisplayState != null)
            {
                explorerHistoryItem.StartAddress = codeDisplayState.FunctionAddress;
                explorerHistoryItem.EndAddress = codeDisplayState.EndAddress;
                explorerHistoryItem.FileNameToDisassemble = codeDisplayState.FileNameToDisassemble;
                explorerHistoryItem.FunctionName = codeDisplayState.FunctionName;
                explorerHistoryItem.ClassName = codeDisplayState.ClassName;
                explorerHistoryItem.FileNameToEdit = codeDisplayState.FileNameToEdit;
                explorerHistoryItem.document = codeDisplayState.Document;
            }
            explorerHistoryItem.ScintillaState = new ScintillaState();
            explorerHistoryItem.ScintillaState.ReadFromScintilla(DecompiledCodeTextBox);

            explorerHistoryItem.ScintillaState2 = new ScintillaState();
            explorerHistoryItem.ScintillaState2.ReadFromScintilla(DisassembledCodeTextBox);

            explorerHistoryItem.ScintillaState3 = new ScintillaState();
            explorerHistoryItem.ScintillaState3.ReadFromScintilla(CombinedViewTextBox);
        }

        void NavigateToHistoryItem(ExplorerHistoryItem explorerHistoryItem)
        {
            this.nextScintillaState = explorerHistoryItem.ScintillaState;
            this.nextScintillaState2 = explorerHistoryItem.ScintillaState2;
            this.nextScintillaState3 = explorerHistoryItem.ScintillaState3;
            if (!String.IsNullOrEmpty(explorerHistoryItem.FileNameToDisassemble))
            {
                VisitFile(explorerHistoryItem.FileNameToDisassemble, explorerHistoryItem.StartAddress, explorerHistoryItem.EndAddress, true);
            }
            else if (!String.IsNullOrEmpty(explorerHistoryItem.FunctionName))
            {
                VisitFunction(explorerHistoryItem.FunctionName, explorerHistoryItem.StartAddress, true);
            }
            else if (!String.IsNullOrEmpty(explorerHistoryItem.ClassName))
            {
                VisitClass(explorerHistoryItem.ClassName, true);
            }
            else
            {
                DisplayFunction(new CodeDisplayState() { Revisiting = true, Document = explorerHistoryItem.document, FileNameToEdit = explorerHistoryItem.FileNameToEdit });
            }
        }

        void GoForward()
        {
            int currentTab = currentTabIndex;
            int index = HistoryIndex.GetOrDefault(currentTab, 0);
            GoToHistoryItem(currentTab, index + 1);
        }

        void CloseTab()
        {
            if (this.ExplorerTabControl.TabPages.Count <= 1)
            {
                return;
            }

            Document documentToDestroy = null;
            if (CurrentTabIsTextEditor())
            {
                if (!PromptToSaveDocument()) return;
                documentToDestroy = GetActiveTab().document;
            }

            int tabIndexToRemove = this.currentTabIndex;

            if (currentTabIndex < this.ExplorerTabControl.TabPages.Count - 1)
            {
                NextTab();
            }
            else
            {
                PreviousTab();
            }

            try
            {
                suspendTabEvents = true;
                History.RemoveAt(tabIndexToRemove);
                HistoryIndex.RemoveAt(tabIndexToRemove);
                if (documentToDestroy != null && !documentToDestroy.IsDisposed && documentToDestroy != defaultDocument1)
                {
                    documentToDestroy.Dispose();
                }
                this.ExplorerTabControl.TabPages.RemoveAt(tabIndexToRemove);
                if (currentTabIndex >= tabIndexToRemove)
                {
                    currentTabIndex--;
                }
                this.ExplorerTabControl.SelectedIndex = currentTabIndex;
            }
            finally
            {
                suspendTabEvents = false;
            }

            if (this.ExplorerTabControl.TabPages.Count <= 1)
            {
                this.ExplorerTabControl.Visible = false;
            }

            EnableDisableNavigationButtons();
        }

        private void NextTab()
        {
            int nextTab = currentTabIndex + 1;
            if (nextTab >= this.ExplorerTabControl.TabCount)
            {
                nextTab = 0;
            }
            SelectTab(nextTab);
        }

        private void PreviousTab()
        {
            int previousTab = currentTabIndex - 1;
            if (previousTab < 0)
            {
                previousTab = this.ExplorerTabControl.TabCount - 1;
            }
            SelectTab(previousTab);
        }

        #region Text Editor Tabs

        bool CurrentTabIsTextEditor(int tabIndex)
        {
            var historyItem = GetActiveTab(tabIndex);
            return (historyItem != null && historyItem.NavigationLocked && (historyItem.document != null && historyItem.document != defaultDocument1));
        }

        bool CurrentTabIsTextEditor()
        {
            return CurrentTabIsTextEditor(this.currentTabIndex);
        }

        private ExplorerHistoryItem GetActiveTab()
        {
            return GetActiveTab(this.currentTabIndex);
        }

        private ExplorerHistoryItem GetActiveTab(int tabIndex)
        {
            var historyList = this.History.GetOrNull(tabIndex);
            if (historyList != null)
            {
                int historyIndex = HistoryIndex.GetOrDefault(tabIndex, 0);
                var historyItem = historyList.GetOrNull(historyIndex);
                return historyItem;
            }
            return null;
        }

        bool PromptToSaveEditors()
        {
            for (int i = 0; i < this.History.Count; i++)
            {
                if (CurrentTabIsTextEditor(i))
                {
                    bool modified = false;
                    var scintillaState = GetActiveTab(i).ScintillaState;
                    if (scintillaState != null)
                    {
                        modified = scintillaState.Modified;
                    }
                    if (i == currentTabIndex)
                    {
                        modified = DecompiledCodeTextBox.Modified;
                    }
                    if (modified)
                    {
                        this.ExplorerTabControl.SelectedIndex = i;
                        bool result = PromptToSaveDocument();
                        if (result == false)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private bool PromptToSaveDocument()
        {
            //string currentFileName = codeDisplayState.FileNameToEdit;
            if (DecompiledCodeTextBox.Modified && !DecompiledCodeTextBox.IsReadOnly)
            {
                string currentFileName = codeDisplayState.FileNameToEdit;
                if (String.IsNullOrEmpty(currentFileName)) currentFileName = "Untitled Document";
                var dialogResult = MessageBox.Show(this, "Save changes to " + currentFileName + "?", "AinDecompiler", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                if (dialogResult == DialogResult.Cancel)
                {
                    return false;
                }
                if (dialogResult == DialogResult.Yes)
                {
                    if (SaveDocument())
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;

        }

        private bool SaveDocument()
        {
            string currentFileName = codeDisplayState.FileNameToEdit;
            if (String.IsNullOrEmpty(currentFileName))
            {
                return SaveDocumentAs();
            }
            else
            {
                try
                {
                    File.WriteAllText(currentFileName, DecompiledCodeTextBox.Text, Extensions.TextEncoding);
                    this.DecompiledCodeTextBox.Modified = false;
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        private bool SaveDocumentAs()
        {
            string lastFileName = codeDisplayState.FileNameToEdit;
            string fileName = null;
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "JAF Files (*.jaf)|*.jaf|JAM Files (*.jam)|*.jam|Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.DefaultExt = "jaf";
                saveFileDialog.FileName = lastFileName;
                var dialogResult = saveFileDialog.ShowDialogWithTopic(DialogTopic.SaveDocument);
                if (dialogResult == DialogResult.OK)
                {
                    fileName = saveFileDialog.FileName;
                }
            }
            if (fileName != null)
            {
                SaveDocumentAs(fileName);
            }
            return false;
        }

        bool SaveDocumentAs(string fileName)
        {
            codeDisplayState.FileNameToEdit = fileName;
            GetActiveTab().FileNameToEdit = fileName;
            if (SaveDocument())
            {
                ExplorerTabControl.SelectedTab.Text = Path.GetFileName(fileName);
                return true;
            }
            else
            {
                if (codeDisplayState != null)
                {
                    string lastFileName = codeDisplayState.FileNameToEdit;
                    codeDisplayState.FileNameToEdit = lastFileName;
                    GetActiveTab().FileNameToEdit = lastFileName;
                }
                return false;
            }
        }

        bool OpenDocument()
        {
            string[] fileNames = null;

            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JAF Files (*.jaf)|*.jaf|JAM Files (*.jam)|*.jam|Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                openFileDialog.ShowReadOnly = false;
                openFileDialog.DefaultExt = "jaf";
                openFileDialog.Multiselect = true;

                var dialogResult = openFileDialog.ShowDialogWithTopic(DialogTopic.OpenDocument);
                if (dialogResult == DialogResult.OK)
                {
                    fileNames = openFileDialog.FileNames;
                }
            }
            if (fileNames != null)
            {
                bool success = true;
                foreach (var fileName in fileNames)
                {
                    success = OpenDocument(fileName);
                    if (!success)
                    {
                        return false;
                    }
                }
            }
            return false;
        }

        bool OpenDocumentInCurrentTab(string fileName)
        {
            bool fileExists = false;
            try
            {
                fileName = Path.GetFullPath(fileName);
                if (File.Exists(fileName))
                {
                    fileExists = true;
                }
            }
            catch
            {
                return false;
            }
            if (!fileExists)
            {
                return false;
            }
            string text = null;
            try
            {
                var encoding = EncodingDetector.DetectEncoding(fileName);
                text = File.ReadAllText(fileName, encoding);
            }
            catch
            {
                return false;
            }
            if (text != null)
            {
                DecompiledCodeTextBox.IsReadOnly = false;
                DecompiledCodeTextBox.Text = text;
                DecompiledCodeTextBox.UndoRedo.EmptyUndoBuffer();
                DecompiledCodeTextBox.Modified = false;
                codeDisplayState.FileNameToEdit = fileName;
                GetActiveTab().FileNameToEdit = fileName;
                ExplorerTabControl.SelectedTab.Text = Path.GetFileName(fileName);
                return true;
            }
            return false;
        }

        private bool OpenDocument(string fileName)
        {
            for (int i = 0; i < this.History.Count; i++)
            {
                if (CurrentTabIsTextEditor(i))
                {
                    var historyItem = GetActiveTab(i);
                    if (Path.GetFullPath(historyItem.FileNameToEdit).Equals(Path.GetFullPath(fileName), StringComparison.OrdinalIgnoreCase))
                    {
                        ExplorerTabControl.SelectedIndex = i;
                        return true;
                    }
                }
            }
            NewEditorTab();
            return OpenDocumentInCurrentTab(fileName);
        }

        public void NewEditorTab()
        {
            NewTab(true, true);
            this.ExplorerTabControl.SelectedTab.Text = "Untitled";
            this.DecompiledCodeTextBox.Select();
            this.DecompiledCodeTextBox.Encoding = UTF8Encoding.UTF8;
            this.DecompiledCodeTextBox.ConfigurationManager.Language = "cs";
            this.DecompiledCodeTextBox.Font = new System.Drawing.Font("Consolas", 10.75F);
            this.DecompiledCodeTextBox.UpdateFonts();
            this.DecompiledCodeTextBox.IsBraceMatching = true;
            this.DecompiledCodeTextBox.Styles.BraceBad.Size = 9F;
            this.DecompiledCodeTextBox.Styles.BraceLight.Size = 9F;
            this.DecompiledCodeTextBox.Styles.ControlChar.Size = 9F;
            this.DecompiledCodeTextBox.Styles.Default.BackColor = System.Drawing.SystemColors.Window;
            this.DecompiledCodeTextBox.Styles.Default.Size = 9F;
            this.DecompiledCodeTextBox.Styles.IndentGuide.Size = 9F;
            this.DecompiledCodeTextBox.Styles.LastPredefined.Size = 9F;
            this.DecompiledCodeTextBox.Styles.LineNumber.Size = 9F;
            this.DecompiledCodeTextBox.Styles.Max.Size = 9F;
            this.DecompiledCodeTextBox.Modified = false;
        }

        #endregion

        public void NewTab()
        {
            NewTab(true, false);
        }

        public int NewTab(bool visitAfterCreating, bool makeEditor)
        {
            int currentTab = currentTabIndex;
            if (currentTab >= this.ExplorerTabControl.TabCount)
            {
                currentTab = this.ExplorerTabControl.TabCount - 1;
            }

            int newTabIndex = currentTabIndex + 1;

            if (ExplorerTabControl.TabPages.Count == 0)
            {
                newTabIndex = 0;
            }

            var newTabPage = new TabPage("New Tab");
            if (newTabIndex >= ExplorerTabControl.TabPages.Count)
            {
                this.ExplorerTabControl.TabPages.Add(newTabPage);
            }
            else
            {
                this.ExplorerTabControl.TabPages.Insert(newTabIndex, newTabPage);
            }

            //ensure history exists
            //var item = this.History.GetOrNew(currentTab);
            //item.Add(new ExplorerHistoryItem());
            //this.History.SetOrAdd(currentTab, item);

            //int i = this.HistoryIndex.GetOrDefault(currentTab, 0);
            //this.HistoryIndex.SetOrAdd(currentTab, i);

            var historyItem = new ExplorerHistoryItem();
            if (makeEditor)
            {
                historyItem.document = DecompiledCodeTextBox.DocumentHandler.Create();
                historyItem.NavigationLocked = true;
            }

            var newList = new List<ExplorerHistoryItem>();
            newList.Add(historyItem);

            this.History.Insert(newTabIndex, newList);
            this.HistoryIndex.Insert(newTabIndex, 0);

            if (this.ExplorerTabControl.TabPages.Count > 1)
            {
                this.ExplorerTabControl.Visible = true;
            }

            if (visitAfterCreating)
            {
                try
                {
                    suspendTabEvents = true;
                    SelectTab(newTabIndex);
                    this.ExplorerTabControl.SelectedIndex = newTabIndex;
                }
                finally
                {
                    suspendTabEvents = false;
                }
            }
            return newTabIndex;
        }

        void SelectTab(int newTabIndex)
        {
            GoToHistoryItem(newTabIndex, HistoryIndex.GetOrDefault(newTabIndex, 0));
        }

        private void backButton_Click(object sender, EventArgs e)
        {
            GoBack();
        }

        private void forwardButton_Click(object sender, EventArgs e)
        {
            GoForward();
        }

        private void backToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoBack();
        }

        private void forwardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoForward();
        }

        private void closeTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CloseTab();
        }

        private void newTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NewTab();
        }

        private void ExplorerTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!suspendTabEvents && !suspendTreeViewEvents)
            {
                SelectTab(ExplorerTabControl.SelectedIndex);
            }
        }

        private void closeTabToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            CloseTab();
        }

        private void newTabToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            NewTab();
        }

        private void ExplorerTabControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                CloseTab();
            }
        }

        private void historyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowHistory();
        }

        private void ShowHistory()
        {
            using (var historyForm = new HistoryForm())
            {
                int historyIndex = HistoryIndex.GetOrDefault(currentTabIndex, 0);
                var historyItems = History.GetOrNew(currentTabIndex);

                historyForm.HistoryItemsListBox.DataSource = historyItems;
                historyForm.HistoryItemsListBox.SelectedIndex = historyIndex;
                if (historyForm.ShowDialog() == DialogResult.OK)
                {
                    historyIndex = historyForm.HistoryItemsListBox.SelectedIndex;
                    GoToHistoryItem(currentTabIndex, historyIndex);
                }
            }

        }

        private void DecompiledCodeTextBox_MouseDown(object sender, MouseEventArgs e)
        {
            DecompiledCodeTextBox_MouseMove(sender, e);

            if (e.Button == MouseButtons.XButton1)
            {
                GoBack();
            }
            if (e.Button == MouseButtons.XButton2)
            {
                GoForward();
            }
            if (e.Button == MouseButtons.Middle)
            {
                VisitFunctionInNewTab();
            }
        }

        private void ExplorerTabControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            {
                var control = sender as Control;
                var screenPoint = control.PointToScreen(e.Location);

                NativeMethods.SimulateMouseLeftClick(screenPoint.X, screenPoint.Y);
            }
        }

        private void ClearTabs()
        {
            try
            {
                DestroyAllEditors();

                suspendTabEvents = true;
                this.ExplorerTabControl.TabPages.Clear();
                this.ExplorerTabControl.Visible = false;
                this.History.Clear();
                this.HistoryIndex.Clear();
                this.currentTabIndex = 0;
                this.codeDisplayState = null;
                this.expressionMap = null;
                var formsToClose = Application.OpenForms.OfType<FunctionSearchForm2>().ToArray();
                foreach (var form in formsToClose)
                {
                    form.Close();
                    form.Dispose();
                }
                var formsToClose2 = Application.OpenForms.OfType<PropertyGridForm>().ToArray();
                foreach (var form in formsToClose2)
                {
                    form.Close();
                    form.Dispose();
                }

                GC.Collect();
                NewTab();
            }
            finally
            {
                suspendTabEvents = false;
            }
        }

        private void DestroyAllEditors()
        {
            if (this.DecompiledCodeTextBox.DocumentHandler.Current.Handle != defaultDocument1.Handle)
            {
                this.DecompiledCodeTextBox.DocumentHandler.Current = defaultDocument1;
            }
            for (int i = 0; i < this.History.Count; i++)
            {
                if (CurrentTabIsTextEditor(i))
                {
                    var e = GetActiveTab(i);
                    if (e.document != null && !e.document.IsDisposed && e.document != defaultDocument1)
                    {
                        e.document.Dispose();
                    }
                }
            }
        }

        #endregion

        #region Tree View item selection and other tree view stuff

        private void MakeTreeView()
        {
            this.fileToolStripMenuItem_DropDownOpening(this.fileToolStripMenuItem, EventArgs.Empty);

            TreeView.BeginUpdate();
            bool oldSuspendTreeViewEvents = suspendTreeViewEvents;
            try
            {
                suspendTreeViewEvents = true;
                RemoveTags(TreeView.Nodes);

                try
                {
                    TreeView.Nodes.Clear();
                }
                catch (AccessViolationException ex)
                {
                    AccessViolationHandler();
                }

                var functionEnumerator = new FunctionEnumerator(ainFile);

                //add "Classes" node and children
                var classesResults = functionEnumerator.GetClasses();
                foreach (var classes in classesResults)
                {
                    var newFileTreeNode = new TreeNode(classes.name);
                    newFileTreeNode.Tag = classes;
                    TreeView.Nodes.Add(newFileTreeNode);
                    foreach (var function in classes.children)
                    {
                        var newTreeNode = new TreeNode(function.name);
                        newTreeNode.Tag = function;
                        newFileTreeNode.Nodes.Add(newTreeNode);
                    }
                }

                //add files and functions
                var results = functionEnumerator.GetFilesAndFunctions();
                foreach (var file in results)
                {
                    var newFileTreeNode = new TreeNode(file.name);
                    newFileTreeNode.Tag = file;
                    TreeView.Nodes.Add(newFileTreeNode);
                    foreach (var function in file.children)
                    {
                        var newTreeNode = new TreeNode(function.name);
                        newTreeNode.Tag = function;
                        newFileTreeNode.Nodes.Add(newTreeNode);
                    }
                }
            }
            finally
            {
                TreeView.EndUpdate();
                suspendTreeViewEvents = oldSuspendTreeViewEvents;
            }
        }

        private void AccessViolationHandler()
        {
            //no idea why this happens, but sometimes the TreeView needs to be re-created.
            var parent = TreeView.Parent;
            try
            {
                TreeView.Dispose();
            }
            catch (AccessViolationException ex2)
            {
                TreeView = null;
            }

            if (TreeView != null && parent.Controls.Contains(TreeView))
            {
                TreeView.Parent.Controls.Remove(TreeView);
            }

            this.SuspendLayout();
            TreeView = new TreeView();
            TreeView.ContextMenuStrip = this.contextMenuStripForTreeview;
            TreeView.Dock = System.Windows.Forms.DockStyle.Fill;
            TreeView.HideSelection = false;
            TreeView.Location = new System.Drawing.Point(0, 0);
            TreeView.Name = "TreeView";
            TreeView.Size = new System.Drawing.Size(240, 612);
            TreeView.TabIndex = 0;
            TreeView.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            parent.Controls.Add(TreeView);
            this.ResumeLayout();
            TreeView.BeginUpdate();
        }

        private void RemoveTags(TreeNodeCollection treeNodeCollection)
        {
            //seems to prevent errors sometimes
            foreach (TreeNode node in treeNodeCollection)
            {
                node.Tag = null;
                if (node.Nodes != null && node.Nodes.Count > 0)
                {
                    RemoveTags(node.Nodes);
                }
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (suspendTabEvents)
            {
                //treeview1 gets focus after hiding the tabs bar
                return;
            }
            var node = e.Node;
            SelectTreeNode(node);
        }

        private void SelectTreeNode(TreeNode node)
        {
            if (node == null) return;
            var nodeInfo = node.Tag as FunctionEnumerationResult;
            if (nodeInfo != null)
            {
                if (nodeInfo.children != null)
                {
                    if (nodeInfo.address != -1)
                    {
                        VisitFile(nodeInfo.name, nodeInfo.address + 6, nodeInfo.lastAddress, false);
                    }
                }
                else
                {
                    if (nodeInfo.address != -1 && nodeInfo.lastAddress != -1)
                    {
                        int functionAddress = nodeInfo.address + 6;
                        VisitFunction(nodeInfo.name, functionAddress, false);
                    }
                    else
                    {
                        VisitClass(nodeInfo.name, false);
                    }
                }
            }
        }

        public void NavigateToFunction(Function function)
        {
            bool found = false;
            foreach (TreeNode fileNode in TreeView.Nodes)
            {
                foreach (TreeNode functionNode in fileNode.Nodes)
                {
                    var enumerationResult = functionNode.Tag as FunctionEnumerationResult;
                    if (enumerationResult != null)
                    {
                        var otherFunction = ainFile.GetFunction(enumerationResult.name);
                        int functionAddress = enumerationResult.address + 6;

                        //if (ainFile.FunctionNameToIndex.ContainsKey(enumerationResult.name))
                        //{
                        //    var otherFunction = ainFile.Functions[ainFile.FunctionNameToIndex[enumerationResult.name]];
                        if (otherFunction == function && function.Address == functionAddress)
                        {
                            TreeView.SelectedNode = null;
                            TreeView.SelectedNode = functionNode;
                            found = true;
                        }
                        //}
                    }
                }
            }
            if (!found)
            {
                VisitFunction(function, false);
            }
        }

        Function GetSelectedFunction()
        {
            var selectedNode = TreeView.SelectedNode;
            if (selectedNode != null)
            {
                var enumerationResult = selectedNode.Tag as FunctionEnumerationResult;
                if (enumerationResult != null)
                {
                    if (ainFile.FunctionNameToIndex.ContainsKey(enumerationResult.name))
                    {
                        return ainFile.Functions[ainFile.FunctionNameToIndex[enumerationResult.name]];
                    }
                }
                return selectedNode.Tag as Function;
            }
            return null;
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            var selectedFunction = GetSelectedFunction();
            if (selectedFunction == null)
            {
                var selectedFileName = GetSelectedFileName();
                if (selectedFileName == null)
                {
                    e.Cancel = true;
                }
            }
        }

        string GetSelectedFileName()
        {
            var selectedNode = TreeView.SelectedNode;
            if (selectedNode != null)
            {
                var enumerationResult = selectedNode.Tag as FunctionEnumerationResult;
                if (enumerationResult != null)
                {
                    if (ainFile.FunctionNameToIndex.ContainsKey(enumerationResult.name))
                    {
                        return null;
                    }
                    else
                    {
                        return enumerationResult.name;
                    }
                }
                return selectedNode.Tag as String;
            }
            return null;
        }

        private void copyNameToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectedFunction = GetSelectedFunction();
            if (selectedFunction != null)
            {
                Clipboard.SetText(selectedFunction.Name);
            }
            else
            {
                var selectedFileName = GetSelectedFileName();
                if (selectedFileName != null)
                {
                    Clipboard.SetText(selectedFileName);
                }
            }
        }

        private void getCallsAndCalledByInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectedFunction = GetSelectedFunction();
            if (selectedFunction != null)
            {
                var functionSearchForm = new FunctionSearchForm2(this, selectedFunction.Name, SearchKind.Function, selectedFunction.Index);
                functionSearchForm.Show();
            }
        }




        #endregion

        #region Viewing a function

        void VisitFunction(string functionName, bool revisiting)
        {
            if (ainFile.FunctionNameToIndex.ContainsKey(functionName))
            {
                if (ainFile.FunctionNameToIndex.ContainsKey(functionName))
                {
                    var function = ainFile.Functions[ainFile.FunctionNameToIndex[functionName]];
                    VisitFunction(function, revisiting);
                }
            }
        }

        void VisitFunction(Function function, bool revisiting)
        {
            int functionAddress = function.Address;
            VisitFunction(function.Name, functionAddress, revisiting);
        }

        void VisitFunction(string functionName, int functionAddress, bool revisiting)
        {
            var decompiler = new Decompiler(this.ainFile);
            int endAddress = decompiler.FindEndAddress(functionAddress);

            Function function = null;
            if (ainFile.FunctionNameToIndex.ContainsKey(functionName))
            {
                function = ainFile.Functions[ainFile.FunctionNameToIndex[functionName]];
            }

            if (AnyUnknownFunctypes(function))
            {
                ainFile.FindFunctionTypes();
            }


            bool useAlternateThread = false;
            bool useTry = !Debugger.IsAttached || useAlternateThread;

            var state = new CodeDisplayState();
            state.FunctionName = functionName;
            state.UseTry = useTry;
            state.ainFile = ainFile;
            state.FunctionAddress = functionAddress;
            state.EndAddress = endAddress;
            state.Function = function;
            state.CodeDisplayOptions = GetCodeDisplayOptions();
            state.Revisiting = revisiting;

            if (useAlternateThread)
            {
                if (bw != null && bw.IsBusy)
                {
                    bw.Abort();
                }
                bw = new MyBackgroundWorker();
                bw.WorkerSupportsCancellation = true;
                bw.WorkerReportsProgress = false;
                bw.DoWork += new DoWorkEventHandler(bw_DoWork);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
                bw.RunWorkerAsync(state);
            }
            else
            {
                VisitFunction(state);
                DisplayFunction(state);
            }
        }

        void VisitFile(string fileName, int functionAddress, int endAddress, bool revisiting)
        {
            var decompiler = new Decompiler(this.ainFile);

            bool useAlternateThread = false;
            bool useTry = !Debugger.IsAttached || useAlternateThread;

            var state = new CodeDisplayState();
            state.UseTry = useTry;
            state.ainFile = ainFile;
            state.FunctionAddress = functionAddress;
            state.EndAddress = endAddress;
            state.CodeDisplayOptions = GetCodeDisplayOptions();
            state.Revisiting = revisiting;
            state.FileNameToDisassemble = fileName;

            if (useAlternateThread)
            {
                if (bw != null && bw.IsBusy)
                {
                    bw.Abort();
                }
                bw = new MyBackgroundWorker();
                bw.WorkerSupportsCancellation = true;
                bw.WorkerReportsProgress = false;
                bw.DoWork += new DoWorkEventHandler(bw_DoWork);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
                bw.RunWorkerAsync(state);
            }
            else
            {
                if (this.SelectionTabControl.SelectedTab != this.DisassembledTabPage)
                {
                    return;
                }

                VisitFunction(state);
                DisplayFunction(state);
            }
        }

        public void VisitClass(string className, bool revisiting)
        {
            var decompiler = new Decompiler(this.ainFile);
            var state = new CodeDisplayState();

            if (AnyUnknownFunctypes(ainFile.GetStruct(className)))
            {
                ainFile.FindFunctionTypes();
            }

            state.UseTry = false;
            state.ainFile = ainFile;
            state.FunctionAddress = -1;
            state.EndAddress = -1;
            state.ClassName = className;
            state.CodeDisplayOptions = GetCodeDisplayOptions();
            state.Revisiting = revisiting;

            VisitFunction(state);
            DisplayFunction(state);
        }

        static void VisitFunction(CodeDisplayState state)
        {
            //called from either the alternate thread, or main thread

            var displayer = new ExpressionDisplayer(state.ainFile, state.CodeDisplayOptions);
            displayer.expressionMap = new ExpressionMap();
            displayer.expressionMap.displayer = displayer;

            Expression expression = null;
            string decompiledCodeText = null;
            var decompiler = new Decompiler(state.ainFile);

            if (state.Function != null)
            {
                if (state.UseTry)
                {
                    try
                    {
                        expression = decompiler.DecompileFunction(state.FunctionAddress);
                    }
                    catch (ThreadAbortException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        decompiledCodeText = "An exception occurred while decompiling the code:" + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace;
                    }
                }
                else
                {
                    expression = decompiler.DecompileFunction(state.FunctionAddress);
                }
            }

            if (expression != null)
            {
                if (state.UseTry)
                {
                    try
                    {
                        decompiledCodeText = displayer.PrintExpression2(expression);
                    }
                    catch (ThreadAbortException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        decompiledCodeText = "An exception occurred while displaying the decompiled code:" + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace;
                    }
                }
                else
                {
                    decompiledCodeText = displayer.PrintExpression2(expression);
                }
            }

            if (!String.IsNullOrEmpty(state.ClassName) && state.Function == null && state.FunctionAddress == -1)
            {
                decompiledCodeText = displayer.GetClassDeclaration(state.ClassName);
            }

            state.ExpressionMap = displayer.expressionMap;
            state.FunctionExpression = expression;
            state.DecompiledCodeText = decompiledCodeText;

            string disassembledCodeText = null;
            var disassembler = new DisassemblerForListFile(state.ainFile, state.CodeDisplayOptions);

            int functionAddress = state.FunctionAddress - 6;

            if (state.FunctionAddress != -1 && state.EndAddress != -1)
            {
                if (state.UseTry)
                {
                    try
                    {
                        disassembledCodeText = disassembler.DisassembleListFileRange(functionAddress, state.EndAddress);
                    }
                    catch (Exception ex)
                    {
                        disassembledCodeText = "An exception occurred while disassembling the code:" + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace;
                    }
                }
                else
                {
                    disassembledCodeText = disassembler.DisassembleListFileRange(functionAddress, state.EndAddress);
                }
            }
            state.DisassembledCodeText = disassembledCodeText;

            if (state.FunctionAddress != -1 && state.ExpressionMap != null && state.DecompiledCodeText != null)
            {
                var writer = new AssemblerProjectWriter(state.ainFile, state.CodeDisplayOptions);
                var lines = state.DecompiledCodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                state.CombinedViewCodeText = writer.GetCombinedFunction(state.FunctionAddress, lines, state.ExpressionMap.GetAddressPerLineNumber());
            }

        }

        private void DisplayFunction(CodeDisplayState state)
        {
            if (!state.Revisiting)
            {
                bool createdNewTab = false;

                //does current tab have navigation locked?
                var currentHistoryItem = GetActiveTab();

                if (currentHistoryItem != null)
                {
                    if (currentHistoryItem.NavigationLocked)
                    {
                        EncodeHistoryItem(currentHistoryItem);

                        int newTabIndex = NewTab(false, false);
                        bool oldSuspendTabEvents = this.suspendTabEvents;
                        try
                        {
                            this.suspendTabEvents = true;
                            this.ExplorerTabControl.SelectedIndex = newTabIndex;
                            this.currentTabIndex = newTabIndex;
                        }
                        finally
                        {
                            this.suspendTabEvents = oldSuspendTabEvents;
                        }
                        createdNewTab = true;
                    }
                }


                if (!createdNewTab)
                {
                    //save old information
                    SavePositionForHistory();
                }
            }

            this.codeDisplayState = state;
            if (state.FileNameToDisassemble != null)
            {
                this.ExplorerTabControl.SelectedTab.Text = state.FileNameToDisassemble;
            }
            if (state.FunctionName != null)
            {
                var function = ainFile.GetFunction(state.FunctionName);
                if (function != null)
                {
                    this.ExplorerTabControl.SelectedTab.Text = function.GetActiveName();
                }
            }
            if (state.ClassName != null)
            {
                var structInfo = ainFile.GetStruct(state.ClassName);
                if (structInfo != null)
                {
                    this.ExplorerTabControl.SelectedTab.Text = structInfo.GetActiveName();
                }
            }

            if (state.Document == null || state.Document == defaultDocument1)
            {
                if (DecompiledCodeTextBox.DocumentHandler.Current.Handle != defaultDocument1.Handle)
                {
                    DecompiledCodeTextBox.DocumentHandler.Current = defaultDocument1;
                }
                this.DecompiledCodeTextBox.IsReadOnly = false;
                this.DecompiledCodeTextBox.Text = state.DecompiledCodeText ?? "";
                this.DecompiledCodeTextBox.IsReadOnly = true;
                this.DecompiledCodeTextBox.ContextMenuStrip = this.contextMenuStripForTextEditor;
            }
            else
            {
                if (DecompiledCodeTextBox.DocumentHandler.Current.Handle != state.Document.Handle)
                {
                    DecompiledCodeTextBox.DocumentHandler.Current = state.Document;
                }
                this.DecompiledCodeTextBox.IsReadOnly = false;
                this.DecompiledCodeTextBox.ContextMenuStrip = null;
            }

            this.expressionMap = state.ExpressionMap;
            this.DisassembledCodeTextBox.Text = state.DisassembledCodeText ?? "";
            this.CombinedViewTextBox.Text = state.CombinedViewCodeText ?? "";

            if (!state.Revisiting)
            {
                VisitPageForHistory();
            }
            if (state.Revisiting)
            {
                if (nextScintillaState != null)
                {
                    nextScintillaState.WriteToScintilla(this.DecompiledCodeTextBox);
                    nextScintillaState = null;
                }
                if (nextScintillaState2 != null)
                {
                    nextScintillaState2.WriteToScintilla(this.DisassembledCodeTextBox);
                    nextScintillaState2 = null;
                }
                if (nextScintillaState3 != null)
                {
                    nextScintillaState3.WriteToScintilla(this.CombinedViewTextBox);
                    nextScintillaState3 = null;
                }
            }
        }

        private static bool AnyUnknownFunctypes(IFunction function)
        {
            if (function == null) return false;
            return AnyUnknownFunctypes(function.Parameters);
        }

        private static bool AnyUnknownFunctypes(IEnumerable<IVariable> parameters)
        {
            if (parameters == null) return false;
            bool anyUnknownFunctypes = (parameters.Any(v => (v.DataType == DataType.Functype || v.DataType == DataType.Delegate) && v.StructType == -1));
            return anyUnknownFunctypes;
        }

        void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var result = e.Result as CodeDisplayState;
            if (result != null)
            {
                DisplayFunction(result);
            }
        }

        void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            var state = e.Argument as CodeDisplayState;
            if (state != null)
            {
                VisitFunction(state);
            }
            e.Result = state;
        }

        private void SetCodeDisplayOptions(CodeDisplayOptions codeDisplayOptions)
        {
            this.displaythisAroundLocalMembersToolStripMenuItem.Checked = codeDisplayOptions.ShowThis;
            this.showAllCastingToolStripMenuItem.Checked = codeDisplayOptions.ShowCasting;
            this.declareVariablesAtBeginningOfFunctionToolStripMenuItem.Checked = codeDisplayOptions.DeclareVariablesAtBeginning;
            this.removeExtraReturnsToolStripMenuItem.Checked = codeDisplayOptions.RemoveReturns;
            this.verboseToolStripMenuItem.Checked = codeDisplayOptions.Verbose;
            this.showGlobalVariableprefixesToolStripMenuItem.Checked = codeDisplayOptions.ShowGlobalPrefix;
            this.useEnumerationsToolStripMenuItem.Checked = codeDisplayOptions.UseEnumerations;
            this.disassembleLittleEndianToolStripMenuItem.Checked = codeDisplayOptions.DisassembleDisplayBytes;
        }

        private void LoadCodeDisplayOptions()
        {
            var codeDisplayOptions = GetCodeDisplayOptions();
            RegistryUtility.GetObject("CodeDisplayOptions", codeDisplayOptions);
            SetCodeDisplayOptions(codeDisplayOptions);
        }

        private void SaveCodeDisplayOptions()
        {
            var codeDisplayOptions = GetCodeDisplayOptions();
            RegistryUtility.SaveObject("CodeDisplayOptions", codeDisplayOptions);
        }

        private CodeDisplayOptions GetCodeDisplayOptions()
        {
            var codeDisplayOptions = new CodeDisplayOptions()
            {
                ShowThis = this.displaythisAroundLocalMembersToolStripMenuItem.Checked,
                ShowCasting = this.showAllCastingToolStripMenuItem.Checked,
                DeclareVariablesAtBeginning = this.declareVariablesAtBeginningOfFunctionToolStripMenuItem.Checked,
                RemoveReturns = this.removeExtraReturnsToolStripMenuItem.Checked,
                Verbose = this.verboseToolStripMenuItem.Checked,
                ShowGlobalPrefix = this.showGlobalVariableprefixesToolStripMenuItem.Checked,
                UseEnumerations = this.useEnumerationsToolStripMenuItem.Checked,
                DisassembleDisplayBytes = this.disassembleLittleEndianToolStripMenuItem.Checked,
            };
            return codeDisplayOptions;
        }

        #endregion

        #region Actions

        private void ReplaceVariableNames()
        {
            ainFile = ainFile.Clone();

            for (int i = 0; i < ainFile.Strings.Count; i++)
            {
                if (ainFile.AlternativeNames.ContainsKey(ainFile.Strings[i]))
                {
                    ainFile.Strings[i] = ainFile.AlternativeNames[ainFile.Strings[i]];
                }
            }

            for (int i = 0; i < ainFile.Functions.Count; i++)
            {
                var function = ainFile.Functions[i];
                if (ainFile.AlternativeNames.ContainsKey(function.Name))
                {
                    function.Name = ainFile.AlternativeNames[function.Name];
                }

                var variables = function.Parameters;
                for (int j = 0; j < variables.Count; j++)
                {
                    var variable = variables[j];
                    if (ainFile.AlternativeNames.ContainsKey(variable.Name))
                    {
                        variable.Name = ainFile.AlternativeNames[variable.Name];
                    }
                }
            }
            for (int i = 0; i < ainFile.Structs.Count; i++)
            {
                var structInfo = ainFile.Structs[i];
                if (ainFile.AlternativeNames.ContainsKey(structInfo.Name))
                {
                    structInfo.Name = ainFile.AlternativeNames[structInfo.Name];
                }
                var members = structInfo.Members;
                for (int j = 0; j < members.Count; j++)
                {
                    var variable = members[j];
                    if (ainFile.AlternativeNames.ContainsKey(variable.Name))
                    {
                        variable.Name = ainFile.AlternativeNames[variable.Name];
                    }
                }
            }
            for (int i = 0; i < ainFile.Globals.Count; i++)
            {
                var variable = ainFile.Globals[i];
                if (ainFile.AlternativeNames.ContainsKey(variable.Name))
                {
                    variable.Name = ainFile.AlternativeNames[variable.Name];
                }
            }

            for (int i = 0; i < ainFile.GlobalGroupNames.Count; i++)
            {
                string globalGroupName = ainFile.GlobalGroupNames[i];
                if (ainFile.AlternativeNames.ContainsKey(globalGroupName))
                {
                    ainFile.GlobalGroupNames[i] = ainFile.AlternativeNames[globalGroupName];
                }
            }

            ainFile.CreateLookups();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFile();
        }

        private void OpenFile()
        {
            if (!PromptToSaveEditors())
            {
                return;
            }

            var openFileDialog = new OpenFileDialog();
            openFileDialog.RestoreDirectory = false;
            openFileDialog.Filter = "AIN Files (*.ain)|*.ain;*.ain_|All Files (*.*)|*.*";
            openFileDialog.DefaultExt = "ain";
            this.Update();
            if (openFileDialog.ShowDialogWithTopic(DialogTopic.LoadAinFile) == DialogResult.OK)
            {
                this.Update();
                try
                {
                    this.UseWaitCursor = true;
                    OpenFile(openFileDialog.FileName);
                }
                finally
                {
                    this.UseWaitCursor = false;
                }
            }
        }

        private void OpenFile(string fileName)
        {
            this.ainFile = new AinFile();
            if (this.ainFile.ReadAndDecryptAinFile(fileName))
            {
                this.decompiler = new Decompiler(ainFile);

                //this.AlternativeNames = new AlternativeNames(fileName);

                MakeTreeView();
                ClearTabs();
                RecentFilesList.FilesList.Add(fileName);
            }
            else
            {
                RecentFilesList.FilesList.Remove(fileName);
            }
            this.originalAinFile = this.ainFile;
        }

        private void findFunctionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var functionSearchForm = new FunctionSearchForm2(this, SearchKind.Function, -1);
            functionSearchForm.Show();
        }

        private void replaceAllNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReplaceVariableNames();
            MakeTreeView();
            ClearTabs();
        }

        private void toolsToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            debugCommandToolStripMenuItem.Visible = Debugger.IsAttached;
            bool enabled = true;
            if (this.ainFile == null)
            {
                enabled = false;
            }
            foreach (ToolStripItem item in toolsToolStripMenuItem.DropDownItems)
            {
                item.Enabled = enabled;
            }
            if (this.ainFile != null)
            {
                if (ainFile.AlternativeNames != null && ainFile.AlternativeNames.Count > 0)
                {
                    this.replaceAllNamesToolStripMenuItem.Enabled = true;
                    this.translateVariableNamesToolStripMenuItem.Enabled = false;
                }
                else
                {
                    this.replaceAllNamesToolStripMenuItem.Enabled = false;
                    this.translateVariableNamesToolStripMenuItem.Enabled = true;
                }

            }
            debugCommandToolStripMenuItem.Enabled = Debugger.IsAttached;
        }

        ScintillaNET.Scintilla GetFocusedControl()
        {
            if (DecompiledCodeTextBox.Focused == true)
            {
                return DecompiledCodeTextBox;
            }
            if (DisassembledCodeTextBox.Focused == true)
            {
                return DisassembledCodeTextBox;
            }
            if (CombinedViewTextBox.Focused == true)
            {
                return CombinedViewTextBox;
            }
            return null;
        }

        private void editToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var control = GetFocusedControl();
            bool enabled = true;

            if (control == null)
            {
                enabled = false;
            }
            editToolStripMenuItem.DropDownItems.OfType<ToolStripItem>().ForEach((i) => i.Enabled = enabled);
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var control = GetFocusedControl();
            if (control != null)
            {
                control.Selection.SelectAll();
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var control = GetFocusedControl();
            if (control != null)
            {
                control.Clipboard.Copy();
            }
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Update();
            ExportFiles();
        }

        private void ExportFiles()
        {
            var codeDisplayOptions = GetCodeDisplayOptions();
            var codeExporter = new CodeExporter(ainFile, codeDisplayOptions);
            if (ainFile.GetVariablesWithUnknownFunctypes().FirstOrDefault() != null)
            {
                string oldText = this.Text;
                this.Text = "Finding Function/Delegate Types... may take up to a few minutes...";
                this.Refresh();
                ainFile.FindFunctionTypes();
                this.Text = oldText;
            }
            codeExporter.ExportFiles();
        }

        private void findFunctionsToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.Function);
        }

        private void ShowSearchForm(SearchKind searchKind)
        {
            int functionNumber = -1;
            if (this.codeDisplayState != null && this.codeDisplayState.Function != null)
            {
                functionNumber = this.codeDisplayState.Function.Index;
            }
            if (searchKind == SearchKind.Local && functionNumber == -1)
            {
                return;
            }
            var functionSearchForm = new FunctionSearchForm2(this, searchKind, functionNumber);
            functionSearchForm.Show();
        }

        private void findGlobalsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.Global);
        }

        private void findClassStructMembersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.StructMember);
        }

        private void findStringsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.String);
        }

        private void findMessagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.Message);
        }

        private void findIntsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.Int);
        }

        private void findLocalVariablesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.Local);
        }

        private void viewEditAINStructuresToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditAinStructures();
        }

        private void EditAinStructures()
        {
            Update();
            var propertyGridForm = new PropertyGridForm(this.ainFile, "View/Edit AIN Structures");
            propertyGridForm.Show();
        }

        private void saveAsProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveAsProject();
        }

        #region OpenProject and background workers

        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenProject();
        }


        private void OpenProject()
        {
            using (var options = new AssembleOptionsForm())
            {
                Update();
                if (options.ShowDialog() == DialogResult.OK)
                {
                    Update();
                    var openFileDialog = new OpenFileDialog();
                    openFileDialog.RestoreDirectory = false;
                    openFileDialog.FileName = "main.jam";
                    openFileDialog.Filter = "JAM Files (*.jam)|*.jam|All Files (*.*)|*.*";
                    openFileDialog.DefaultExt = "jam";
                    if (openFileDialog.ShowDialogWithTopic(DialogTopic.AssembleCode) == DialogResult.OK)
                    {
                        Update();
                        var saveFileDialog = new SaveFileDialog();
                        saveFileDialog.RestoreDirectory = false;
                        saveFileDialog.Filter = "AIN Files (*.ain)|*.ain;*.ain_|All Files (*.*)|*.*";
                        saveFileDialog.DefaultExt = "ain";

                        if (saveFileDialog.ShowDialogWithTopic(DialogTopic.AssembleCodeSaveAin) == DialogResult.OK)
                        {
                            Update();
                            bool encryptFile = options.EncryptOutputAinCheckBox.Checked;
                            bool openAfterwards = options.OpenGeneratedFileCheckBox.Checked;

                            var buildProjectInBackground = new BuildProjectInBackground();
                            //buildProjectInBackground.ForceUniqueMessages = true;
                            bool success = buildProjectInBackground.Run(openFileDialog.FileName, saveFileDialog.FileName, encryptFile);

                            if (openAfterwards && success)
                            {
                                OpenFile(saveFileDialog.FileName);
                            }
                        }
                    }
                }
            }
        }

        public class BuildProjectInBackground : BackgroundTask
        {
            public bool ForceUniqueMessages = false;

            protected override string TitleBarText { get { return "Assembling Code..."; } }

            protected override bool AbortOnCancel { get { return false; } }

            public bool Run(string inputProjectFileName, string outputAinFileName, bool encrypt)
            {
                return base.Run(inputProjectFileName, outputAinFileName, encrypt);
            }

            protected override object DoWork(string inputFileName, string outputFileName, BackgroundTask.WorkerData workerData)
            {
                string inputProjectFileName = workerData.InputFileName;
                string outputAinFileName = workerData.OutputFileName;
                bool encrypt = (workerData.ExtraData as bool?) == true;

                AssemblerProjectReader reader = new AssemblerProjectReader();
                reader.ForceUniqueMessages = this.ForceUniqueMessages;
                reader.worker = workerData.BackgroundWorker;
                bool result = reader.LoadProject(inputProjectFileName);
                if (workerData.BackgroundWorker.CancellationPending || result == false)
                {
                    return false;
                }

                var ainFile = reader.MakeAinFile();
                Compiler.Compiler.RemoveFunctypes(ainFile);
                if (encrypt)
                {
                    ainFile.WriteAndEncryptAinFile(outputAinFileName);
                }
                else
                {
                    ainFile.WriteAinFile(outputAinFileName);
                }

                return true;
            }
        }

        #endregion

        #region SaveProject and Background Workers

        private void SaveAsProject()
        {
            Update();
            if (this.originalAinFile == null)
            {
                return;
            }

            using (var optionsDialog = new DisassemblyOptionsForm())
            {
                if (optionsDialog.ShowDialog() == DialogResult.OK)
                {
                    Update();
                    bool singleFile = optionsDialog.ExportToSingleFileRadioButton.Checked;
                    var options = GetCodeDisplayOptions();
                    options.ForceOriginalStringOrder = optionsDialog.ForceOrderOfStringsToBePreservedRadioButton.Checked;
                    options.MergeDuplicateStrings = optionsDialog.MergeAllDuplicateTextRadioButton.Checked;
                    options.MergeDuplicateMessages = optionsDialog.MergeAllDuplicateTextRadioButton.Checked;
                    options.RemoveOrphanFunctions = optionsDialog.RemoveLeftoversFromEarilerTranslationProjectsRadioButton.Checked;
                    options.AnnotateWithDecompiledCode = optionsDialog.AnnotateWithDecompiledSourceCodeRadioButton.Checked;

                    var saveFileDialog = new SaveFileDialog();
                    saveFileDialog.Filter = "JAM Files (*.jam)|*.jam|All Files (*.*)|*.*";
                    saveFileDialog.DefaultExt = "jam";
                    saveFileDialog.RestoreDirectory = true;
                    if (singleFile)
                    {
                        saveFileDialog.FileName = "main.jam";
                    }
                    else
                    {
                        saveFileDialog.Title = "Pick an empty directory to create a project";
                        saveFileDialog.FileName = "main.jam";
                    }

                    if (saveFileDialog.ShowDialogWithTopic(DialogTopic.DisassembleCode) == DialogResult.OK)
                    {
                        Update();
                        AssemblerProjectWriter writer = new AssemblerProjectWriter(originalAinFile, options);
                        //writer.AlternativeNames = this.AlternativeNames;

                        string fileName = saveFileDialog.FileName;
                        var saveProjectInBackground = new SaveProjectInBackground();
                        saveProjectInBackground.SaveAsProject(writer, fileName, singleFile);
                    }
                }
            }
        }

        //public class SaveProjectInBackground2 : BackgroundTask
        //{
        //    protected override string TitleBarText { get { return "Disassembling Code..."; } }

        //    protected override bool AbortOnCancel { get { return false; } }

        //    public bool Run(string inputProjectFileName, string outputAinFileName, bool encrypt)
        //    {
        //        return base.Run(inputProjectFileName, outputAinFileName, encrypt);
        //    }

        //    protected override object DoWork(string inputFileName, string outputFileName, BackgroundTask.WorkerData workerData)
        //    {
        //        string inputProjectFileName = workerData.InputFileName;
        //        string outputAinFileName = workerData.OutputFileName;
        //        bool encrypt = (workerData.ExtraData as bool?) == true;

        //        AssemblerProjectReader reader = new AssemblerProjectReader();
        //        reader.worker = workerData.BackgroundWorker;
        //        bool result = reader.LoadProject(inputProjectFileName);
        //        if (workerData.BackgroundWorker.CancellationPending || result == false)
        //        {
        //            return false;
        //        }

        //        var ainFile = reader.MakeAinFile();
        //        if (encrypt)
        //        {
        //            ainFile.WriteAndEncryptAinFile(outputAinFileName);
        //        }
        //        else
        //        {
        //            ainFile.WriteAinFile(outputAinFileName);
        //        }

        //        return true;
        //    }
        //}

        public class SaveProjectInBackground
        {
            ProgressForm progressForm;

            class SaveProjectWorkerData
            {
                public string fileName;
                public AssemblerProjectWriter writer;
                public bool singleFile;
                public MyBackgroundWorker worker;
            }

            MyBackgroundWorker saveProjectWorker = null;

            public void SaveAsProject(AssemblerProjectWriter writer, string fileName, bool singleFile)
            {
                saveProjectWorker = new MyBackgroundWorker();
                saveProjectWorker.WorkerReportsProgress = true;
                saveProjectWorker.WorkerSupportsCancellation = true;
                saveProjectWorker.DoWork += new DoWorkEventHandler(saveProjectWorker_DoWork);
                saveProjectWorker.ProgressChanged += new ProgressChangedEventHandler(saveProjectWorker_ProgressChanged);
                saveProjectWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(saveProjectWorker_RunWorkerCompleted);

                var data = new SaveProjectWorkerData() { fileName = fileName, singleFile = singleFile, worker = saveProjectWorker, writer = writer };

                progressForm = new ProgressForm();
                progressForm.Text = "Disassembling Code...";
                progressForm.CancelButtonPressed += new EventHandler(progressForm_CancelButtonPressed2);

                saveProjectWorker.RunWorkerAsync(data);
                var dialogResult = progressForm.ShowDialog();

                progressForm.Dispose();

            }

            void progressForm_CancelButtonPressed2(object sender, EventArgs e)
            {
                if (saveProjectWorker != null & !saveProjectWorker.CancellationPending)
                {
                    saveProjectWorker.CancelAsync();
                }
            }

            void saveProjectWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
            {
                if (progressForm != null && !progressForm.IsDisposed)
                {
                    if (e.Cancelled == false)
                    {
                        progressForm.OkayToClose = true;
                        progressForm.DialogResult = DialogResult.OK;
                        progressForm.Close();
                    }
                    else
                    {
                        progressForm.OkayToClose = true;
                        progressForm.DialogResult = DialogResult.Cancel;
                        progressForm.Close();
                    }
                }
            }

            void saveProjectWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
            {
                if (progressForm != null && !progressForm.IsDisposed)
                {
                    progressForm.SetProgress(e.ProgressPercentage);
                }
            }

            void saveProjectWorker_DoWork(object sender, DoWorkEventArgs e)
            {
                var data = e.Argument as SaveProjectWorkerData;
                SaveAsProject(data);
            }

            private void SaveAsProject(SaveProjectWorkerData data)
            {
                var fileName = data.fileName;
                var writer = data.writer;
                var singleFile = data.singleFile;
                var worker = data.worker;

                writer.worker = worker;
                if (singleFile)
                {
                    writer.SaveAsProject(fileName, true);
                }
                else
                {
                    writer.SaveAsProject(Path.GetDirectoryName(fileName), false);
                }
            }

            public void Wait()
            {
                while (saveProjectWorker.IsBusy)
                {
                    Thread.Sleep(10);
                    Application.DoEvents();
                }
            }
        }

        #endregion

        private void fileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            var items = RecentFilesList.FilesList.GetList();
            recentFilesToolStripMenuItem.DropDownItems.Clear();
            foreach (var fileName in items)
            {
                var newItem = recentFilesToolStripMenuItem.DropDownItems.Add(fileName);
                newItem.Tag = fileName;
                newItem.Click += new EventHandler(newItem_Click);
            }
            recentFilesToolStripMenuItem.Enabled = recentFilesToolStripMenuItem.DropDownItems.Count > 0;

            if (this.ainFile == null)
            {
                saveAsProjectToolStripMenuItem.Enabled = false;
                exportToolStripMenuItem.Enabled = false;
                quickCompileCodePatchToolStripMenuItem.Enabled = false;
            }
            else
            {
                saveAsProjectToolStripMenuItem.Enabled = true;
                exportToolStripMenuItem.Enabled = true;
                quickCompileCodePatchToolStripMenuItem.Enabled = true;
            }
        }

        void newItem_Click(object sender, EventArgs e)
        {
            var toolStripItem = sender as ToolStripItem;
            if (toolStripItem != null)
            {
                string fileName = toolStripItem.Tag as string;
                if (fileName != null)
                {
                    OpenFile(fileName);
                }
            }
        }

        private void ExplorerForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null)
                {
                    OpenFile(files[0]);
                }
            }
        }

        private void ExplorerForm_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void findTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var activeContol = GetFocusedControl();
            if (activeContol != null)
            {
                activeContol.FindReplace.ShowFind();
            }
        }

        #endregion

        private void goToLineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var activeContol = GetFocusedControl();
            if (activeContol != null)
            {
                activeContol.GoTo.ShowGoToDialog();
            }
        }

        private void showFindToolbarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var activeContol = GetFocusedControl();
            if (activeContol != null)
            {
                activeContol.FindReplace.IncrementalSearch();
            }
        }

        private void translateVariableNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var translateVariableNamesForm = new TranslateVariableNamesForm(this.ainFile))
            {
                if (translateVariableNamesForm.ShowDialog() == DialogResult.OK)
                {
                    ainFile.AlternativeNames.LoadFile(ainFile.OriginalFilename);
                }



            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var aboutForm = new AboutForm())
            {
                aboutForm.ShowDialog();
            }
        }

        private void decompiledToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectionTabControl.SelectedIndex = 0;
        }

        private void disassembledToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectionTabControl.SelectedIndex = 1;
        }

        private void combinedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectionTabControl.SelectedIndex = 2;
        }

        private void nextTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int currentTabIndex = ExplorerTabControl.SelectedIndex;
            int tabIndex = currentTabIndex + 1;
            if (tabIndex >= ExplorerTabControl.TabCount)
            {
                tabIndex = 0;
            }
            ExplorerTabControl.SelectedIndex = tabIndex;
        }

        private void previousTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int currentTabIndex = ExplorerTabControl.SelectedIndex;
            int tabIndex = currentTabIndex - 1;
            if (tabIndex < 0)
            {
                tabIndex = ExplorerTabControl.TabCount - 1;
            }
            ExplorerTabControl.SelectedIndex = tabIndex;
        }

        private void TreeView_MouseDown(object sender, MouseEventArgs e)
        {
            var oldSelectedNode = TreeView.SelectedNode;

            if (e.Button == MouseButtons.Middle)
            {
                NewTab();
                var control = sender as Control;
                var screenPoint = control.PointToScreen(e.Location);
                NativeMethods.SimulateMouseLeftClick(screenPoint.X, screenPoint.Y);
                if (TreeView.SelectedNode == oldSelectedNode)
                {
                    treeView1_AfterSelect(sender, new TreeViewEventArgs(oldSelectedNode, TreeViewAction.ByMouse));
                }
            }
        }

        public void GoToCharacterPosition(int position)
        {
            SelectionTabControl.SelectedIndex = 0;
            DecompiledCodeTextBox.CurrentPos = position;
            this.Activate();
            DecompiledCodeTextBox.Focus();
        }

        private void exportImportStringsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var form = new ExportImportStringsForm(ainFile))
            {
                form.ShowDialog();
            }
        }

        //private void wordwrapAndSaveAINToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    WordWrapAndSaveAinFile();
        //}

        //private void WordWrapAndSaveAinFile()
        //{
        //    var wordWrapOptions = WordWrapOptionsOld.GetWordWrapOptions(ainFile);

        //    using (var wordWrapForm = new WordWrapForm())
        //    {
        //        wordWrapForm.WordWrapOptions = wordWrapOptions;
        //        var dialogResult = wordWrapForm.ShowDialog();
        //        if (dialogResult == DialogResult.OK)
        //        {
        //            using (var saveFileDialog = new SaveFileDialog())
        //            {
        //                saveFileDialog.Filter = "Ain File (*.ain)|*.ain;*.ain_|All Files (*.*)|*.*";
        //                saveFileDialog.DefaultExt = "ain";
        //                if (saveFileDialog.ShowDialogWithTopic(DialogTopic.ImportTextSaveAin) == DialogResult.OK)
        //                {
        //                    var wordWrapper = new WordWrapperOld(this.ainFile, wordWrapOptions);
        //                    wordWrapper.WordWrap(saveFileDialog.FileName);
        //                }
        //            }
        //        }
        //        if (dialogResult == DialogResult.OK || dialogResult == DialogResult.Yes)
        //        {
        //            wordWrapOptions.SaveToRegistry(ainFile);
        //        }
        //    }
        //}

        private string GetGeneratedCode(string functionName)
        {
            StringBuilder sb = new StringBuilder();
            if (ainFile.FunctionNameToIndex.ContainsKey(functionName))
            {
                Function function = ainFile.Functions[ainFile.FunctionNameToIndex[functionName]];
                if (function.ParameterCount > 0)
                {
                    for (int i = 0; i < function.ParameterCount; i++)
                    {
                        var arg = function.Parameters[i];
                        if (arg.DataType == DataType.Int)
                        {
                            sb.AppendLine("\t" + "PUSH 0");
                        }
                        else if (arg.DataType == DataType.String)
                        {
                            sb.AppendLine("\t" + "S_PUSH \"\"");
                        }
                        else
                        {
                            sb.AppendLine("\t" + "PUSH 0");
                        }
                    }
                }
            }
            sb.Append("\t" + "CALLFUNC " + functionName);
            return sb.ToString();
        }

        private void ExplorerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!PromptToSaveEditors())
            {
                e.Cancel = true;
                return;
            }

            SaveCodeDisplayOptions();
            RegistryUtility.SaveSetting("UseModifiedShiftJis", useModifiedShiftJISEncodingForEuropeanTextToolStripMenuItem.Checked);
            RegistryUtility.SaveSetting("UseModifiedShiftJis2", useModifiedShiftJISEncodingForAccentedTextNEWToolStripMenuItem.Checked);
            RegistryUtility.SaveSetting("ZlibCompressionLevel", (int)(AinFile.DefaultCompressionLevel));
        }

        private void findLibraryCallsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.LibraryCalls);
        }

        private void exportImportTextnewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var exportImportForm = new ExportImportTextNewForm(ainFile))
            {
                exportImportForm.ShowDialog();
            }
        }

        private void findSystemCallsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.SystemCalls);
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoRefresh();
        }

        private void GoRefresh()
        {
            int currentTab = currentTabIndex;
            int index = HistoryIndex.GetOrDefault(currentTab, 0);
            GoToHistoryItem(currentTab, index);
        }

        private Function CurrentFunction
        {
            get
            {
                if (this.codeDisplayState != null)
                {
                    return this.codeDisplayState.Function;
                }
                return null;
            }
        }

        #region TEST/DEBUG CODE

        bool ContainsHalfwidthCharacters(string str)
        {
            foreach (var c in str)
            {
                if (c >= 0xFF61 && c <= 0xFFDF)
                {
                    return true;
                }
            }
            return false;
        }

        private void debugCommandToolStripMenuItem_Click(object sender, EventArgs eventArgs)
        {
            DebugCommand();
        }

        void VoiceToVoice2()
        {
            int voiceFunctionNumber = ainFile.GetFunction("VOICE").Index;

            List<List<int>> voiceNumbersByFunction = new List<List<int>>();
            foreach (var function in this.ainFile.Functions)
            {
                int startAddress = function.Address;
                int endAddress = Decompiler.FindEndAddress(startAddress, ainFile);
                InstructionInfo ins;

                int previousVoiceNumber = -1;

                List<int> VoiceNumbers = voiceNumbersByFunction.GetOrNew(function.Index);
                voiceNumbersByFunction.SetOrAdd(function.Index, VoiceNumbers);

                int address = startAddress;
                while (address < endAddress)
                {
                    ins = ainFile.Peek(address);
                    if (ins.instruction == Instruction.CALLFUNC && ins.word1 == voiceFunctionNumber)
                    {
                        var previousIns = ainFile.Peek(ins.CurrentAddress - 6);
                        if (previousIns.instruction == Instruction.PUSH)
                        {
                            int currentVoiceNumber = previousIns.word1;
                            if (currentVoiceNumber >= 0)
                            {
                                VoiceNumbers.Add(currentVoiceNumber);
                            }
                            previousVoiceNumber = currentVoiceNumber;
                        }
                    }


                    address = ins.nextAddress;
                }

            }

            var writer = new AssemblerProjectWriter();
            writer.Options.ForceOriginalStringOrder = true;
            writer.Options.MergeDuplicateMessages = false;
            writer.Options.MergeDuplicateStrings = false;
            writer.Options.UseEnumerations = false;
            writer.ReadAinFile(this.ainFile);



            int currentFunctionIndex = 0;
            int currentVoiceIndex = 0;
            Action<object, InstructionInfoEventArgs> handler = (sender, e) =>
            {
                if (e.InstructionInfo.instruction == Instruction.FUNC)
                {
                    currentFunctionIndex = e.InstructionInfo.word1;
                    currentVoiceIndex = 0;
                }
                if (e.InstructionInfo.instruction == Instruction.CALLFUNC && e.InstructionInfo.word1 == voiceFunctionNumber)
                {
                    var previousInstruction = ainFile.Peek(e.InstructionInfo.CurrentAddress - 6);
                    if (previousInstruction.instruction == Instruction.PUSH && previousInstruction.word1 > 0)
                    {
                        var list = voiceNumbersByFunction.GetOrNull(currentFunctionIndex);
                        int nextVoice = -1;
                        int currentVoice = -1;
                        if (list != null)
                        {
                            nextVoice = list.GetOrDefault(currentVoiceIndex + 1, -1);
                            currentVoice = list.GetOrDefault(currentVoiceIndex, -1);
                        }

                        if (currentVoice == previousInstruction.word1)

                            if (nextVoice != -1)
                            {
                                e.WriteLine(" PUSH " + nextVoice.ToString());
                                e.WriteLine(" CALLFUNC VOICE2");
                                e.Handled = true;
                            }

                    }
                    else
                    {

                    }

                    currentVoiceIndex++;
                }
            };


            writer.BeforeWritingInstruction += new EventHandler<InstructionInfoEventArgs>(handler);

            var temporaryFile = new TemporaryFile("jam", true);
            writer.SaveAsProject(temporaryFile.FileName, true);

            var reader = new AssemblerProjectReader();
            reader.LoadProject(temporaryFile.FileName);
            var newAinFile = reader.MakeAinFile();
            newAinFile.WriteAndEncryptAinFile("C:\\alicesoft\\haruka\\DISASSEMBLED\\output.ain");
            temporaryFile.Dispose();
        }


        void DebugCommand()
        {
            int charCount = 0;
            foreach (var message in ainFile.Messages)
            {
                charCount += message.Length;
            }


            return;
            VoiceToVoice2();
            return;

            //check for unused global variables
            List<Global> unusedGlobals = new List<Global>();

            foreach (var global in ainFile.Globals)
            {
                var usedBy = this.ainFile.CodeSearchCache.GlobalsCache.GetUsedBy(global);
                if (usedBy.Count == 0)
                {
                    var initialValue = global.GetDefaultValue();
                    if (initialValue == null)
                    {
                        unusedGlobals.Add(global);
                    }
                }
            }

            return;

            foreach (var function in ainFile.Functions)
            {
                int hash = ainFile.ComputeFunctionHash(function);
                if (hash == function.Crc)
                {

                }
                else
                {

                }
            }
            return;
            HashSet<int> InstructionsUsed = new HashSet<int>();

            foreach (var function in ainFile.Functions)
            {
                int startAddress = function.Address - 6;
                int endAddress = function.GetEndAddress();
                int address = startAddress;

                while (address < endAddress)
                {
                    var ins = ainFile.Peek(address);
                    InstructionsUsed.Add((int)ins.instruction);

                    address = ins.nextAddress;
                }
            }

            List<Instruction> instructions = new List<Instruction>();
            foreach (var insins in InstructionsUsed.OrderBy())
            {
                instructions.Add((Instruction)insins);
            }



            return;

            //check for narrow japanese characters
            StringBuilder sb = new StringBuilder();

            foreach (var str in ainFile.Strings)
            {
                if (ContainsHalfwidthCharacters(str))
                {
                    sb.AppendLine(str);
                }
            }
            foreach (var msg in ainFile.Messages)
            {
                if (ContainsHalfwidthCharacters(msg))
                {
                    sb.AppendLine(msg);
                }
            }

            string result = sb.ToString();

            var functionsWithMessages = ainFile.Functions.Where(f => ainFile.CodeSearchCache.MessagesCache.GetUses(f).Count > 0).ToArray();



            return;

            var questFunctions = ainFile.Functions.Where(f => f.Name.StartsWith("QUEST")).OrderBy(n => Convert.ToInt32(n.Name.Substring(5))).ToArray();
            //StringBuilder sb = new StringBuilder();

            StringBuilder sb2 = new StringBuilder();

            foreach (var func in questFunctions)
            {
                var uses = ainFile.CodeSearchCache.FunctionsCache.GetUses(func);

                sb.AppendLine(func.Name);
                foreach (var funcused in uses)
                {
                    bool hasMessages = ainFile.CodeSearchCache.MessagesCache.GetUses(funcused).Count > 0;
                    if (hasMessages)
                    {
                        sb.AppendLine("\t" + funcused.Name);
                    }
                    else
                    {
                        sb2.AppendLine(funcused.Name);
                    }
                }
            }

            Clipboard.SetText(sb.ToString());

            return;
            {
                var f = ainFile.GetFunction("_CMsgKeyWait@_DoCommand");
                var p = f.Parameters[0];

                var tracer = new VariableTracer(ainFile);
                var results = tracer.TraceVariable(p, VariableTraceMode.DirectCopiesRecursive);
                return;
            }


            ainFile.WriteAndEncryptAinFile(@"<insert filename here>");

            return;
            string filename = @"<insert filename here>";
            string outputFileName = @"<insert filename here>";
            var compiler = new Compiler.Compiler(this.ainFile, -1);
            string codeText = "#include " + AssemblerProjectWriter.EscapeAndQuoteString(filename);

            compiler.CompileCodeInPlace(codeText);
            //generate code for "DumpTextMain" function

            MemoryStream ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(ainFile.Code);

            var newFunction = ainFile.GetFunction("DumpTextMain");
            newFunction.Address = (int)(ms.Position) + 6;

            bw.Write((short)Instruction.FUNC);
            bw.Write((int)newFunction.Index);

            for (int i = 0; i < ainFile.Messages.Count; i++)
            {
                bw.Write((short)Instruction.MSG);
                bw.Write((int)i);
            }

            bw.Write((short)Instruction.RETURN);
            bw.Write((short)Instruction.ENDFUNC);
            bw.Write((int)newFunction.Index);

            ainFile.Code = ms.ToArray();
            ainFile.WriteAndEncryptAinFile(outputFileName);


            return;

            List<Function> orphans = new List<Function>();
            foreach (var func in ainFile.Functions)
            {
                var usedBy = ainFile.CodeSearchCache.FunctionsCache.GetUsedBy(func);
                if (usedBy.Count == 0)
                {
                    var usedBy2 = ainFile.CodeSearchCache.StringsCache.GetUsedBy(func.Name);
                    if (usedBy2.Count < 2)
                    {
                        if (FunctionContainsMessages(func))
                        {
                            orphans.Add(func);
                        }
                    }
                }
            }



            return;

            FilterLogTada();

            //migrate a string numbers dump to a whitelisted script dump

            string fileName = @"<insert filename here>";

            var fileProcessor = new TextFileProcessor(ainFile);
            fileProcessor.LoadTranslationFile(fileName);
            fileProcessor.DoFiles(@"<insert filename here>");
            fileProcessor.DoFile2(@"<insert filename here>");




            //ExportFunctionsContainingMessages();




            //foreach (var function in ainFile.Functions)
            //{

            //}



            //var wordWrapOptions = WordWrapOptions2.GetWordWrapOptions(ainFile);
            //if (wordWrapOptions == null) wordWrapOptions = new WordWrapOptions2();

            //var wordWrapForm2 = new WordWrapForm2(wordWrapOptions, ainFile);
            //wordWrapForm2.Show();

            ////var testCompilerForm = new TestCompilerForm(ainFile);
            ////testCompilerForm.Show();

            ////var stopwatch = new Stopwatch();
            ////stopwatch.Start();
            ////var exporter = new StringExportImport(ainFile);
            ////int g = exporter.GetFirstGlobalGroupIdNumber();
            ////exporter.GetExclusionList();
            ////MessageBox.Show("Time: " + ((double)stopwatch.ElapsedMilliseconds) / 1000.0);
        }

        private bool FunctionContainsMessages(Function func)
        {
            int funcAddress = func.Address;
            int endAddress = decompiler.FindEndAddress(funcAddress);

            int address = funcAddress;
            while (address < endAddress)
            {
                var ins = ainFile.Peek(address);
                address = ins.nextAddress;

                if (ins.instruction == Instruction.MSG)
                {
                    return true;
                }
            }
            return false;
        }

        class TextFileProcessor
        {
            AinFile ainFile;
            int firstStringNumber = -1;
            public TextFileProcessor(AinFile ainFile)
            {
                this.ainFile = ainFile;
                var stringExportImport = new StringExportImport(ainFile);
                this.firstStringNumber = stringExportImport.GetFirstStringIdNumber();

            }
            Dictionary<string, string> JapaneseToEnglish = new Dictionary<string, string>();

            public void DoFiles(string path)
            {
                var filenames = Directory.GetFiles(path, "*.txt", SearchOption.TopDirectoryOnly);
                foreach (var filename in filenames)
                {
                    DoFile(filename);
                }
            }

            public void DoFile(string filename)
            {
                //TranslateParserThingy.Translator.TranslationOptions.UseCommentCharacter = false;
                var encoding = EncodingDetector.DetectEncoding(filename);
                var lines = File.ReadAllLines(filename, encoding);
                List<string> lines2 = new List<string>();

                string outputFileName = Path.Combine(Path.GetDirectoryName(filename), "output\\" + Path.GetFileName(filename));

                for (int i = 0; i < lines.Length; i++)
                {
                    bool handled = false;
                    string originalLine = lines[i];
                    string line = originalLine;
                    string prefix = "";
                    line = line.TrimStart();
                    int indexOfSpace = line.IndexOf(' ');
                    if (indexOfSpace > 0)
                    {
                        string word1 = line.Substring(0, indexOfSpace);
                        line = line.Substring(indexOfSpace + 1);
                        indexOfSpace = line.IndexOf(' ');
                        if (indexOfSpace > 0)
                        {
                            string word2 = line.Substring(0, indexOfSpace);
                            line = line.Substring(indexOfSpace + 1);

                            prefix = word1 + " " + word2 + " ";

                            int stringNumber;
                            if (IntUtil.TryParse(word2, out stringNumber) && word1 == "s")
                            {
                                if (JapaneseToEnglish.ContainsKey(line))
                                {
                                    string newLine = JapaneseToEnglish[line];
                                    lines2.Add("#" + prefix + line);
                                    lines2.Add(prefix + newLine);
                                    handled = true;
                                }
                                else
                                {
                                    //string newLine = TranslateParserThingy.Translator.TranslateText(line);
                                    //lines2.Add("#" + prefix + line);
                                    //lines2.Add(prefix + newLine);
                                    //handled = true;
                                }
                            }
                        }
                    }
                    if (!handled)
                    {
                        lines2.Add(originalLine);
                    }
                }
                File.WriteAllLines(outputFileName, lines2.ToArray(), encoding);
            }

            public void DoFile2(string filename)
            {
                //TranslateParserThingy.Translator.TranslationOptions.UseCommentCharacter = false;
                var encoding = EncodingDetector.DetectEncoding(filename);
                var lines = File.ReadAllLines(filename, encoding);
                List<string> lines2 = new List<string>();

                string outputFileName = Path.Combine(Path.GetDirectoryName(filename), "output\\" + Path.GetFileName(filename));

                for (int i = 0; i < lines.Length; i++)
                {
                    string originalLine = lines[i];
                    string line = originalLine;
                    bool handled = false;
                    if (JapaneseToEnglish.ContainsKey(line))
                    {
                        string newLine = JapaneseToEnglish[line];
                        lines2.Add(newLine);
                        handled = true;
                    }
                    else
                    {
                        //string newLine = TranslateParserThingy.Translator.TranslateText(line);
                        //lines2.Add("#" + prefix + line);
                        //lines2.Add(prefix + newLine);
                        //handled = true;
                    }
                    if (!handled)
                    {
                        lines2.Add(originalLine);
                    }
                }
                File.WriteAllLines(outputFileName, lines2.ToArray(), encoding);
            }

            public void LoadTranslationFile(string filename)
            {
                var encoding = EncodingDetector.DetectEncoding(filename);
                var lines = File.ReadAllLines(filename, encoding);

                //Dictionary<int, string> firstString = new Dictionary<int, string>(), secondString = new Dictionary<int, string>();
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    line = line.TrimStart(' ', '#');
                    int indexOfSpace = line.IndexOf(' ');
                    int lineNumber = 0;
                    string text = null;
                    if (indexOfSpace > 0)
                    {
                        string numberString = line.Substring(0, indexOfSpace);
                        if (IntUtil.TryParse(numberString, out lineNumber))
                        {
                            text = line.Substring(indexOfSpace + 1);
                        }
                    }
                    if (lineNumber > 0 && text != null)
                    {
                        string originalText = ainFile.GetString(lineNumber - firstStringNumber);
                        if (originalText != null)
                        {
                            if (text[0] > 128 && originalText == text)
                            {


                            }
                            else
                            {
                                if (text[0] > 128)
                                {

                                }
                                JapaneseToEnglish[originalText] = text;
                            }
                        }


                        //if (!firstString.ContainsKey(lineNumber))
                        //{
                        //    firstString.Add(lineNumber, text);
                        //}
                        //else if (!secondString.ContainsKey(lineNumber))
                        //{
                        //    secondString.Add(lineNumber, text);
                        //}
                        //else
                        //{

                        //}
                    }
                }

                //foreach (var pair1 in firstString)
                //{
                //    string text1 = pair1.Value;
                //    int stringNumber = pair1.Key;
                //    if (secondString.ContainsKey(stringNumber))
                //    {
                //        string text2 = secondString[stringNumber];
                //        JapaneseToEnglish[text1] = text2;
                //    }
                //}
            }
        }

        private void ExportFunctionsContainingMessages()
        {
            //export all functions containing messages
            string outputDirectory = @"<insert filename here>";
            var enumerator = new FunctionEnumerator(ainFile);
            var results = enumerator.GetFilesAndFunctions();
            foreach (var fileNode in results)
            {
                string fileName = Path.GetFileName(fileNode.name);
                string outputFileName = Path.Combine(outputDirectory, fileName);

                TextWriter tw = null;
                FileStream fs = null;
                foreach (var functionNode in fileNode.children)
                {
                    int startAddress = functionNode.address;
                    int lastAddress = functionNode.lastAddress;
                    int address = startAddress;
                    bool wantToDecompile = false;

                    while (address < lastAddress)
                    {
                        var ins = ainFile.Peek(address);
                        address = ins.nextAddress;
                        if (ins.instruction == Instruction.MSG)
                        {
                            wantToDecompile = true;
                            break;
                        }
                    }

                    if (wantToDecompile)
                    {
                        var function = functionNode.Function;
                        if (tw == null)
                        {
                            fs = new FileStream(outputFileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                            tw = new StreamWriter(fs, Extensions.TextEncoding);
                        }

                        if (function != null)
                        {
                            var decompliledCode = ainFile.DecompiledCodeCache.GetDecompiledCodeText(function);
                            tw.WriteLine(decompliledCode);
                        }
                    }
                }

                if (tw != null && fs != null)
                {
                    tw.Flush();
                    fs.Flush();
                    tw.Close();
                    fs.Close();
                    tw.Dispose();
                    fs.Dispose();
                }
            }
        }

        private void FilterLogTada()
        {
            var logTada = ainFile.GetFunction("LOG_TADA");
            var logFunc = ainFile.GetFunction("LOG");

            var matches1 = ainFile.CodeSearchCache.FunctionsCache.GetUsedBy(logTada);
            var matches2 = ainFile.CodeSearchCache.FunctionsCache.GetUsedBy(logFunc);
            matches1.UnionWith(matches2);

            var ms = new MemoryStream(ainFile.Code);
            var bw = new BinaryWriter(ms);

            foreach (var function in matches1)
            {
                var functionExpression = ainFile.DecompiledCodeCache.GetDecompiledCode(function);
                foreach (var e in functionExpression.GetChildExpressions())
                {
                    if (e.ExpressionType == Instruction.CALLFUNC && (e.Value == logTada.Index || e.Value == logFunc.Index))
                    {
                        foreach (var e2 in e.GetChildExpressions())
                        {
                            if (e2.ExpressionType == Instruction.S_PUSH)
                            {
                                ms.Position = e2.address + 2;
                                bw.Write(0);
                            }
                        }
                    }
                }
            }
        }

        private void testCompilerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var compiler = new Compiler.Compiler();
            compiler.Test();

        }

        private void makeSengokuRanceEnumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            object[] initData = new object[]
            {       
                1106	,"●デバッグ様Ｘ",
                1000	,"●Rance",
                1001	,"●Sill",
                1003	,"●Ogawa_Kentarou",
                1002	,"●Suzume",
                1004	,"●Kentou_Kanami",
                1005	,"●Maria",
                1145	,"●Kasumi",
                1006	,"●Leila",
                1007	,"●Rizna",
                1008	,"●Uruza",
                1009	,"●Magic",
                1156	,"●Gekkou",
                1157	,"●Masou_Shizuka",
                1078	,"●Kouhime",
                1011	,"●Ranmaru",
                1010	,"●Shibata_Katsuie",
                1079	,"●Tamagushi_Fuuka",
                1116	,"●Maeda_Toshiie",
                1115	,"●Akechi_Mitsuhide",
                1114	,"●Niwa_Nagahide",
                1182	,"●Sakuma_Morimasa",
                1154	,"●Hachisuka_Muneyana",
                1155	,"●Takigawa_Kazumasu",
                1023	,"●Hara_Shouji",
                1024	,"●Akihime",
                1121	,"●Saijou_Mochikiyo",
                1025	,"●Ashikaga_Choushin",
                1027	,"●Ikkyu",
                1026	,"●Yamamoto_Isoroku",
                1122	,"●Yamaguchi_Tamon",
                1123	,"●Inoue_Shigemi",
                1030	,"●Akashi_Kazemaru",
                1031	,"●Sakamoto_Ryouma",
                1032	,"●Gon",
                1033	,"●Kawanoe_Yuzuru",
                1034	,"●Kawanoe_Mine",
                1172	,"●Shoutoku_Taishi",
                1035	,"●Mouri_Motonari",
                1036	,"●Mouri_Teru",
                1037	,"●Kikkawa_Kiku",
                1038	,"●Kobayakawa_Chinu",
                1131	,"●Iwami_Ginzan",
                1132	,"●Murakami_Suigun",
                1133	,"●Kuu_Hime",
                1073	,"●Yamanaka_Kojika",
                1039	,"●Tanegashima_Shigehiko",
                1040	,"●Yuzuhara_Yuzumi",
                1041	,"●Tokugawa_Ieyasu",
                1042	,"●Honda_Tadakatsu",
                1043	,"●Ii_Naomasa",
                1046	,"●Sakakibara_Yasumasa",
                1045	,"●Sakai_Tadatsugu",
                1044	,"●Hattori_Hanzou",
                1072	,"●Senhime",
                1048	,"●Uesugi_Kenshin",
                1049	,"●Naoe_Ai",
                1108	,"●Katsuko",
                1109	,"●Torako",
                1052	,"●Daidouji_Komatsu",
                1050	,"●Houjou_Souun",
                1051	,"●Nanjou_Ran",
                1135	,"●Houjou_Masako",
                1056	,"●Yamagata_Masakage",
                1054	,"●Baba_Shouen",
                1055	,"●Kousaka_Yoshikage",
                1053	,"●Sanada_Tourin",
                1181	,"●Yamamoto_Kansuke",
                1028	,"●Seigan",
                1029	,"●Genri",
                1047	,"●Honda_Masanobu",
                1126	,"●Miyoshi_A_Kichi",
                1127	,"●Miyoshi_B_Ko",
                1128	,"●Miyoshi_C_Zou",
                1130	,"●Riot_Abetter",
                1065	,"●Inukai",
                1066	,"●Ninja_68",
                1067	,"●Ninja_33",
                1068	,"●Ninja_14",
                1069	,"●Ninja_16",
                1070	,"●Ninja_55",
                1179	,"●Gumo_Danjou",
                1057	,"●Asakura_Yoshikage",
                1059	,"●Yukihime",
                1058	,"●Hakkin_Dasan",
                1060	,"●Okita_Nozomi",
                1071	,"●Natori",
                1149	,"●Maya",
                1150	,"●Isuzu",
                1147	,"●Goemon",
                1148	,"●Souma_Hayate",
                1169	,"●Himiko",
                1062	,"●Shimazu_Kazuhisa",
                1064	,"●Shimazu_Iehisa",
                1061	,"●Shimazu_Yoshihisa",
                1063	,"●Shimazu_Toshihisa",
                1076	,"●Agireda",
                1012	,"●Dokuganryu_Masamune",
                1013	,"●Nogiku",
                1014	,"●Orime",
                1015	,"●Noir",
                1016	,"●Omachi",
                1170	,"●Moscow_Fuyushougun",
                1017	,"●Demon_King_Miki",
                1083	,"●Shogun_Isun",
                1084	,"●Shogun_Bepcho",
                1085	,"●Shogun_Wispin",
                1074	,"●Keikoku",
                1199	,"●Steve",
                1183	,"●Monster_Commander",
                1184	,"●Monster_Commander",
                1185	,"●Monster_Commander",
                1186	,"●Monster_Commander",
                1107	,"●Infected",
                1139	,"●Natsumi_Papa",
                1167	,"●Ouka_Toki",
                1201	,"●Elina",
                1202	,"●Turban",
                1203	,"●Kurosaki_Ichiya",
                1204	,"●Morita_Ai",
                1205	,"●Makutsudou_Nobuhiko",
                1206	,"●Hasuma_Kanae",
                1207	,"●Takakura_Kamei",
                1176	,"●Akiyama_Yoshifuru",
                1158	,"●Ganko_Ittetsu",
                1159	,"●Andou_Kyuuri",
                1160	,"●Takuwan_Sama",
                1198	,"●Aburako_Dousan",
                1161	,"●Kikan_Jyuutarou",
                1162	,"●Saika_Mako",
                1163	,"●Kuge_Kiyoko",
                1164	,"●Kuge_Kyouko",
                1165	,"●Haniwa_Nyorai",
                1177	,"●Settoku_Houshi",
                1193	,"●Gathered_Foot_Soldiers",
                1192	,"●Kopa_Mercenaries",
                1195	,"●Bodyguard",
                1187	,"●Hara_Family_Commander",
                1188	,"●Tokugawa_Family_Commander",
                1189	,"●Iga_Commander",
                1194	,"●Uesugi_Commander",
                1190	,"●Ashikaga_Commander",
                1200	,"●Asakura_Commander",
                1191	,"●Volunteer_Army_Commander",
                1118	,"●Mikasashu",
                1120	,"●Mikasashu",
                1119	,"●Mikasashu",
                1110	,"●Itou_Kakka",
                1111	,"●Itagaki_Choukan",
                1112	,"●Ooshio_Heihachirou",
                1101	,"●Botan",
                1102	,"●Headfish",
                1103	,"●Long_Armed_Ape",
                1104	,"●Panda",
                36  ,"●Golden_Bear",
                1087	,"●Blue_Ogre",
                1086	,"●Red_Ogre",
                1197	,"●Assassin",
                1141	,"●Kumano_Gorou",
                1142	,"●Suzutani_Musou",
                1018	,"●Demon_Nobunaga",
                1098	,"●Demon_Nobunaga",
                1099	,"●Demon_Xavier",
                1088	,"●Toukichirou",
                1096	,"●Sekimei",
                1021	,"●Apostle_Madou",
                1090	,"●Genbu",
                1022	,"●Apostle_Gigai",
                1019	,"●Apostle_Rengoku",
                1089	,"●Byakko",
                1020	,"●Apostle_Shikibu",
                1093	,"●Orochi",
                1094	,"●Orochi_Girl",
                1095	,"●Sakanoueno_Tamuramaro",
                1233	,"●Daidaara",
                1234	,"●Youkai_Namename",
                1235	,"●Giant_Raccoon_Dog",
                1236	,"●Front_Ogre",
                1237	,"●Back_Ogre",
                1196	,"●Doggie",
                1238	,"●Mazone",
                1239	,"●Torabanbara",
                1240	,"●Neutron_Hanny",
                1241	,"●Gunagan",
                1208	,"●Freedom",
                1209	,"●Uppi",
                1210	,"●Magball",
                1211	,"●Pressure_Master",
                1212	,"●Rommel",
                1213	,"●Nyo",
                1214	,"●Squid_Man",
                1215	,"●Yankee",
                1216	,"●Mage_Man",
                1217	,"●Zombie",
                1218	,"●Bebetar",
                1219	,"●Kurome",
                1220	,"●Hanny",
                1221	,"●Pig_Banbara",
                1222	,"●Akame",
                1223	,"●Jabara",
                1224	,"●Conturtle",
                1225	,"●Samerai",
                1242	,"●Pinky",
                1226	,"●King_Squid_Man",
                1227	,"●Himler",
                1228	,"●S_Hanny",
                1229	,"●Okayu_Fever",
                1230	,"●Caterpillar_DX",
                1231	,"●Dekant",
                1232	,"●Maou",
             };

            var enumerationType = ainFile.MetadataFile.EnumerationTypes.GetOrAddNew("CharacterName");
            enumerationType.Name = "CharacterName";

            for (int i = 0; i < initData.Length; i += 2)
            {
                int value = (int)initData[i];
                string name = (string)initData[i + 1];
                enumerationType.Set(value, name);
            }

            var function = ainFile.Functions[0x42F];
            var argument = function.Parameters[2];
            ainFile.MetadataFile.Metadata.GetOrAddNew(argument).EnumerationType = enumerationType;
            ainFile.SaveMetadata();


        }

        private void testtracerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var tracer = new VariableTracer(ainFile);
            var variable = ainFile.Functions[0x430].Parameters[5];

            var results = tracer.TraceVariable(variable, VariableTraceMode.Any);

        }

        private void testdefaultArgumentFinderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var function = ainFile.Functions[0x11A2];
            var argument = function.Parameters[1];
            var defaultArgumentFinder = new DefaultArgumentFinder(ainFile);
            var results = defaultArgumentFinder.FindArgumentValuesInt(argument);

        }

        private void testSpeedOfStringExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var exporter = new StringExportImport(ainFile);
            int g = exporter.GetFirstGlobalGroupIdNumber();
            exporter.GetExclusionList();
            MessageBox.Show("Time: " + ((double)stopwatch.ElapsedMilliseconds) / 1000.0);

        }

        int testSwitchCaseNumber = -1;
        private void testCodegeneratorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var currentFunction = this.CurrentFunction;
            if (currentFunction != null)
            {
                int funcAddress = currentFunction.Address - 6;
                var decompiler2 = new Decompiler(ainFile);

                var codeExpression = decompiler2.DecompileFunction(currentFunction);
                var ainCopy = ainFile.Clone();
                ainCopy.Code = (byte[])ainFile.Code.Clone();

                if (testSwitchCaseNumber >= 0)
                {
                    ainCopy.Switches.Clear();
                    for (int i = 0; i < testSwitchCaseNumber; i++)
                    {
                        ainCopy.Switches.Add(new SwitchBlock());
                    }
                }

                if (codeExpression != null)
                {
                    foreach (var expression in codeExpression.GetChildExpressions())
                    {
                        expression.ainFile = ainCopy;
                    }
                }

                var codeGenerator = new Compiler.CodeGenerator(ainCopy);
                codeGenerator.Address = funcAddress;
                codeGenerator.GenerateCodeForFunction(codeExpression);
                var bw = new BinaryWriter(codeGenerator.Code);
                bw.Write((short)0x61);  //FUNC
                bw.Write(-1);           //-1  (to stop it from bleeding into an adjacent function)

                if (codeGenerator.Code.Length + funcAddress > ainCopy.Code.Length)
                {
                    int padAmount = (int)codeGenerator.Code.Length + funcAddress - ainCopy.Code.Length;
                    ainCopy.Code = ainCopy.Code.Concat(Enumerable.Repeat((byte)0, padAmount)).ToArray();
                }
                codeGenerator.Code.ToArray().CopyTo(ainCopy.Code, funcAddress);

                var decompiler = new Decompiler(ainCopy);
                int endAddress = decompiler.FindEndAddress(funcAddress + 6);
                var disassembler = new DisassemblerForListFile(ainCopy);
                string disassembledCodeText = disassembler.DisassembleListFileRange(funcAddress, endAddress);
                DisassembledCodeTextBox.Text = disassembledCodeText;

                var decompiledCode = decompiler.DecompileFunction(funcAddress + 6);
                var expressionDisplayer = new ExpressionDisplayer(ainCopy, currentFunction.Index);
                string decompiledCodeText = expressionDisplayer.PrintExpression2(decompiledCode);
                var expressionMap = expressionDisplayer.expressionMap;

                var codeDisplayOptions = GetCodeDisplayOptions();

                var writer = new AssemblerProjectWriter(ainCopy, codeDisplayOptions);
                var lines = decompiledCodeText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                string combinedText = writer.GetCombinedFunction(funcAddress + 6, lines, expressionMap.GetAddressPerLineNumber());

                this.DecompiledCodeTextBox.Text = decompiledCodeText;
                this.DisassembledCodeTextBox.Text = disassembledCodeText;
                this.CombinedViewTextBox.Text = combinedText;

                if (testSwitchCaseNumber >= 0)
                {
                    testSwitchCaseNumber = ainCopy.Switches.Count;
                }
            }
        }

        private void testCodeGeneratorallFunctionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            testSwitchCaseNumber = 0;
            foreach (var function in ainFile.Functions)
            {
                VisitFunction(function, false);
                string oldDisassembly = DisassembledCodeTextBox.Text;
                testCodegeneratorToolStripMenuItem_Click(sender, e);
                string newDisassembly = DisassembledCodeTextBox.Text;
                if (oldDisassembly != newDisassembly)
                {

                }
            }
            testSwitchCaseNumber = -1;
        }

        private void testCodeGeneratorTobuildNewAINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string outputFileName;
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "AIN Files (*.ain)|*.ain|All Files (*.*)|*.*";
                if (saveFileDialog.ShowDialogEx() == DialogResult.OK)
                {
                    outputFileName = saveFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }

            var result = RebuildAinFileWithCodeGenerator(this.ainFile, false);
            result.WriteAndEncryptAinFile(outputFileName);
        }

        private AinFile RebuildAinFileWithCodeGenerator(AinFile ainFile, bool usePeephole)
        {
            var ainCopy = ainFile.Clone();
            AinFile oldDefaultAinFile = Expression.defaultAinFile;
            try
            {
                Expression.defaultAinFile = ainCopy;
                ainCopy.Switches.Clear();
                var codeGenerator = new Compiler.CodeGenerator(ainCopy);
                var decompiler = new Decompiler(ainFile);
                var bw = new BinaryWriter(codeGenerator.Code);

                var compiler = new Compiler.Compiler(ainCopy, 0);

                var fe = new FunctionEnumerator(ainFile);
                var fes = fe.GetFilesAndFunctions();
                foreach (var fil in fes)
                {
                    foreach (var fun in fil.children)
                    {
                        var function = ainFile.GetFunction(fun.id);
                        if (function != null)
                        {
                            var codeExpression = decompiler.DecompileFunction(function);
                            if (codeExpression != null)
                            {
                                foreach (var expression in codeExpression.GetChildExpressions())
                                {
                                    expression.ainFile = ainCopy;
                                }
                            }
                            if (codeExpression == null || codeExpression.ExpressionType == Instruction.Nop)
                            {
                                codeExpression = new Expression(Instruction.FUNC, 0, function.Index);
                                codeExpression.ainFile = ainCopy;
                            }

                            int address = (int)codeGenerator.Code.Position;
                            var otherFunction = function.Canonicalize(ainCopy) as Function;
                            if (otherFunction != null)
                            {
                                otherFunction.Address = address + 6;
                            }

                            if (usePeephole)
                            {
                                if (ainCopy.Version == 6 && !ainCopy.UseSimplifiedCode)
                                {
                                    Compiler.Compiler.PeepholeV6Function(codeExpression);
                                }
                                PeepholeForRebuild(codeExpression);
                            }

                            codeGenerator.GenerateCodeForFunction(codeExpression);
                            //}
                        }
                    }
                    bw.Write((Int16)Instruction.EOF);
                    bw.Write((int)fil.id);
                    codeGenerator.Address += 6;
                }
                ainCopy.Code = codeGenerator.Code.ToArray();
                foreach (var switchBlock in ainCopy.Switches)
                {
                    switchBlock.SwitchCases.Sort();
                }

            }
            finally
            {
                Expression.defaultAinFile = oldDefaultAinFile;
            }
            return ainCopy;
        }

        private bool PeepholeForRebuild(Expression baseExpression)
        {
        restart:
            ;
            if (baseExpression == null) return false;
            if (baseExpression.ExpressionType == Instruction.Statement &&
                baseExpression.Args.Count == 2 &&
                (baseExpression.Arg2 ?? Expression.Empty).ExpressionType == Instruction.Statement &&
                (baseExpression.Arg1 ?? Expression.Empty).ExpressionType != Instruction.Statement)
            {
                var lastParent = baseExpression.Parent;
                bool result;
                result = PeepholeForRebuild(baseExpression.Arg1);
                if (result)
                {
                    baseExpression = lastParent;
                    goto restart;
                }
                baseExpression = baseExpression.Arg2;
                goto restart;
            }
            else
            {
                for (int i = 0; i < baseExpression.Args.Count; i++)
                {
                    var expr = baseExpression.Args[i];
                    var lastParent = baseExpression.Parent;
                    bool result;
                    result = PeepholeForRebuild(expr);
                    if (result)
                    {
                        baseExpression = lastParent;
                        goto restart;
                    }
                }
            }

            if (baseExpression.ExpressionType == Instruction.Statement)
            {
                return false;
            }

            var ainFile = baseExpression.ainFile;

            var expression = baseExpression;
            if (expression.ExpressionType == Instruction.CALLMETHOD)
            {
                var function = ainFile.GetFunction(expression.Value);
                if (function != null && function.Name.EndsWith("@2"))
                {
                    if (ainFile.Peek(function.Address).instruction == Instruction.RETURN)
                    {
                        Expression.RemoveStatement(expression);
                        return true;
                    }
                }
            }
            if (expression.ExpressionType == Instruction.ITOF && expression.Arg1.ExpressionType == Instruction.PUSH)
            {
                expression.ExpressionType = Instruction.F_PUSH;
                expression.FloatValue = expression.Arg1.Value;
                expression.Args.Clear();
            }
            if (expression.ExpressionType == Instruction.SH_LOCALDELETE)
            {
                var variable = expression.Variable;
                if (variable != null)
                {
                    variable = variable.Canonicalize();
                }
                if (variable != null && variable.DataType.IsPrimitiveRefType())
                {
                    Expression.RemoveStatement(expression);
                    return true;
                }
            }
            if (!ainFile.UseSimplifiedCode)
            {
                if ((expression.ExpressionType == Instruction.ASSIGN || expression.ExpressionType == Instruction.LI_ASSIGN) && expression.Arg1.ExpressionType == Instruction.AssignmentRef &&
                    expression.Arg1.Arg1.ExpressionType == Instruction.PUSHLOCALPAGE && expression.Arg1.Arg2.ExpressionType == Instruction.PUSH)
                {
                    var arg2 = expression.Arg2;
                    if (arg2.ExpressionType.IsCast())
                    {
                        arg2 = arg2.SkipChildCastOperations();
                    }
                    if (arg2.ExpressionType == Instruction.PUSH)
                    {
                        var var1 = expression.Arg1.Variable;
                        if (var1.DataType == DataType.Int || var1.DataType == DataType.Bool || var1.DataType == DataType.Lint)
                        {
                            if (expression.IsVoidContext())
                            {
                                expression.ExpressionType = Instruction.SH_LOCALASSIGN;
                                expression.Value = expression.Arg1.Arg2.Value;
                                expression.Value2 = arg2.Value;
                                expression.Args.Clear();
                            }
                        }
                    }
                }
            }
            return false;
        }


        #endregion

        private void displaythisAroundLocalMembersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoRefresh();
        }

        private void showAllCastingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoRefresh();
        }

        private void removeExtraReturnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoRefresh();
        }

        private void declareVariablesAtBeginningOfFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoRefresh();
        }

        private void verboseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoRefresh();
        }

        private void showGlobalVariableprefixesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoRefresh();
        }

        private void useEnumerationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoRefresh();
        }

        private void showAlternativetranslatedVariableNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ainFile.DisplayAlternativeNames = showAlternativetranslatedVariableNamesToolStripMenuItem.Checked;
            GoRefresh();

        }

        private void useModifiedShiftJISEncodingForEuropeanTextToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void useModifiedShiftJISEncodingForEuropeanTextToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            if (useModifiedShiftJISEncodingForEuropeanTextToolStripMenuItem.Checked)
            {
                useModifiedShiftJISEncodingForAccentedTextNEWToolStripMenuItem.Checked = false;
                Extensions.TextEncoding = new UTF8Encoding(true);
                Extensions.BinaryEncoding = new ModifiedShiftJis();
            }
            else
            {
                Extensions.TextEncoding = Encoding.GetEncoding("shift_jis");
                Extensions.BinaryEncoding = Extensions.TextEncoding;
            }
        }

        private void useModifiedShiftJISEncodingForAccentedTextNEWToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            if (useModifiedShiftJISEncodingForAccentedTextNEWToolStripMenuItem.Checked)
            {
                useModifiedShiftJISEncodingForEuropeanTextToolStripMenuItem.Checked = false;
                Extensions.TextEncoding = new UTF8Encoding(true);
                Extensions.BinaryEncoding = ModifiedShiftJis2.GetEncoding();
            }
            else
            {
                Extensions.TextEncoding = Encoding.GetEncoding("shift_jis");
                Extensions.BinaryEncoding = Extensions.TextEncoding;
            }
        }

        private void viewFunctionExpressionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var currentFunction = this.CurrentFunction;
            if (currentFunction != null)
            {
                var code = ainFile.DecompiledCodeCache.GetDecompiledCode(currentFunction);

                using (var propertyGridForm = new PropertyGridForm(code, "Expression Tree"))
                {
                    propertyGridForm.ShowDialog();
                }
            }
        }

        private void compileCodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CompileCode();
        }

        private void CompileCode()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Project Environment Files (*.pje)|*.pje|All Files (*.*)|*.*";
                ofd.DefaultExt = "pje";
                if (ofd.ShowDialogWithTopic(DialogTopic.CompileCode) == DialogResult.OK)
                {
                    CompileCode(ofd.FileName);
                }
            }
        }

        private void CompileCode(string projectFileName)
        {
            var compileCodeInBackground = new CompileCodeInBackground();
            compileCodeInBackground.Run(projectFileName);
        }

        public class CompileCodeInBackground : BackgroundTask
        {
            protected override string TitleBarText { get { return "Compiling Code..."; } }
            protected override bool AbortOnCancel { get { return true; } }

            protected override object DoWork(string inputFileName, string outputFileName, BackgroundTask.WorkerData workerData)
            {
                Compiler.Compiler compiler = new Compiler.Compiler();
                compiler.worker = this.backgroundWorker;
                compiler.Error += new EventHandler<ErrorEventArgs>(compiler_Error);
                compiler.Compile(inputFileName);
                if (this.backgroundWorker.CancellationPending)
                {
                    return false;
                }
                return true;
            }

            void compiler_Error(object sender, ErrorEventArgs e)
            {
                this.backgroundWorker.ReportProgress(-1, e.GetException());
            }

            public bool Run(string projectFileName)
            {
                return base.Run(projectFileName, "");
            }
        }

        public class QuickCompileCodeInBackground : BackgroundTask
        {
            protected override string TitleBarText { get { return "Quick Compiling Code Patch..."; } }
            protected override bool AbortOnCancel { get { return true; } }

            protected override object DoWork(string codeText, string outputFileName, BackgroundTask.WorkerData workerData)
            {
                AinFile ainCopy = workerData.ExtraData as AinFile;
                Compiler.Compiler compiler = new Compiler.Compiler(ainCopy, 0);
                compiler.worker = this.backgroundWorker;
                compiler.Error += new EventHandler<ErrorEventArgs>(compiler_Error);
                compiler.CompileCodeInPlace(codeText);
                if (this.backgroundWorker.CancellationPending)
                {
                    return false;
                }
                if (compiler.Errors.Count > 0)
                {
                    return false;
                }
                ainCopy.WriteAndEncryptAinFile(outputFileName);
                return true;
            }

            void compiler_Error(object sender, ErrorEventArgs e)
            {
                this.backgroundWorker.ReportProgress(-1, e.GetException());
            }

            public bool Run(string codeText, string outputFileName, AinFile ainFile)
            {
                return base.Run(codeText, outputFileName, ainFile);
            }
        }

        private void newDocumentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NewEditorTab();
        }

        private void openDocumentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenDocument();
        }

        private void saveDocumentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveDocument();
        }

        private void saveDocumentAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveDocumentAs();
        }

        private void quickCompileCodePatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            QuickCompileCodePatch();
        }

        private void QuickCompileCodePatch()
        {
            string[] fileNames = null;
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JAF Files (*.jaf)|*.jaf|All Files (*.*)|*.*";
                openFileDialog.AutoUpgradeEnabled = true;
                openFileDialog.ValidateNames = true;
                openFileDialog.Multiselect = true;
                openFileDialog.DereferenceLinks = true;
                openFileDialog.DefaultExt = "jaf";
                openFileDialog.AddExtension = true;
                openFileDialog.CheckFileExists = true;
                openFileDialog.CheckPathExists = true;
                if (openFileDialog.ShowDialogWithTopic(DialogTopic.QuickCompileCode) == DialogResult.OK)
                {
                    fileNames = openFileDialog.FileNames;
                }
            }
            if (fileNames == null)
            {
                return;
            }
            string outputFileName = null;
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "AIN Files (*.ain)|*.ain|All Files (*.*)|*.*";
                saveFileDialog.AddExtension = true;
                saveFileDialog.AutoUpgradeEnabled = true;
                saveFileDialog.CheckPathExists = true;
                saveFileDialog.DefaultExt = "ain";
                saveFileDialog.DereferenceLinks = true;
                saveFileDialog.ValidateNames = true;
                if (saveFileDialog.ShowDialogWithTopic(DialogTopic.QuickCompileCodeSaveAin) == DialogResult.OK)
                {
                    outputFileName = saveFileDialog.FileName;
                }
            }
            if (outputFileName == null)
            {
                return;
            }
            if (fileNames != null && outputFileName != null)
            {
                QuickCompileCodePatch(fileNames, outputFileName);
            }
        }

        private void QuickCompileCodePatch(string[] fileNames, string outputFileName)
        {
            StringWriter sw = new StringWriter();
            foreach (var filename in fileNames)
            {
                string fullPath = Path.GetFullPath(filename);
                sw.WriteLine("#include " + AssemblerProjectWriter.EscapeAndQuoteString(fullPath));
            }

            string inclusionFile = sw.ToString();

            var ainCopy = ainFile.Clone();

            var quickCompiler = new QuickCompileCodeInBackground();
            quickCompiler.Run(inclusionFile, outputFileName, ainCopy);
        }

        private void exportAllFunctionsContainingMessagesToSingleFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fileName = PromptToSaveJafFile();
            if (fileName != null)
            {
                ExportFunctionsContainingMessagestoSingleFile(fileName);
            }
        }

        private void ExportFunctionsContainingMessagestoSingleFile(string fileName)
        {
            StreamWriter sw = new StreamWriter(fileName, false, Encoding.UTF8);
            foreach (var func in ainFile.Functions)
            {
                bool found = FunctionContainsMessage(func);
                if (found)
                {
                    sw.WriteLine(ainFile.DecompiledCodeCache.GetDecompiledCodeText(func));
                }
            }
            sw.Flush();
            sw.Close();
            sw.Dispose();
        }

        private static string PromptToSaveJafFile()
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JAF Files (*.jaf)|*.jaf|All Files (*.*)|*.*";
            string fileName = null;
            if (saveFileDialog.ShowDialogEx() == DialogResult.OK)
            {
                fileName = saveFileDialog.FileName;
            }
            return fileName;
        }

        private bool FunctionContainsMessage(Function func)
        {
            Instruction lookFor = Instruction.MSG;
            int count = FunctionInstructionCount(func, lookFor);
            return count > 2;
        }

        private int FunctionInstructionCount(Function func, Instruction lookFor)
        {
            int count = 0;
            int address = func.Address;
            int endAddress = Decompiler.FindEndAddress(address, ainFile);

            while (address < endAddress)
            {
                var ins = ainFile.Peek(address);
                address = ins.nextAddress;

                if (ins.instruction == lookFor)
                {
                    count++;
                }
            }
            return count;
        }

        private void wordWrapForm2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var options = DefaultWordWrapOptions.GetWordWrapOptions(ainFile);
            using (var wordWrapForm2 = new WordWrapForm2(options, ainFile))
            {
                wordWrapForm2.ShowDialog();
            }
        }

        private void zlibCompressionModeToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            fastCompressionModeToolStripMenuItem.Checked = false;
            maximumCompressionModeToolStripMenuItem.Checked = false;
            if (AinFile.DefaultCompressionLevel == ZLibNet.CompressionLevel.BestSpeed)
            {
                fastCompressionModeToolStripMenuItem.Checked = true;
            }
            else if (AinFile.DefaultCompressionLevel == ZLibNet.CompressionLevel.BestCompression)
            {
                maximumCompressionModeToolStripMenuItem.Checked = true;
            }
        }

        private void fastCompressionModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AinFile.DefaultCompressionLevel = ZLibNet.CompressionLevel.BestSpeed;
        }

        private void maximumCompressionModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AinFile.DefaultCompressionLevel = ZLibNet.CompressionLevel.BestCompression;
        }

        private void DecompiledCodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!CurrentTabIsTextEditor() && IsEditingKey(e))
            {
                EditCurrentFunction();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            if (e.KeyCode == Keys.BrowserBack)
            {
                GoBack();
            }
            if (e.KeyCode == Keys.BrowserForward)
            {
                GoForward();
            }
            if (e.KeyCode == Keys.BrowserRefresh)
            {
                GoRefresh();
            }
        }

        private void EditCurrentFunction()
        {
            var function = this.CurrentFunction;
            var currentClass = ainFile.GetStruct(this.codeDisplayState.ClassName);
            string name = (function != null) ? (function.Name) : ((currentClass != null) ? (currentClass.Name) : "");
            string whatToEdit = (function != null) ? ("function") : ((currentClass != null) ? ("class declaration for") : "");
            if (String.IsNullOrEmpty(name))
            {
                return;
            }

            string functionName = function.Name;
            string fileName = GetFilename(name, false);
            bool fileExists = File.Exists(fileName);

            string prompt = "Edit " + whatToEdit + " " + functionName + "?" + Environment.NewLine +
                (fileExists
                ?
                ("(open " + Path.GetFileName(fileName) + ")")
                :
                ("(save new " + Path.GetFileName(fileName) + ")"));

            if (MessageBox.Show(prompt, "Edit Code", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
            {
                fileName = GetFilename(name, true);
                OpenEditorForFilename(fileName);
            }
        }

        private void OpenEditorForFilename(string fileName)
        {
            if (File.Exists(fileName))
            {
                var scintillaState = new ScintillaState();
                scintillaState.ReadFromScintilla(this.DecompiledCodeTextBox);

                if (OpenDocument(fileName))
                {
                    scintillaState.WriteToScintilla(this.DecompiledCodeTextBox);
                    this.DecompiledCodeTextBox.Modified = false;
                }
                else
                {
                    MessageBox.Show("Unable to open " + fileName + "!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                }
            }
            else
            {
                var scintillaState = new ScintillaState();
                scintillaState.ReadFromScintilla(this.DecompiledCodeTextBox);

                string newText;

                if (ainFile.DisplayAlternativeNames)
                {
                    ainFile.DisplayAlternativeNames = false;
                    GoRefresh();
                    newText = this.DecompiledCodeTextBox.Text;
                    ainFile.DisplayAlternativeNames = true;
                    GoRefresh();
                }
                else
                {
                    newText = this.DecompiledCodeTextBox.Text;
                }

                NewEditorTab();
                this.DecompiledCodeTextBox.Text = newText;
                scintillaState.WriteToScintilla(this.DecompiledCodeTextBox);
                codeDisplayState.FileNameToEdit = fileName;
                SaveDocumentAs(fileName);
                this.DecompiledCodeTextBox.Modified = false;
            }
        }

        private string GetFilename(string name, bool createDirectory)
        {
            string baseDirectory = Path.GetDirectoryName(this.ainFile.OriginalFilename);
            string codePatchesDirectory = Path.Combine(baseDirectory, "CodePatches");
            if (createDirectory)
            {
                if (!Directory.Exists(codePatchesDirectory))
                {
                    Directory.CreateDirectory(codePatchesDirectory);
                }
            }
            return Path.Combine(codePatchesDirectory, name + ".jaf");
        }

        private bool IsEditingKey(KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.V || e.KeyCode == Keys.X)
                {
                    return true;
                }
            }
            else if (!e.Alt)
            {
                if (e.Shift && (e.KeyCode == Keys.Insert || e.KeyCode == Keys.Delete))
                {
                    return true;
                }
                if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
                {
                    return true;
                }
                if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
                {
                    return true;
                }
                if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.Divide)
                {
                    return true;
                }
                if (e.KeyCode >= Keys.OemSemicolon && e.KeyCode <= Keys.Oemtilde)
                {
                    return true;
                }
                if (e.KeyCode >= Keys.OemOpenBrackets && e.KeyCode <= Keys.OemBackslash)
                {
                    return true;
                }
                if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back || e.KeyCode == Keys.Space || e.KeyCode == Keys.Tab || e.KeyCode == Keys.LineFeed || e.KeyCode == Keys.Enter)
                {
                    return true;
                }
            }
            return false;
        }

        private void findBuiltinMethodsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSearchForm(SearchKind.BuiltInMethod);
        }

        private void COMPILERTESTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string directoryName = @"<insert filename here>";
            string[] ainFiles = Directory.GetFiles(directoryName, "*.ain");
            foreach (string ainFileName in ainFiles)
            {
                TestCompiler(ainFileName, false);
            }
        }

        private void TestCompiler(string ainFileName, bool force)
        {
            string directoryName = Path.GetDirectoryName(ainFileName);
            AinFile oldDefaultAinFile = Expression.defaultAinFile;
            AinFile lastLoadedAinFile = AinFile.LastLoadedAinFile;
            try
            {
                string baseName = Path.GetFileNameWithoutExtension(ainFileName);
                string outputDirectory = Path.Combine(directoryName, baseName);
                var ainFile = new AinFile(ainFileName);

                //deleteme:
                if (!force && (ainFile.Version >= 6 || ainFile.Version < 4))
                {
                    return;
                }

                Directory.CreateDirectory(outputDirectory);
                Expression.defaultAinFile = ainFile;
                ainFile.DisplayAlternativeNames = false;

                var codeDisplayOptions = GetCodeDisplayOptions();
                codeDisplayOptions.AddEndfunc = false;
                codeDisplayOptions.AnnotateWithDecompiledCode = false;
                codeDisplayOptions.DeclareVariablesAtBeginning = false;
                codeDisplayOptions.DisassembleDisplayBytes = false;
                codeDisplayOptions.DisplayDefaultValuesForMethods = true;
                codeDisplayOptions.ForceOriginalStringOrder = false;
                codeDisplayOptions.MergeDuplicateMessages = true;
                codeDisplayOptions.MergeDuplicateStrings = true;
                codeDisplayOptions.RemoveOrphanFunctions = true;
                codeDisplayOptions.RemoveReturns = false;
                codeDisplayOptions.ShowCasting = true;
                codeDisplayOptions.ShowGlobalPrefix = false;
                codeDisplayOptions.ShowThis = true;
                codeDisplayOptions.UseEnumerations = false;
                codeDisplayOptions.Verbose = false;

                string disassembledDirectory = Path.Combine(outputDirectory, "DISASSEMBLED_ORIGINAL");
                string disassembledRebuiltDirectory = Path.Combine(outputDirectory, "DISASSEMBLED_REBUILT");
                string decompiledDirectory = Path.Combine(outputDirectory, "DECOMPILED");
                string disassembledRecompiledDirectory = Path.Combine(outputDirectory, "DISASSEMBLED_RECOMPILED");

                if (/*force || */!Directory.Exists(disassembledDirectory))
                {
                    Directory.CreateDirectory(disassembledDirectory);
                    var disassembler1 = new AssemblerProjectWriter(ainFile, codeDisplayOptions);
                    disassembler1.SaveAsProject(disassembledDirectory, false);
                }

                if (force || !Directory.Exists(disassembledRebuiltDirectory))
                {
                    Directory.CreateDirectory(disassembledRebuiltDirectory);
                    var rebuiltAinFile = RebuildAinFileWithCodeGenerator(ainFile, true);
                    Expression.defaultAinFile = rebuiltAinFile;
                    var disassembler2 = new AssemblerProjectWriter(rebuiltAinFile, codeDisplayOptions);
                    disassembler2.SaveAsProject(disassembledRebuiltDirectory, false);
                    string rebuiltFileName = Path.Combine(outputDirectory, baseName + "_rebuilt.ain");
                    //rebuiltAinFile.WriteAndEncryptAinFile(rebuiltFileName);
                }

                if (force || !Directory.Exists(decompiledDirectory))
                {
                    Directory.CreateDirectory(decompiledDirectory);
                    var codeExporter = new CodeExporter(ainFile, GetCodeDisplayOptions());
                    codeExporter.ExportFiles(decompiledDirectory);
                    string metadataFileName = Path.Combine(directoryName, Path.GetFileNameWithoutExtension(ainFileName) + "_metadata.xml");
                    string metadataFileName2 = Path.Combine(decompiledDirectory, Path.GetFileNameWithoutExtension(ainFileName) + "_metadata.xml");
                    if (File.Exists(metadataFileName) && !File.Exists(metadataFileName2))
                    {
                        File.Copy(metadataFileName, metadataFileName2);
                    }
                }

                if (force || !Directory.Exists(disassembledRecompiledDirectory))
                {
                    Directory.CreateDirectory(disassembledRecompiledDirectory);
                    string recompiledFileName = Path.Combine(decompiledDirectory, Path.GetFileName(ainFileName));
                    var compiler = new Compiler.Compiler();
                    string pjeFileName = Path.Combine(decompiledDirectory, baseName + ".pje");
                    compiler.Compile(pjeFileName);
                    var recompiledAin = new AinFile(recompiledFileName);
                    if (recompiledAin.OriginalFilename != "")
                    {
                        Expression.defaultAinFile = recompiledAin;
                        var disassembler3 = new AssemblerProjectWriter(recompiledAin, codeDisplayOptions);
                        disassembler3.SaveAsProject(disassembledRecompiledDirectory, false);
                    }
                    else
                    {

                    }
                }
            }
            finally
            {
                Expression.defaultAinFile = oldDefaultAinFile;
                AinFile.LastLoadedAinFile = lastLoadedAinFile;
            }
        }

        private void COMPILERTESTthisOnlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TestCompiler(ainFile.OriginalFilename, true);
        }

        private void saveAsAIN2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.ainFile == null)
            {
                return;
            }
            this.ainFile.IsAi2File = !this.ainFile.IsAi2File;
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "AIN Files (*.ain)|*.ain|All Files (*.*)|*.*";
                saveFileDialog.FileName = ainFile.OriginalFilename;
                var dialogResult = saveFileDialog.ShowDialogEx();
                if (dialogResult == DialogResult.OK)
                {
                    ainFile.WriteAndEncryptAinFile(saveFileDialog.FileName, ZLibNet.CompressionLevel.BestCompression);
                }
            }
        }

        private void saveAsAINunencryptedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.ainFile == null)
            {
                return;
            }
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "AIN Files (*.ain)|*.ain|All Files (*.*)|*.*";
                saveFileDialog.FileName = ainFile.OriginalFilename;
                var dialogResult = saveFileDialog.ShowDialogEx();
                if (dialogResult == DialogResult.OK)
                {
                    ainFile.WriteAinFile(saveFileDialog.FileName);
                }
            }
        }
    }
}
