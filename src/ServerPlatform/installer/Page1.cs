using System;
using System.Windows.Forms;

namespace installer
{
    public partial class Page1 : UserControl
    {
        public Page1()
        {
            InitializeComponent();
        }

        public event Action<string> SwitchForm;

        private void button1_Click(object sender, EventArgs e)
        {
            SwitchForm?.Invoke("Form2");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var dr = MessageBox.Show("Are you sure you want to exit Setup?", "", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (dr == DialogResult.Yes) Application.Exit();
        }
    }
}