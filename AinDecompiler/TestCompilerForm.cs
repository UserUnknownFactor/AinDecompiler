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
    public partial class TestCompilerForm : Form
    {
        AinFile ainFile;

        public TestCompilerForm(AinFile ainFile)
        {
            this.ainFile = ainFile;
            InitializeComponent();
        }

        public TestCompilerForm()
            : this(new AinFile())
        {

        }

        private void TestCompilerForm_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            byte[] codeBytes;
            textBox2.Text = Compiler.Compiler.ComplieCode(ainFile, textBox1.Text, checkBox1.Checked, out codeBytes);
        }
    }
}
