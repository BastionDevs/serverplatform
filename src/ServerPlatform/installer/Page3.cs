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
    public partial class Page3 : UserControl
    {
        public event Action<string> SwitchForm;

        private Timer timer1;
        private int imageIndex = 0;
        private Image[] images = new Image[]
        {
            Properties.Resources.bastion_original_on_white,
            Properties.Resources.CubeNotFound_250
        };

        public Page3()
        {
            InitializeComponent();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            timer1 = new Timer();
            timer1.Interval = 1500;
            timer1.Tick += timer1_Tick;
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // Change to the next image
            imageIndex = (imageIndex + 1) % images.Length;
            pictureBox1.Image = images[imageIndex];
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SwitchForm?.Invoke("Form2");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            DialogResult dr = MessageBox.Show("Are you sure you want to exit Setup?", "", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (dr == DialogResult.Yes) { Application.Exit(); }
        }
    }
}
