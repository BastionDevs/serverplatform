using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace servermgr
{
    public partial class Form2 : Form
    {
        Form1 form1;

        public Form2()
        {
            InitializeComponent();
        }

        public Form2(string user, string token, Form1 form)
        {
            InitializeComponent();
            form1 = form;
        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }

        private void pictureBox2_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button is MouseButtons.Left)
            {
                pictureBox2.ContextMenuStrip.Show(pictureBox2, new Point(e.X, e.Y));
            }
        }

        private void signOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            form1.email = "";
            form1.pwd = "";
            form1.token = "";

            form1.ShowLoginScreen();
            this.Close();
        }
    }
}
