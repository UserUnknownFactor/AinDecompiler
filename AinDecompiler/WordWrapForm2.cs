using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Globalization;

namespace AinDecompiler
{
    public partial class WordWrapForm2 : Form
    {
        WordWrapOptions wordWrapOptionsAttached;
        WordWrapOptions wordWrapOptions;
        AinFile ainFile;
        WordWrapOptionsProfile currentProfile = null;

        bool _ignoreEvents = false;

        public WordWrapForm2()
        {
            InitializeComponent();
        }

        public WordWrapForm2(WordWrapOptions wordWrapOptions, AinFile ainFile)
        {
            this.wordWrapOptionsAttached = wordWrapOptions ?? new WordWrapOptions();
            this.wordWrapOptions = this.wordWrapOptionsAttached.Clone();
            this.ainFile = ainFile;
            InitializeComponent();
        }

        private void WordWrapForm2_Load(object sender, EventArgs e)
        {
            try
            {
                _ignoreEvents = true;
                ListProfiles();
                if (listView1.Items.Count > 0)
                {
                    listView1.Items[0].Selected = true;
                    listView1.Items[0].Focused = true;
                }
            }
            finally
            {
                _ignoreEvents = false;
            }
            this.currentProfile = GetSelectedProfile();
            ReadFields();
        }

        WordWrapOptionsProfile GetSelectedProfile()
        {
            var selectedItem = listView1.SelectedItems.OfType<ListViewItem>().FirstOrDefault();
            if (selectedItem != null)
            {
                return selectedItem.Tag as WordWrapOptionsProfile;
            }
            return null;
        }

        private void ReadFields()
        {
            this.useVariableWidthFontCheckBox.Checked = wordWrapOptions.UseVariableWidthFont;
            this.maintainIndentationCheckBox.Checked = wordWrapOptions.MaintainIndentation;
            this.removeLineBreaksIfWordWrappingCheckBox.Checked = wordWrapOptions.RemoveLineBreaksIfWordWrapping;
            this.useVariableWidthFontCheckBox.Checked = wordWrapOptions.UseVariableWidthFont;
            this.neverUseWordWrapCheckBox.Checked = wordWrapOptions.Disabled;

            this.kanjiWidthTextBox.Text = wordWrapOptions.TemplateKanjiWidth.ToString(CultureInfo.InvariantCulture);

            if (currentProfile != null)
            {
                this.codeTextBox.Text = currentProfile.GetTriggerCodes();
                charactersPerLineTextBox.Text = currentProfile.MaxCharactersPerLine.ToString();
                linesPerMessageTextBox.Text = currentProfile.MaxLinesPerMessage.ToString();
            }
        }

        private int SetFields()
        {
            wordWrapOptions.UseVariableWidthFont = this.useVariableWidthFontCheckBox.Checked;
            wordWrapOptions.MaintainIndentation = this.maintainIndentationCheckBox.Checked;
            wordWrapOptions.RemoveLineBreaksIfWordWrapping = this.removeLineBreaksIfWordWrappingCheckBox.Checked;
            wordWrapOptions.UseVariableWidthFont = this.useVariableWidthFontCheckBox.Checked;
            wordWrapOptions.Disabled = this.neverUseWordWrapCheckBox.Checked;

            int failure = 0;
            if (currentProfile != null)
            {
                failure |= (!currentProfile.SetTriggerCodes(this.codeTextBox.Text, this.ainFile)) ? 1 : 0;
                int charactersPerLine;
                if (int.TryParse(charactersPerLineTextBox.Text, out charactersPerLine))
                {
                    currentProfile.MaxCharactersPerLine = charactersPerLine;
                }
                else
                {
                    failure |= 2;
                }
                int linesPerMessage;
                if (int.TryParse(linesPerMessageTextBox.Text, out linesPerMessage))
                {
                    currentProfile.MaxLinesPerMessage = linesPerMessage;
                }
                else
                {
                    failure |= 4;
                }
            }

            double kanjiWidth;
            if (double.TryParse(kanjiWidthTextBox.Text, out kanjiWidth))
            {
                wordWrapOptions.TemplateKanjiWidth = (float)kanjiWidth;
            }
            else
            {
                failure |= 8;
            }

            if (failure == 0)
            {
                errorProvider1.Clear();
            }

            return failure;
        }

        private void ListProfiles()
        {
            try
            {
                listView1.BeginUpdate();
                listView1.Items.Clear();

                for (int i = 0; i < wordWrapOptions.WordWrapOptionsProfiles.Count; i++)
                {
                    var profile = wordWrapOptions.WordWrapOptionsProfiles[i];
                    var listItem = new ListViewItem(profile.ProfileName, listView1.Groups[0]);
                    listItem.Tag = profile;
                    listView1.Items.Add(listItem);
                }
            }
            finally
            {
                listView1.EndUpdate();
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_ignoreEvents)
            {
                return;
            }

            SetFields();
            this.currentProfile = GetSelectedProfile();
            ReadFields();
        }

        private void listView1_BeforeLabelEdit(object sender, LabelEditEventArgs e)
        {
            e.CancelEdit = false;
        }

        private void listView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (e.CancelEdit == false && e.Label != null)
            {
                var listView = sender as ListView;
                if (e.Item >= 0 && e.Item < listView1.Items.Count)
                {
                    var item = listView.Items[e.Item];
                    var profile = item.Tag as WordWrapOptionsProfile;
                    if (profile != null)
                    {
                        profile.ProfileName = e.Label.Trim();
                        item.Text = profile.ProfileName;
                    }
                }
            }
        }

