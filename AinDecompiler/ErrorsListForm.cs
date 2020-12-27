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
    public partial class ErrorsListForm : Form
    {
        int lastErrorCount = 0;

        public ErrorsListForm()
        {
            InitializeComponent();
        }

        public void SetErrorList(IList<string> errors)
        {
            if (errors.Count > lastErrorCount)
            {
                for (int i = lastErrorCount; i < errors.Count; i++)
                {
                    AddErrorMessage(errors[i]);
                }
                lastErrorCount = errors.Count;
            }
        }

        public void AddErrorMessage(string errorMessage)
        {
            scintilla1.AppendText(errorMessage + Environment.NewLine);
        }

        private void ErrorsListForm_Load(object sender, EventArgs e)
        {

        }
    }
}
