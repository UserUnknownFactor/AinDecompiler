﻿namespace AinDecompiler
{
    partial class ErrorsListForm
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
            this.scintilla1 = new ScintillaNET.Scintilla();
            ((System.ComponentModel.ISupportInitialize)(this.scintilla1)).BeginInit();
            this.SuspendLayout();
            // 
            // scintilla1
            // 
            this.scintilla1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.scintilla1.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.scintilla1.Location = new System.Drawing.Point(0, 0);
            this.scintilla1.Name = "scintilla1";
            this.scintilla1.Size = new System.Drawing.Size(560, 287);
            this.scintilla1.Styles.BraceBad.Size = 9F;
            this.scintilla1.Styles.BraceLight.Size = 9F;
            this.scintilla1.Styles.ControlChar.Size = 9F;
            this.scintilla1.Styles.Default.BackColor = System.Drawing.SystemColors.Window;
            this.scintilla1.Styles.Default.Size = 9F;
            this.scintilla1.Styles.IndentGuide.Size = 9F;
            this.scintilla1.Styles.LastPredefined.Size = 9F;
            this.scintilla1.Styles.LineNumber.Size = 9F;
            this.scintilla1.Styles.Max.Size = 9F;
            this.scintilla1.TabIndex = 1;
            // 
            // ErrorsListForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(560, 287);
            this.Controls.Add(this.scintilla1);
            this.Name = "ErrorsListForm";
            this.Text = "Errors List";
            this.Load += new System.EventHandler(this.ErrorsListForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.scintilla1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private ScintillaNET.Scintilla scintilla1;

    }
}