        private void addNodeToolStripButton_Click(object sender, EventArgs e)
        {
            var newProfile = new WordWrapOptionsProfile();
            newProfile.ProfileName = "New wordwrap settings";
            var newListItem = new ListViewItem(newProfile.ProfileName, listView1.Groups[0]);
            wordWrapOptions.WordWrapOptionsProfiles.Add(newProfile);
            listView1.Items.Add(newListItem);
            newListItem.Selected = true;
            newListItem.Focused = true;
            newListItem.BeginEdit();
        }

        private void removeToolStripButton_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 0)
            {
                return;
            }
            int selectedIndex = listView1.SelectedIndices.OfType<int>().FirstOrDefault();
            if (selectedIndex >= 0 && selectedIndex < this.wordWrapOptions.WordWrapOptionsProfiles.Count && this.wordWrapOptions.WordWrapOptionsProfiles.Count > 1)
            {
                var profile = this.wordWrapOptions.WordWrapOptionsProfiles.GetOrNull(selectedIndex);
                var item = this.listView1.Items[selectedIndex];
                item.Tag = null;
                if (currentProfile == profile)
                {
                    currentProfile = null;
                }
                listView1.Items.RemoveAt(selectedIndex);
                this.wordWrapOptions.WordWrapOptionsProfiles.RemoveAt(selectedIndex);
                var focusedItem = listView1.FocusedItem;
                if (focusedItem != null)
                {
                    focusedItem.Selected = true;
                }
                else
                {
                    listView1.Items[0].Selected = true;
                }
            }
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            applyButton_Click(applyButton, e);
            Close();
        }

        private void applyButton_Click(object sender, EventArgs e)
        {
            ApplyChanges();
            //if (ApplyChanges != null)
            //{
            //    ApplyChanges(this, EventArgs.Empty);
            //}
        }

        private void ApplyChanges()
        {
            SetFields();
            this.wordWrapOptionsAttached.AssignFrom(this.wordWrapOptions);
        }

        //public event EventHandler ApplyChanges;

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void linesPerMessageTextBox_Validating(object sender, CancelEventArgs e)
        {
            int result = SetFields();
            var control = sender as Control;
            if (0 != (result & 4))
            {
                errorProvider1.SetError(control, "Invalid lines per message");
            }
            else
            {
                errorProvider1.SetError(control, "");
            }
        }

        private void charactersPerLineTextBox_Validating(object sender, CancelEventArgs e)
        {
            int result = SetFields();
            var control = sender as Control;
            if (0 != (result & 2))
            {
                errorProvider1.SetError(control, "Invalid characters per line");
            }
            else
            {
                errorProvider1.SetError(control, "");
            }
        }

        private void codeTextBox_Validating(object sender, CancelEventArgs e)
        {
            int result = SetFields();
            var control = sender as Control;
            if (0 != (result & 1))
            {
                errorProvider1.SetError(control, "Code contains errors");
            }
            else
            {
                errorProvider1.SetError(control, "");
            }
        }

        private void selectFontButton_Click(object sender, EventArgs e)
        {
            using (var fontDialog = new FontDialog())
            {
                if (this.wordWrapOptions != null)
                {
                    fontDialog.Font = new Font(this.wordWrapOptions.TemplateFontName, this.wordWrapOptions.TemplateFontSize, this.wordWrapOptions.TemplateFontBold ? FontStyle.Bold : FontStyle.Regular);
                }

                if (fontDialog.ShowDialog() == DialogResult.OK)
                {
                    var font = fontDialog.Font;
                    this.wordWrapOptions.TemplateFontBold = (0 != (font.Style & FontStyle.Bold)) ? true : false;
                    this.wordWrapOptions.TemplateFontName = font.FontFamily.Name;
                    this.wordWrapOptions.TemplateFontSize = font.Size;
                }
            }
        }

        private void neverUseWordWrapCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            DisableOrEnableAllControls(!this.neverUseWordWrapCheckBox.Checked);
        }

        private void DisableOrEnableAllControls(bool enabled)
        {
            this.useVariableWidthFontCheckBox.Enabled = enabled;
            this.maintainIndentationCheckBox.Enabled = enabled;
            this.removeLineBreaksIfWordWrappingCheckBox.Enabled = enabled;
            this.useVariableWidthFontCheckBox.Enabled = enabled;

            this.codeTextBox.Enabled = enabled;
            this.linesPerMessageTextBox.Enabled = enabled;
            this.charactersPerLineTextBox.Enabled = enabled;

            this.listView1.Enabled = enabled;
            this.toolStrip1.Enabled = enabled;
            this.selectFontButton.Enabled = enabled;

            this.lblCharactersPerLine.Enabled = enabled;
            this.lblCode.Enabled = enabled;
            this.lblLinesPerMessage.Enabled = enabled;

            this.kanjiWidthTextBox.Enabled = enabled;
            this.lblKanjiWidth.Enabled = enabled;
        }

        private void kanjiWidthTextBox_Validating(object sender, CancelEventArgs e)
        {
            int result = SetFields();
            var control = sender as Control;
            if (0 != (result & 8))
            {
                errorProvider1.SetError(control, "Invalid Kanji Width");
            }
            else
            {
                errorProvider1.SetError(control, "");
            }
        }
    }
}
