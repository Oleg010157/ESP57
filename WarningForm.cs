using System.Windows.Forms;

namespace ICDIBasic
{
    public partial class MyMsgBox : Form
    {
        public MyMsgBox(string txt)
        {
            InitializeComponent();

            this.Text = txt;
        }


        public void Show(string txt)
        {
            label1.Text = txt;
            this.Show();
        }


        public bool isClosed = false;
        private void MyMsgBox_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.isClosed = true;
            this.Visible = false;
            e.Cancel = true;

        }
    }
}
