using System.Drawing;
using System.Windows.Forms;

namespace Executioner
{
    public partial class Message : Form
    {
        public Message(string message)
        {
            InitializeComponent();
            messageTextBox.Text = message;
            messageTextBox.Location = new Point(ClientSize.Width / 2 - messageTextBox.Size.Width / 2,
                                                ClientSize.Height / 2 - messageTextBox.Size.Height / 2);
            messageTextBox.Anchor = AnchorStyles.None;
            messageTextBox.Dock = DockStyle.Fill;
        }
    }
}
