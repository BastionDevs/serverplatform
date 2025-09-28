using System;
using System.Drawing;
using System.Windows.Forms;
using installer.Properties;

namespace installer
{
    public partial class Page2 : UserControl
    {
        private readonly Image[] _images =
        {
            Resources.bastion_original_on_white, Resources.CubeNotFound_250, Resources.thekiwiii_250, Resources.sp_logo_square
        };

        private int _imageIndex;

        private Timer _timer1;

        public Page2()
        {
            InitializeComponent();
            InitializeTimer();
        }

        public event Action<string> SwitchForm;

        private void InitializeTimer()
        {
            _timer1 = new Timer();
            _timer1.Interval = 1750;
            _timer1.Tick += timer1_Tick;
            _timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // Change to the next image
            _imageIndex = (_imageIndex + 1) % _images.Length;
            pictureBox1.Image = _images[_imageIndex];
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SwitchForm?.Invoke("Form1");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SwitchForm?.Invoke("Form3");
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked) radioButton2.Checked = false;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked) radioButton2.Checked = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var dr = MessageBox.Show("Are you sure you want to exit Setup?", "", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (dr == DialogResult.Yes) Application.Exit();
        }
    }
}