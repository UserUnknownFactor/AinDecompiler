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
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();
            label1.Text = 
            "AIN Decompiler" + Environment.NewLine +
            "Version " + Application.ProductVersion + Environment.NewLine +
            "By SomeLoliCatgirl";
        }
    }
}
