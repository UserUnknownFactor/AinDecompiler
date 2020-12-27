namespace AinDecompiler
{
    partial class WordWrapForm2
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Word Wrap Modes", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewItem listViewItem3 = new System.Windows.Forms.ListViewItem("Default");
            System.Windows.Forms.ListViewItem listViewItem4 = new System.Windows.Forms.ListViewItem("Disable Wrapping");
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WordWrapForm2));
            this.listView1 = new System.Windows.Forms.ListView();
            this.columnHeader1 = new System.Windows.Forms.ColumnHeader();
            this.panel1 = new System.Windows.Forms.Panel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.addNodeToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.removeToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.codeTextBox = new System.Windows.Forms.TextBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.lblKanjiWidth = new System.Windows.Forms.Label();
            this.kanjiWidthTextBox = new System.Windows.Forms.TextBox();
            this.toolStrip2 = new System.Windows.Forms.ToolStrip();
            this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripDropDownButton();
            this.lblCode = new System.Windows.Forms.Label();
            this.charactersPerLineTextBox = new System.Windows.Forms.TextBox();
            this.selectFontButton = new System.Windows.Forms.Button();
            this.removeLineBreaksIfWordWrappingCheckBox = new System.Windows.Forms.CheckBox();
            this.maintainIndentationCheckBox = new System.Windows.Forms.CheckBox();
            this.useVariableWidthFontCheckBox = new System.Windows.Forms.CheckBox();
            this.lblCharactersPerLine = new System.Windows.Forms.Label();
            this.lblLinesPerMessage = new System.Windows.Forms.Label();
            this.linesPerMessageTextBox = new System.Windows.Forms.TextBox();
            this.panel4 = new System.Windows.Forms.Panel();
            this.neverUseWordWrapCheckBox = new System.Windows.Forms.CheckBox();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.applyButton = new System.Windows.Forms.Button();
            this.errorProvider1 = new System.Windows.Forms.ErrorProvider(this.components);
            this.panel1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel3.SuspendLayout();
            this.toolStrip2.SuspendLayout();
            this.panel4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).BeginInit();
            this.SuspendLayout();
            // 
            // listView1
            // 
            this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1});
            this.listView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listView1.FullRowSelect = true;
            listViewGroup2.Header = "Word Wrap Modes";
            listViewGroup2.Name = "listViewGroup1";
            this.listView1.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup2});
            this.listView1.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listView1.HideSelection = false;
            listViewItem3.Group = listViewGroup2;
            listViewItem4.Group = listViewGroup2;
            this.listView1.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            listViewItem3,
            listViewItem4});
            this.listView1.LabelEdit = true;
            this.listView1.Location = new System.Drawing.Point(0, 25);
            this.listView1.MultiSelect = false;
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(153, 260);
            this.listView1.TabIndex = 0;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            this.listView1.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.listView1_AfterLabelEdit);
            this.listView1.SelectedIndexChanged += new System.EventHandler(this.listView1_SelectedIndexChanged);
            this.listView1.BeforeLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.listView1_BeforeLabelEdit);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Width = 148;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.listView1);
            this.panel1.Controls.Add(this.toolStrip1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(153, 285);
            this.panel1.TabIndex = 3;
            // 
            // toolStrip1
            // 
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addNodeToolStripButton,
            this.removeToolStripButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(153, 25);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // addNodeToolStripButton
            // 
            this.addNodeToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.addNodeToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("addNodeToolStripButton.Image")));
            this.addNodeToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.addNodeToolStripButton.Name = "addNodeToolStripButton";
            this.addNodeToolStripButton.Size = new System.Drawing.Size(59, 22);
            this.addNodeToolStripButton.Text = "&Add mode";
            this.addNodeToolStripButton.Click += new System.EventHandler(this.addNodeToolStripButton_Click);
            // 
            // removeToolStripButton
            // 
            this.removeToolStripButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.removeToolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("removeToolStripButton.Image")));
            this.removeToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.removeToolStripButton.Name = "removeToolStripButton";
            this.removeToolStripButton.Size = new System.Drawing.Size(50, 22);
            this.removeToolStripButton.Text = "&Remove";
            this.removeToolStripButton.Click += new System.EventHandler(this.removeToolStripButton_Click);
            // 
            // codeTextBox
            // 
            this.codeTextBox.AcceptsReturn = true;
            this.codeTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.codeTextBox.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.codeTextBox.Location = new System.Drawing.Point(5, 0);
            this.codeTextBox.Multiline = true;
            this.codeTextBox.Name = "codeTextBox";
            this.codeTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.codeTextBox.Size = new System.Drawing.Size(289, 132);
            this.codeTextBox.TabIndex = 0;
            this.codeTextBox.Validating += new System.ComponentModel.CancelEventHandler(this.codeTextBox_Validating);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.codeTextBox);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(153, 148);
            this.panel2.Name = "panel2";
            this.panel2.Padding = new System.Windows.Forms.Padding(5, 0, 13, 5);
            this.panel2.Size = new System.Drawing.Size(307, 137);
            this.panel2.TabIndex = 21;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.lblKanjiWidth);
            this.panel3.Controls.Add(this.kanjiWidthTextBox);
            this.panel3.Controls.Add(this.toolStrip2);
            this.panel3.Controls.Add(this.lblCode);
            this.panel3.Controls.Add(this.charactersPerLineTextBox);
            this.panel3.Controls.Add(this.selectFontButton);
            this.panel3.Controls.Add(this.removeLineBreaksIfWordWrappingCheckBox);
            this.panel3.Controls.Add(this.maintainIndentationCheckBox);
            this.panel3.Controls.Add(this.useVariableWidthFontCheckBox);
            this.panel3.Controls.Add(this.lblCharactersPerLine);
            this.panel3.Controls.Add(this.lblLinesPerMessage);
            this.panel3.Controls.Add(this.linesPerMessageTextBox);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel3.Location = new System.Drawing.Point(153, 0);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(307, 148);
            this.panel3.TabIndex = 0;
            // 
            // lblKanjiWidth
            // 
            this.lblKanjiWidth.AutoSize = true;
            this.lblKanjiWidth.Location = new System.Drawing.Point(250, 41);
            this.lblKanjiWidth.Name = "lblKanjiWidth";
            this.lblKanjiWidth.Size = new System.Drawing.Size(57, 13);
            this.lblKanjiWidth.TabIndex = 11;
            this.lblKanjiWidth.Text = "kanji width";
            // 
            // kanjiWidthTextBox
            // 
            this.kanjiWidthTextBox.Location = new System.Drawing.Point(252, 57);
            this.kanjiWidthTextBox.Name = "kanjiWidthTextBox";
            this.kanjiWidthTextBox.Size = new System.Drawing.Size(52, 20);
            this.kanjiWidthTextBox.TabIndex = 10;
            this.kanjiWidthTextBox.Validating += new System.ComponentModel.CancelEventHandler(this.kanjiWidthTextBox_Validating);
            // 
            // toolStrip2
            // 
            this.toolStrip2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.toolStrip2.Dock = System.Windows.Forms.DockStyle.None;
            this.toolStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripDropDownButton1});
            this.toolStrip2.Location = new System.Drawing.Point(216, 0);
            this.toolStrip2.Name = "toolStrip2";
            this.toolStrip2.Size = new System.Drawing.Size(91, 25);
            this.toolStrip2.TabIndex = 9;
            this.toolStrip2.Text = "toolStrip2";
            // 
            // toolStripDropDownButton1
            // 
            this.toolStripDropDownButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripDropDownButton1.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
            this.toolStripDropDownButton1.Size = new System.Drawing.Size(79, 22);
            this.toolStripDropDownButton1.Text = "Select Game";
            // 
            // lblCode
            // 
            this.lblCode.AutoSize = true;
            this.lblCode.Location = new System.Drawing.Point(3, 129);
            this.lblCode.Name = "lblCode";
            this.lblCode.Size = new System.Drawing.Size(299, 13);
            this.lblCode.TabIndex = 8;
            this.lblCode.Text = "Code to trigger selecting this &mode: (Separate with blank lines)";
            // 
            // charactersPerLineTextBox
            // 
            this.charactersPerLineTextBox.Location = new System.Drawing.Point(114, 33);
            this.charactersPerLineTextBox.Name = "charactersPerLineTextBox";
            this.charactersPerLineTextBox.Size = new System.Drawing.Size(61, 20);
            this.charactersPerLineTextBox.TabIndex = 3;
            this.charactersPerLineTextBox.Validating += new System.ComponentModel.CancelEventHandler(this.charactersPerLineTextBox_Validating);
            // 
            // selectFontButton
            // 
            this.selectFontButton.Location = new System.Drawing.Point(155, 55);
            this.selectFontButton.Name = "selectFontButton";
            this.selectFontButton.Size = new System.Drawing.Size(95, 23);
            this.selectFontButton.TabIndex = 5;
            this.selectFontButton.Text = "Select &Font...";
            this.selectFontButton.UseVisualStyleBackColor = true;
            this.selectFontButton.Click += new System.EventHandler(this.selectFontButton_Click);
            // 
            // removeLineBreaksIfWordWrappingCheckBox
            // 
            this.removeLineBreaksIfWordWrappingCheckBox.AutoSize = true;
            this.removeLineBreaksIfWordWrappingCheckBox.Location = new System.Drawing.Point(8, 105);
            this.removeLineBreaksIfWordWrappingCheckBox.Name = "removeLineBreaksIfWordWrappingCheckBox";
            this.removeLineBreaksIfWordWrappingCheckBox.Size = new System.Drawing.Size(251, 17);
            this.removeLineBreaksIfWordWrappingCheckBox.TabIndex = 7;
            this.removeLineBreaksIfWordWrappingCheckBox.Text = "Remove Line &Breaks if text wrapped to next line";
            this.removeLineBreaksIfWordWrappingCheckBox.UseVisualStyleBackColor = true;
            // 
            // maintainIndentationCheckBox
            // 
            this.maintainIndentationCheckBox.AutoSize = true;
            this.maintainIndentationCheckBox.Location = new System.Drawing.Point(8, 82);
            this.maintainIndentationCheckBox.Name = "maintainIndentationCheckBox";
            this.maintainIndentationCheckBox.Size = new System.Drawing.Size(204, 17);
            this.maintainIndentationCheckBox.TabIndex = 6;
            this.maintainIndentationCheckBox.Text = "&Maintain Indentation after 「 or spaces";
            this.maintainIndentationCheckBox.UseVisualStyleBackColor = true;
            // 
            // useVariableWidthFontCheckBox
            // 
            this.useVariableWidthFontCheckBox.AutoSize = true;
            this.useVariableWidthFontCheckBox.Location = new System.Drawing.Point(8, 59);
            this.useVariableWidthFontCheckBox.Name = "useVariableWidthFontCheckBox";
            this.useVariableWidthFontCheckBox.Size = new System.Drawing.Size(141, 17);
            this.useVariableWidthFontCheckBox.TabIndex = 4;
            this.useVariableWidthFontCheckBox.Text = "Use &Variable-Width Font";
            this.useVariableWidthFontCheckBox.UseVisualStyleBackColor = true;
            // 
            // lblCharactersPerLine
            // 
            this.lblCharactersPerLine.AutoSize = true;
            this.lblCharactersPerLine.Location = new System.Drawing.Point(10, 36);
            this.lblCharactersPerLine.Name = "lblCharactersPerLine";
            this.lblCharactersPerLine.Size = new System.Drawing.Size(98, 13);
            this.lblCharactersPerLine.TabIndex = 2;
            this.lblCharactersPerLine.Text = "&Characters per line:";
            // 
            // lblLinesPerMessage
            // 
            this.lblLinesPerMessage.AutoSize = true;
            this.lblLinesPerMessage.Location = new System.Drawing.Point(8, 10);
            this.lblLinesPerMessage.Name = "lblLinesPerMessage";
            this.lblLinesPerMessage.Size = new System.Drawing.Size(100, 13);
            this.lblLinesPerMessage.TabIndex = 0;
            this.lblLinesPerMessage.Text = "&Lines Per Message:";
            // 
            // linesPerMessageTextBox
            // 
            this.linesPerMessageTextBox.Location = new System.Drawing.Point(114, 7);
            this.linesPerMessageTextBox.Name = "linesPerMessageTextBox";
            this.linesPerMessageTextBox.Size = new System.Drawing.Size(61, 20);
            this.linesPerMessageTextBox.TabIndex = 1;
            this.linesPerMessageTextBox.Validating += new System.ComponentModel.CancelEventHandler(this.linesPerMessageTextBox_Validating);
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.neverUseWordWrapCheckBox);
            this.panel4.Controls.Add(this.okButton);
            this.panel4.Controls.Add(this.cancelButton);
            this.panel4.Controls.Add(this.applyButton);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel4.Location = new System.Drawing.Point(0, 285);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(460, 32);
            this.panel4.TabIndex = 1;
            // 
            // neverUseWordWrapCheckBox
            // 
            this.neverUseWordWrapCheckBox.AutoSize = true;
            this.neverUseWordWrapCheckBox.Location = new System.Drawing.Point(38, 4);
            this.neverUseWordWrapCheckBox.Name = "neverUseWordWrapCheckBox";
            this.neverUseWordWrapCheckBox.Size = new System.Drawing.Size(167, 17);
            this.neverUseWordWrapCheckBox.TabIndex = 22;
            this.neverUseWordWrapCheckBox.Text = "Do not word wrap game script";
            this.neverUseWordWrapCheckBox.UseVisualStyleBackColor = true;
            this.neverUseWordWrapCheckBox.CheckedChanged += new System.EventHandler(this.neverUseWordWrapCheckBox_CheckedChanged);
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.Location = new System.Drawing.Point(211, 0);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 0;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(292, 0);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 1;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // applyButton
            // 
            this.applyButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.applyButton.Location = new System.Drawing.Point(373, 0);
            this.applyButton.Name = "applyButton";
            this.applyButton.Size = new System.Drawing.Size(75, 23);
            this.applyButton.TabIndex = 2;
            this.applyButton.Text = "Apply";
            this.applyButton.UseVisualStyleBackColor = true;
            this.applyButton.Click += new System.EventHandler(this.applyButton_Click);
            // 
            // errorProvider1
            // 
            this.errorProvider1.ContainerControl = this;
            // 
            // WordWrapForm2
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(460, 317);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel3);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.panel4);
            this.MinimumSize = new System.Drawing.Size(465, 232);
            this.Name = "WordWrapForm2";
            this.Text = "Word Wrap Settings";
            this.Load += new System.EventHandler(this.WordWrapForm2_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.toolStrip2.ResumeLayout(false);
            this.toolStrip2.PerformLayout();
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton addNodeToolStripButton;
        private System.Windows.Forms.ToolStripButton removeToolStripButton;
        private System.Windows.Forms.TextBox codeTextBox;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label lblCode;
        private System.Windows.Forms.TextBox charactersPerLineTextBox;
        private System.Windows.Forms.Button selectFontButton;
        private System.Windows.Forms.CheckBox removeLineBreaksIfWordWrappingCheckBox;
        private System.Windows.Forms.CheckBox maintainIndentationCheckBox;
        private System.Windows.Forms.CheckBox useVariableWidthFontCheckBox;
        private System.Windows.Forms.Label lblCharactersPerLine;
        private System.Windows.Forms.Label lblLinesPerMessage;
        private System.Windows.Forms.TextBox linesPerMessageTextBox;
        private System.Windows.Forms.ToolStrip toolStrip2;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
        private System.Windows.Forms.Panel panel4;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button applyButton;
        private System.Windows.Forms.CheckBox neverUseWordWrapCheckBox;
        private System.Windows.Forms.ErrorProvider errorProvider1;
        private System.Windows.Forms.Label lblKanjiWidth;
        private System.Windows.Forms.TextBox kanjiWidthTextBox;

    }
}