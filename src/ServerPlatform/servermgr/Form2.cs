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

        private bool isProgrammaticClose = false;

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

            isProgrammaticClose = true;
            this.Close();
        }

        private void Form2_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = panel1.Bounds;

            // Draw the "border-bottom" effect
            using (Pen borderPen = new Pen(Color.FromArgb(224, 224, 224)))
            {
                e.Graphics.DrawLine(borderPen, rect.Left, rect.Bottom, rect.Right, rect.Bottom);
            }

            // Draw a soft shadow using alpha blending
            for (int i = 1; i <= 4; i++) // 4px fade
            {
                int alpha = (int)(30 - (i * 6)); // gradually fade out
                using (Pen shadowPen = new Pen(Color.FromArgb(alpha, 0, 0, 0)))
                {
                    e.Graphics.DrawLine(
                        shadowPen,
                        rect.Left,
                        rect.Bottom + i,
                        rect.Right,
                        rect.Bottom + i
                    );
                }
            }
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !isProgrammaticClose)
            {
                BackendAuth.signOut(user.Split('@')[1], token);
                Application.Exit();
            }
        }
    }
}
