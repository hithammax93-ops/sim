using System;
using System.Drawing;
using System.Windows.Forms;

namespace HmaxUnlocker.Forms
{
    /// <summary>
    /// نافذة إدخال بسيطة لتحديد المسار الوجهة للملف على الجهاز.
    /// </summary>
    public class PromptDialog : Form
    {
        private readonly TextBox _textBox;

        public string Value => _textBox.Text;

        private PromptDialog(string title, string promptText, string defaultValue)
        {
            Color clrBackground = Color.FromArgb(30, 30, 46);
            Color clrPanel = Color.FromArgb(49, 50, 68);
            Color clrText = Color.FromArgb(205, 214, 244);
            Color clrAccent = Color.FromArgb(250, 179, 135);
            Color clrInputBg = Color.FromArgb(23, 23, 36);

            this.Text = title;
            this.Size = new Size(560, 190);
            this.BackColor = clrBackground;
            this.ForeColor = clrText;
            this.Font = new Font("Segoe UI", 9F);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var lbl = new Label();
            lbl.Text = promptText;
            lbl.Location = new Point(15, 12);
            lbl.AutoSize = true;
            this.Controls.Add(lbl);

            _textBox = new TextBox();
            _textBox.Text = defaultValue ?? "";
            _textBox.Location = new Point(15, 38);
            _textBox.Size = new Size(510, 25);
            _textBox.BackColor = clrInputBg;
            _textBox.ForeColor = clrText;
            _textBox.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(_textBox);

            var btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnOk.Location = new Point(15, 80);
            btnOk.Size = new Size(245, 38);
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.BackColor = clrAccent;
            btnOk.ForeColor = clrBackground;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.DialogResult = DialogResult.OK;
            this.Controls.Add(btnOk);

            var btnCancel = new Button();
            btnCancel.Text = "CANCEL";
            btnCancel.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnCancel.Location = new Point(280, 80);
            btnCancel.Size = new Size(245, 38);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.BackColor = Color.FromArgb(69, 71, 90);
            btnCancel.ForeColor = clrText;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        public static string Show(string title, string promptText, string defaultValue)
        {
            using (var dlg = new PromptDialog(title, promptText, defaultValue))
            {
                return dlg.ShowDialog() == DialogResult.OK ? dlg.Value : null;
            }
        }
    }
}
