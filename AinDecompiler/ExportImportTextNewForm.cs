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
    public partial class ExportImportTextNewForm : Form
    {
        AinFile ainFile;
        WordWrapOptions wordWrapOptions = null;

        public ExportImportTextNewForm(AinFile ainFile)
        {
            this.ainFile = ainFile;

            InitializeComponent();
            wordWrapOptions = WordWrapOptions.GetWordWrapOptions(ainFile);
            this.useWordWrappingCheckBox.Checked = !wordWrapOptions.Disabled;
        }

        private void wordWrapSettingsButton_Click(object sender, EventArgs e)
        {
            using (var wordWrapOptionsForm = new WordWrapForm2(wordWrapOptions, ainFile))
            {
                var dialogResult = wordWrapOptionsForm.ShowDialog();
                if (dialogResult == DialogResult.OK || dialogResult == DialogResult.Yes)
                {
                    wordWrapOptions.SaveToRegistry(ainFile);
                }
            }
        }

        private void exportMessagesAndStringsButton_Click(object sender, EventArgs e)
        {
            var exportImport = new TextImportExport(ainFile);
            AddAnnotations(exportImport);

            var defaultFileName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename) + "_text.txt";
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.FileName = defaultFileName;
                if (saveFileDialog.ShowDialogWithTopic(DialogTopic.ExportText) == DialogResult.OK)
                {
                    exportImport.SaveText(saveFileDialog.FileName, Extensions.TextEncoding);
                }
            }
        }

        private void importMessagesAndStringsButton_Click(object sender, EventArgs e)
        {
            var exportImport = new TextImportExport(ainFile);
            exportImport.wordWrapOptions = this.wordWrapOptions;
            if (this.useWordWrappingCheckBox.Checked == false)
            {
                exportImport.wordWrapOptions = null;
            }
            var defaultFileName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename) + "_text.txt";
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.DefaultExt = "txt";
                openFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                openFileDialog.FileName = defaultFileName;
                if (openFileDialog.ShowDialogWithTopic(DialogTopic.ImportText) == DialogResult.OK)
                {
                    using (var saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.DefaultExt = "ain";
                        saveFileDialog.Filter = "Ain Files (*.ain)|*.ain|All Files (*.*)|*.*";
                        if (saveFileDialog.ShowDialogWithTopic(DialogTopic.ImportTextSaveAin) == DialogResult.OK)
                        {
                            string textFileName = openFileDialog.FileName;
                            string outputFileName = saveFileDialog.FileName;

                            exportImport.ReplaceText(textFileName, outputFileName);
                        }
                    }
                }
            }
        }

        private void exportMatchingStringsButton_Click(object sender, EventArgs e)
        {
            var exportImport = new TextImportExport(ainFile);

            string fileName = "";
            string[] lines;
            using (var textPromptForm = new TextPromptForm())
            {
                textPromptForm.Text = "Enter strings to match one line at a time, or load a text file";
                if (textPromptForm.ShowDialog() == DialogResult.OK)
                {
                    fileName = textPromptForm.FileName;
                    lines = textPromptForm.scintilla1.Text.Split(Environment.NewLine);
                }
                else
                {
                    return;
                }
            }

            foreach (var line in lines)
            {
                if (line != "")
                {
                    exportImport.StringsToMatch.Set(line);
                }
            }

            if (exportImport.StringsToMatch.Count == 0)
            {
                return;
            }

            StringBuilder outputStringBuilder = new StringBuilder();
            StringWriter sw = new StringWriter(outputStringBuilder);
            exportImport.IncludeMessages = false;
            exportImport.SaveText(sw);
            string outputText = outputStringBuilder.ToString();

            using (var textPromptForm = new TextPromptForm())
            {
                textPromptForm.Text = "Save this file or copy it to the clipboard";
                textPromptForm.FileName = fileName;
                textPromptForm.ShowOkButton = false;
                textPromptForm.scintilla1.Text = outputText;
                textPromptForm.ShowDialog();
            }
        }

        private void ExportMessagesButton_Click(object sender, EventArgs e)
        {
            var exportImport = new TextImportExport(ainFile);
            AddAnnotations(exportImport);

            var defaultFileName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename) + "_text.txt";
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.FileName = defaultFileName;
                if (saveFileDialog.ShowDialogWithTopic(DialogTopic.ExportText) == DialogResult.OK)
                {
                    exportImport.SaveTextToMultipleFiles(saveFileDialog.FileName, Extensions.TextEncoding);
                }
            }
        }

        private void AddAnnotations(TextImportExport exportImport)
        {
            if (this.annotateWithNamesCheckBox.Checked)
            {
                var annotationSettings = AnnotationSettings.GetAnnotationSettings(ainFile);
                exportImport.SetAnnotations(annotationSettings);
            }
            exportImport.CombineMessages = !this.useOldFormatCheckBox.Checked;
        }

        private void ExportImportTextNewForm_Load(object sender, EventArgs e)
        {

        }

        private void namesButton_Click(object sender, EventArgs e)
        {
            using (var annotateWithNamesForm = new AnnotateWithNamesForm(this.ainFile))
            {
                annotateWithNamesForm.ShowDialog();
            }
        }

        private void exportStringsButton_Click(object sender, EventArgs e)
        {
            var exportImport = new TextImportExport(ainFile);
            exportImport.IncludeMessages = false;
            exportImport.IncludeStrings = true;

            var defaultFileName = Path.GetFileNameWithoutExtension(ainFile.OriginalFilename) + "_strings.txt";
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.DefaultExt = "txt";
                saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.FileName = defaultFileName;
                if (saveFileDialog.ShowDialogWithTopic(DialogTopic.ExportText) == DialogResult.OK)
                {
                    exportImport.SaveText(saveFileDialog.FileName, Extensions.TextEncoding);
                }
            }
        }
    }
}
