using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace servermgr
{
    public partial class Form1 : Form
    {
        private Timer resizeTimer;
        private Size targetSize;
        private Size originalSize;
        private int resizeSteps = 10; // Number of steps for smooth resizing
        private int currentStep = 0;

        private bool transitioningToForm2 = false;

        private Size originalFormSize;

        public string email;
        public string pwd;

        public string token;

        public Form1()
        {
            InitializeComponent();
            InitializeSmoothResize();
        }

        private void InitializeSmoothResize()
        {
            resizeTimer = new Timer();
            resizeTimer.Interval = 10; // Timer interval in milliseconds
            resizeTimer.Tick += ResizeTimer_Tick;
        }

        private void ResizeTimer_Tick(object sender, EventArgs e)
        {
            if (currentStep < resizeSteps)
            {
                int width = originalSize.Width + (targetSize.Width - originalSize.Width) * currentStep / resizeSteps;
                int height = originalSize.Height + (targetSize.Height - originalSize.Height) * currentStep / resizeSteps;
                this.Size = new Size(width, height);
                this.CenterToScreen();
                currentStep++;
            }
            else
            {
                resizeTimer.Stop();
                this.Size = targetSize; // Ensure final size is set
                this.CenterToScreen();

                if (transitioningToForm2)
                {
                    transitioningToForm2 = false;
                    OpenForm2();
                }
            }
        }

        public void ShowLoginScreen(Form1 form1)
        {
            // Reset fields
            email = null;
            pwd = null;
            token = null;

            // Navigate back to login page
            string curDir = Directory.GetCurrentDirectory();
            webBrowser1.Navigate(String.Format("file:///{0}/html/login.html", curDir));

            // Show and resize back if needed
            StartSmoothResize(form1.originalFormSize);
            form1.CenterToScreen();
            form1.Show();
        }

        private void OpenForm2()
        {
            Form2 form2 = new Form2(email.Split('@')[0], token, this); // Pass email/pwd here if needed
            form2.StartPosition = FormStartPosition.Manual;
            form2.Location = this.Location;
            form2.Size = this.Size;
            form2.Show();

            this.Hide();
        }

        private void StartSmoothResize(Size newSize)
        {
            targetSize = newSize;
            originalSize = this.Size;
            currentStep = 0;
            resizeTimer.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (originalFormSize != null)
            {
                originalFormSize = this.Size;
            }
            string curDir = Directory.GetCurrentDirectory();
            webBrowser1.Navigate(String.Format("file:///{0}/html/login.html", curDir));
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            HtmlElement btnunifiedSignIn = webBrowser1.Document.GetElementById("unifiedSignIn");
            if (btnunifiedSignIn != null)
            {
                btnunifiedSignIn.Click += btnunifiedSignIn_Click;
            }
        }

        private void btnunifiedSignIn_Click(object sender, HtmlElementEventArgs e)
        {
            if (!webBrowser1.Url.ToString().Contains("loginPwd.html"))
            {
                HtmlElement emailInput = webBrowser1.Document.GetElementById("unifiedUsername");
                if (emailInput != null)
                {
                    email = emailInput.GetAttribute("value");
                }
            }
            else
            {
                HtmlElement pwdInput = webBrowser1.Document.GetElementById("unifiedPassword");
                if (pwdInput != null)
                {
                    pwd = pwdInput.GetAttribute("value");
                }

                e.ReturnValue = false;

                if (!email.Contains("@"))
                {
                    email += "@localhost";
                }

                webBrowser1.Document.InvokeScript("redirLoad");
                StartSmoothResize(new Size(720, 481));

                token = BackendAuth.getAuthToken(email, pwd);
                if (token.StartsWith("ERROR-AuthFailure"))
                {
                    webBrowser1.Navigate(String.Format("file:///{0}/html/login.html", Directory.GetCurrentDirectory()));
                    StartSmoothResize(this.Size);
                } else
                {
                    transitioningToForm2 = true;
                    StartSmoothResize(new Form2().Size);
                }
            }
        }

        private void quitServerPlatformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
