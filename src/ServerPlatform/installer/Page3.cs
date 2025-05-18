using System;
using System.Drawing;
using System.Windows.Forms;
using installer.Properties;

namespace installer
{
    public partial class Page3 : UserControl
    {
        private readonly Image[] images =
        {
            Resources.bastion_original_on_white, Resources.CubeNotFound_250
        };

        private int imageIndex;

        private Timer timer1;

        public Page3()
        {
            InitializeComponent();
            InitializeTimer();
        }

        public event Action<string> SwitchForm;

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
            var dr = MessageBox.Show("Are you sure you want to exit Setup?", "", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (dr == DialogResult.Yes) Application.Exit();
        }
    }
}