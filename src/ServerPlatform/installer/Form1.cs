using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace installer
{
    public partial class Form1 : Form
    {
        private Dictionary<string, UserControl> forms = new Dictionary<string, UserControl>();

        public Form1()
        {
            InitializeComponent();
            InitializeForms();
            LoadForm("Form1"); // Start with Form1
        }

        private void InitializeForms()
        {
            // Create instances of UserControls
            var form1 = new Page1();
            var form2 = new Page2();
            var form3 = new Page3();

            // Add to dictionary
            forms["Form1"] = form1;
            forms["Form2"] = form2;
            forms["Form3"] = form3;

            // Subscribe to SwitchForm events
            form1.SwitchForm += LoadForm;
            form2.SwitchForm += LoadForm;
            form3.SwitchForm += LoadForm;
        }

        private void LoadForm(string formName)
        {
            if (forms.ContainsKey(formName))
            {
                panelContainer.Controls.Clear();
                panelContainer.Controls.Add(forms[formName]);
            }
        }
    }
}
