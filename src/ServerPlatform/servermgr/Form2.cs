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

        private string user;
        private string token;

        public Form2()
        {
            InitializeComponent();
        }

        public Form2(string userstring, string tokenstring, Form1 form)
        {
            InitializeComponent();
            form1 = form;
            user = userstring;
            token = tokenstring;
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
            BackendAuth.signOut(user.Split('@')[1], token);

            form1.email = "";
            form1.pwd = "";
            form1.token = "";

            form1.ShowLoginScreen(form1);
            this.Close();
        }
    }
}
