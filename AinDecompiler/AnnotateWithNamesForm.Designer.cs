namespace AinDecompiler
{
    partial class AnnotateWithNamesForm
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
            this.useEnumerationTypeCheckBox = new System.Windows.Forms.CheckBox();
            this.enumComboBox = new System.Windows.Forms.ComboBox();
            this.useFunctionParametersCheckBox = new System.Windows.Forms.CheckBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.importToolStripButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.cancelButton = new System.Windows.Forms.Button();
            this.okButton = new System.Windows.Forms.Button();
            this.variableNamesTextBox = new System.Windows.Forms.TextBox();
            this.substitutionTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // useEnumerationTypeCheckBox
            // 
            this.useEnumerationTypeCheckBox.AutoSize = true;
            this.useEnumerationTypeCheckBox.Location = new System.Drawing.Point(12, 8);
            this.useEnumerationTypeCheckBox.Name = "useEnumerationTypeCheckBox";
            this.useEnumerationTypeCheckBox.Size = new System.Drawing.Size(147, 17);
            this.useEnumerationTypeCheckBox.TabIndex = 0;
            this.useEnumerationTypeCheckBox.Text = "Use an enumeration type:";
            this.useEnumerationTypeCheckBox.UseVisualStyleBackColor = true;
            // 
            // enumComboBox
            // 
            this.enumComboBox.FormattingEnabled = true;
            this.enumComboBox.Location = new System.Drawing.Point(12, 31);
            this.enumComboBox.Name = "enumComboBox";
            this.enumComboBox.Size = new System.Drawing.Size(268, 21);
            this.enumComboBox.TabIndex = 1;
            // 
            // useFunctionParametersCheckBox
            // 
            this.useFunctionParametersCheckBox.AutoSize = true;
            this.useFunctionParametersCheckBox.Location = new System.Drawing.Point(12, 58);
            this.useFunctionParametersCheckBox.Name = "useFunctionParametersCheckBox";
            this.useFunctionParametersCheckBox.Size = new System.Drawing.Size(275, 17);
            this.useFunctionParametersCheckBox.TabIndex = 2;
            this.useFunctionParametersCheckBox.Text = "Use function parameters: \"funcName.paramNumber\"";
            this.useFunctionParametersCheckBox.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.toolStrip1);
            this.panel1.Location = new System.Drawing.Point(478, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(89, 28);
            this.panel1.TabIndex = 7;
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importToolStripButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(89, 25);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // importToolStripButton
            // 
            this.importToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.importToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.importToolStripButton.Name = "importToolStripButton";
            this.importToolStripButton.Size = new System.Drawing.Size(52, 22);
            this.importToolStripButton.Text = "Import";
            this.importToolStripButton.DropDownOpening += new System.EventHandler(this.importToolStripButton_DropDownOpening);
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(205, 253);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 9;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // okButton
            // 
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(124, 253);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 10;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // variableNamesTextBox
            // 
            this.variableNamesTextBox.AcceptsReturn = true;
            this.variableNamesTextBox.Location = new System.Drawing.Point(12, 81);
            this.variableNamesTextBox.Multiline = true;
            this.variableNamesTextBox.Name = "variableNamesTextBox";
            this.variableNamesTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.variableNamesTextBox.Size = new System.Drawing.Size(268, 166);
            this.variableNamesTextBox.TabIndex = 11;
            // 
            // substitutionTextBox
            // 
            this.substitutionTextBox.AcceptsReturn = true;
            this.substitutionTextBox.AcceptsTab = true;
            this.substitutionTextBox.Location = new System.Drawing.Point(286, 81);
            this.substitutionTextBox.Multiline = true;
            this.substitutionTextBox.Name = "substitutionTextBox";
            this.substitutionTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.substitutionTextBox.Size = new System.Drawing.Size(268, 166);
            this.substitutionTextBox.TabIndex = 12;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(283, 59);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(254, 13);
            this.label1.TabIndex = 13;
            this.label1.Text = "String Substitution Table (text, tab, replacementText)";
            // 
            // AnnotateWithNamesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(567, 288);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.substitutionTextBox);
            this.Controls.Add(this.variableNamesTextBox);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.useFunctionParametersCheckBox);
            this.Controls.Add(this.enumComboBox);
            this.Controls.Add(this.useEnumerationTypeCheckBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "AnnotateWithNamesForm";
            this.Text = "Character Names";
            this.Load += new System.EventHandler(this.AnnotateWithNamesForm_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox useEnumerationTypeCheckBox;
        private System.Windows.Forms.ComboBox enumComboBox;
        private System.Windows.Forms.CheckBox useFunctionParametersCheckBox;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripDropDownButton importToolStripButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.TextBox variableNamesTextBox;
        private System.Windows.Forms.TextBox substitutionTextBox;
        private System.Windows.Forms.Label label1;
    }
}