using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AinDecompiler
{
    public partial class AnnotateWithNamesForm : Form
    {
        AnnotationSettings mySettings = new AnnotationSettings();
        AinFile ainFile = AinFile.defaultInstance;

        private AnnotateWithNamesForm()
        {
            InitializeComponent();
        }

        public AnnotateWithNamesForm(AinFile ainFile)
            : this()
        {
            LoadEnumerationTypes(ainFile);

            this.mySettings = AnnotationSettings.GetAnnotationSettings(ainFile);
            ReadSettings();
        }

        private void LoadEnumerationTypes(AinFile ainFile)
        {
            var enumerationTypeNames = ainFile.MetadataFile.EnumerationTypes.Keys.ToArray();
            foreach (var typeName in enumerationTypeNames)
            {
                this.enumComboBox.Items.Add(typeName);
            }
        }

        private void ReadSettings()
        {
            this.useEnumerationTypeCheckBox.Checked = mySettings.UseEnumerationType;
            this.useFunctionParametersCheckBox.Checked = mySettings.UseVariables;
            var enumerationType = mySettings.GetEnumerationType(ainFile);
            if (enumerationType == null)
            {
                this.enumComboBox.Text = "";
            }
            else
            {
                this.enumComboBox.Text = enumerationType.Name;
            }
            var parameters = mySettings.GetVariables(ainFile);

            StringBuilder sb = new StringBuilder();
            //lstParameters.BeginUpdate();
            //lstParameters.Items.Clear();
            foreach (var param in parameters)
            {
                var function = param.Parent as Function;
                if (function != null)
                {
                    string parameterName = function.Name + "." + param.Name;
                    sb.AppendLine(parameterName);
                    //lstParameters.Items.Add(parameterName);
                }
            }
            //lstParameters.EndUpdate();
            variableNamesTextBox.Text = sb.ToString();

            sb.Length = 0;
            var substitutionTable = mySettings.GetSubstitutionTable();
            foreach (var pair in substitutionTable)
            {
                sb.AppendLine(pair.Key + "\t" + pair.Value);
            }
            substitutionTextBox.Text = sb.ToString();
        }

        void WriteSettings()
        {
            mySettings.UseEnumerationType = this.useEnumerationTypeCheckBox.Checked;
            mySettings.UseVariables = this.useFunctionParametersCheckBox.Checked;
            mySettings.EnumerationTypeName = this.enumComboBox.Text;
            mySettings.VariableNames.Clear();
            //foreach (string item in lstParameters.Items)
            foreach (string item in variableNamesTextBox.Lines)
            {
                mySettings.VariableNames.Add(item);
            }

            mySettings.SubstitutionTable.Clear();
            foreach (string line in substitutionTextBox.Lines)
            {
                mySettings.SubstitutionTable.Add(line);
            }
            ReadSettings();
        }

        private void AnnotateWithNamesForm_Load(object sender, EventArgs e)
        {

        }

        private void importToolStripButton_DropDownOpening(object sender, EventArgs e)
        {
            foreach (ToolStripItem dropDownItem in importToolStripButton.DropDownItems)
            {
                dropDownItem.Click -= new EventHandler(newItem_Click);
            }

            importToolStripButton.DropDownItems.Clear();
            string[] keys = RegistryUtility.ListKeys("AnnotationSettings");
            foreach (var key in keys)
            {
                var newItem = new ToolStripMenuItem(key);
                newItem.Tag = key;
                newItem.Click += new EventHandler(newItem_Click);
                importToolStripButton.DropDownItems.Add(newItem);
            }
        }

        void newItem_Click(object sender, EventArgs e)
        {
            ToolStripItem item = sender as ToolStripItem;
            if (item != null)
            {
                string codeName = item.Tag as string;
                if (codeName == null)
                {
                    codeName = item.Text;
                }
                RegistryUtility.GetObject("AnnotationSettings\\" + codeName, mySettings);
                ReadSettings();
            }
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            WriteSettings();
            mySettings.SaveToRegistry(ainFile);
        }

        //private void addButton_Click(object sender, EventArgs e)
        //{
        //    this.lstParameters.Items.Add(this.FunctionNameTextBox.Text);
        //}

        //private void removeButton_Click(object sender, EventArgs e)
        //{
        //    int listIndex = this.lstParameters.SelectedIndex;
        //    if (listIndex >= 0 && listIndex < this.lstParameters.Items.Count)
        //    {
        //        this.lstParameters.Items.RemoveAt(listIndex);
        //    }
        //}
    }

    public class AnnotationSettings
    {
        public string EnumerationTypeName;
        public List<string> VariableNames = new List<string>();
        public List<string> SubstitutionTable = new List<string>();
        public bool UseEnumerationType;
        public bool UseVariables;

        public void SaveToRegistry(AinFile ainFile)
        {
            string baseName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename);
            string pathName = "AnnotationSettings\\" + baseName;
            SaveToRegistry(pathName);
        }

        public void SaveToRegistry(string registryPathName)
        {
            RegistryUtility.SaveObject(registryPathName, this);
        }

        public static AnnotationSettings GetAnnotationSettings(AinFile ainFile)
        {
            var annotationSettings = new AnnotationSettings();
            string baseName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename);
            string pathName = "AnnotationSettings\\" + baseName;
            if (RegistryUtility.PathExists(pathName))
            {
                RegistryUtility.GetObject(pathName, annotationSettings);
            }
            else
            {

            }
            return annotationSettings;
        }

        public EnumerationType GetEnumerationType(AinFile ainFile)
        {
            if (String.IsNullOrEmpty(this.EnumerationTypeName))
            {
                return null;
            }
            return ainFile.MetadataFile.EnumerationTypes.GetOrNull(this.EnumerationTypeName);
        }

        public Variable[] GetVariables(AinFile ainFile)
        {
            List<Variable> list = new List<Variable>();
            foreach (var variableName in VariableNames)
            {
                int dotPosition = variableName.IndexOf('.');
                if (dotPosition == -1)
                {
                    dotPosition = variableName.Length;
                }
                if (dotPosition > 0)
                {
                    string functionName = variableName.Substring(0, dotPosition);
                    string parameterName = "";
                    if (dotPosition + 1 <= variableName.Length)
                    {
                        parameterName = variableName.Substring(dotPosition + 1);
                    }

                    var function = ainFile.GetFunction(functionName);
                    if (function != null)
                    {
                        Variable variable = null;
                        for (int i = 0; i < function.ParameterCount; i++)
                        {
                            if (function.Parameters[i].Name == parameterName)
                            {
                                variable = function.Parameters[i];
                                break;
                            }
                        }
                        if (variable == null)
                        {
                            int variableIndex = 0;
                            if (!int.TryParse(parameterName, out variableIndex))
                            {
                                variableIndex = 0;
                            }
                            variable = function.GetNonVoidFunctionParameter(variableIndex) as Variable;
                        }

                        if (variable != null)
                        {
                            list.Add((Variable)variable);
                        }
                    }
                }
            }
            return list.ToArray();
        }

        public Dictionary<string, string> GetSubstitutionTable()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            foreach (var line in this.SubstitutionTable)
            {
                var pieces = line.Split('\t');
                if (pieces.Length >= 2)
                {
                    string firstPiece = pieces[0];
                    string secondPiece = pieces[1];
                    firstPiece = firstPiece.Trim();
                    secondPiece = secondPiece.Trim();
                    if (!String.IsNullOrEmpty(firstPiece) && !String.IsNullOrEmpty(secondPiece))
                    {
                        dic[firstPiece] = secondPiece;
                    }
                }
            }
            return dic;
        }
    }
}
