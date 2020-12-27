namespace AinDecompiler
{
    partial class ExportImportTextNewForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.useWordWrappingCheckBox = new System.Windows.Forms.CheckBox();
            this.wordWrapSettingsButton = new System.Windows.Forms.Button();
            this.exportMessagesAndStringsButton = new System.Windows.Forms.Button();
            this.importMessagesAndStringsButton = new System.Windows.Forms.Button();
            this.migratePatchButton = new System.Windows.Forms.Button();
            this.exportMatchingStringsButton = new System.Windows.Forms.Button();
            this.ExportMessagesButton = new System.Windows.Forms.Button();
            this.annotateWithNamesCheckBox = new System.Windows.Forms.CheckBox();
            this.namesButton = new System.Windows.Forms.Button();
            this.useOldFormatCheckBox = new System.Windows.Forms.CheckBox();
            this.exportStringsButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // useWordWrappingCheckBox
            // 
            this.useWordWrappingCheckBox.AutoSize = true;
            this.useWordWrappingCheckBox.Checked = true;
            this.useWordWrappingCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.useWordWrappingCheckBox.Location = new System.Drawing.Point(12, 12);
            this.useWordWrappingCheckBox.Name = "useWordWrappingCheckBox";
            this.useWordWrappingCheckBox.Size = new System.Drawing.Size(123, 17);
            this.useWordWrappingCheckBox.TabIndex = 0;
            this.useWordWrappingCheckBox.Text = "Use Word Wrapping";
            this.useWordWrappingCheckBox.UseVisualStyleBackColor = true;
            // 
            // wordWrapSettingsButton
            // 
            this.wordWrapSettingsButton.Location = new System.Drawing.Point(141, 8);
            this.wordWrapSettingsButton.Name = "wordWrapSettingsButton";
            this.wordWrapSettingsButton.Size = new System.Drawing.Size(122, 23);
            this.wordWrapSettingsButton.TabIndex = 1;
            this.wordWrapSettingsButton.Text = "Word Wrap Settings...";
            this.wordWrapSettingsButton.UseVisualStyleBackColor = true;
            this.wordWrapSettingsButton.Click += new System.EventHandler(this.wordWrapSettingsButton_Click);
            // 
            // exportMessagesAndStringsButton
            // 
            this.exportMessagesAndStringsButton.Location = new System.Drawing.Point(12, 35);
            this.exportMessagesAndStringsButton.Name = "exportMessagesAndStringsButton";
            this.exportMessagesAndStringsButton.Size = new System.Drawing.Size(251, 23);
            this.exportMessagesAndStringsButton.TabIndex = 2;
            this.exportMessagesAndStringsButton.Text = "Export Messages and Strings...";
            this.exportMessagesAndStringsButton.UseVisualStyleBackColor = true;
            this.exportMessagesAndStringsButton.Click += new System.EventHandler(this.exportMessagesAndStringsButton_Click);
            // 
            // importMessagesAndStringsButton
            // 
            this.importMessagesAndStringsButton.Location = new System.Drawing.Point(12, 122);
            this.importMessagesAndStringsButton.Name = "importMessagesAndStringsButton";
            this.importMessagesAndStringsButton.Size = new System.Drawing.Size(251, 23);
            this.importMessagesAndStringsButton.TabIndex = 5;
            this.importMessagesAndStringsButton.Text = "Import Messages and Strings...";
            this.importMessagesAndStringsButton.UseVisualStyleBackColor = true;
            this.importMessagesAndStringsButton.Click += new System.EventHandler(this.importMessagesAndStringsButton_Click);
            // 
            // migratePatchButton
            // 
            this.migratePatchButton.Location = new System.Drawing.Point(12, 151);
            this.migratePatchButton.Name = "migratePatchButton";
            this.migratePatchButton.Size = new System.Drawing.Size(251, 23);
            this.migratePatchButton.TabIndex = 6;
            this.migratePatchButton.Text = "Migrate patch to a new version of the Game...";
            this.migratePatchButton.UseVisualStyleBackColor = true;
            // 
            // exportMatchingStringsButton
            // 
            this.exportMatchingStringsButton.Location = new System.Drawing.Point(12, 93);
            this.exportMatchingStringsButton.Name = "exportMatchingStringsButton";
            this.exportMatchingStringsButton.Size = new System.Drawing.Size(251, 23);
            this.exportMatchingStringsButton.TabIndex = 4;
            this.exportMatchingStringsButton.Text = "Export Matching Strings...";
            this.exportMatchingStringsButton.UseVisualStyleBackColor = true;
            this.exportMatchingStringsButton.Click += new System.EventHandler(this.exportMatchingStringsButton_Click);
            // 
            // ExportMessagesButton
            // 
            this.ExportMessagesButton.Location = new System.Drawing.Point(12, 180);
            this.ExportMessagesButton.Name = "ExportMessagesButton";
            this.ExportMessagesButton.Size = new System.Drawing.Size(251, 23);
            this.ExportMessagesButton.TabIndex = 7;
            this.ExportMessagesButton.Text = "Export Messages to Multiple Files...";
            this.ExportMessagesButton.UseVisualStyleBackColor = true;
            this.ExportMessagesButton.Click += new System.EventHandler(this.ExportMessagesButton_Click);
            // 
            // annotateWithNamesCheckBox
            // 
            this.annotateWithNamesCheckBox.AutoSize = true;
            this.annotateWithNamesCheckBox.Checked = true;
            this.annotateWithNamesCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.annotateWithNamesCheckBox.Location = new System.Drawing.Point(12, 213);
            this.annotateWithNamesCheckBox.Name = "annotateWithNamesCheckBox";
            this.annotateWithNamesCheckBox.Size = new System.Drawing.Size(127, 17);
            this.annotateWithNamesCheckBox.TabIndex = 8;
            this.annotateWithNamesCheckBox.Text = "Annotate with Names";
            this.annotateWithNamesCheckBox.UseVisualStyleBackColor = true;
            // 
            // namesButton
            // 
            this.namesButton.Location = new System.Drawing.Point(140, 209);
            this.namesButton.Name = "namesButton";
            this.namesButton.Size = new System.Drawing.Size(122, 23);
            this.namesButton.TabIndex = 9;
            this.namesButton.Text = "Names...";
            this.namesButton.UseVisualStyleBackColor = true;
            this.namesButton.Click += new System.EventHandler(this.namesButton_Click);
            // 
            // useOldFormatCheckBox
            // 
            this.useOldFormatCheckBox.AutoSize = true;
            this.useOldFormatCheckBox.Location = new System.Drawing.Point(12, 245);
            this.useOldFormatCheckBox.Name = "useOldFormatCheckBox";
            this.useOldFormatCheckBox.Size = new System.Drawing.Size(200, 17);
            this.useOldFormatCheckBox.TabIndex = 10;
            this.useOldFormatCheckBox.Text = "Use Old Format (no message groups)";
            this.useOldFormatCheckBox.UseVisualStyleBackColor = true;
            // 
            // exportStringsButton
            // 
            this.exportStringsButton.Location = new System.Drawing.Point(11, 64);
            this.exportStringsButton.Name = "exportStringsButton";
            this.exportStringsButton.Size = new System.Drawing.Size(251, 23);
            this.exportStringsButton.TabIndex = 3;
            this.exportStringsButton.Text = "Export Strings...";
            this.exportStringsButton.UseVisualStyleBackColor = true;
            this.exportStringsButton.Click += new System.EventHandler(this.exportStringsButton_Click);
            // 
            // ExportImportTextNewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(275, 274);
            this.Controls.Add(this.exportStringsButton);
            this.Controls.Add(this.useOldFormatCheckBox);
            this.Controls.Add(this.namesButton);
            this.Controls.Add(this.annotateWithNamesCheckBox);
            this.Controls.Add(this.ExportMessagesButton);
            this.Controls.Add(this.exportMatchingStringsButton);
            this.Controls.Add(this.migratePatchButton);
            this.Controls.Add(this.importMessagesAndStringsButton);
            this.Controls.Add(this.exportMessagesAndStringsButton);
            this.Controls.Add(this.wordWrapSettingsButton);
            this.Controls.Add(this.useWordWrappingCheckBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "ExportImportTextNewForm";
            this.Text = "Import/Export Text";
            this.Load += new System.EventHandler(this.ExportImportTextNewForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox useWordWrappingCheckBox;
        private System.Windows.Forms.Button wordWrapSettingsButton;
        private System.Windows.Forms.Button exportMessagesAndStringsButton;
        private System.Windows.Forms.Button importMessagesAndStringsButton;
        private System.Windows.Forms.Button migratePatchButton;
        private System.Windows.Forms.Button exportMatchingStringsButton;
        private System.Windows.Forms.Button ExportMessagesButton;
        private System.Windows.Forms.CheckBox annotateWithNamesCheckBox;
        private System.Windows.Forms.Button namesButton;
        private System.Windows.Forms.CheckBox useOldFormatCheckBox;
        private System.Windows.Forms.Button exportStringsButton;
    }
}