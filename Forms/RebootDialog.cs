using System;
using System.Drawing;
using System.Windows.Forms;

namespace HmaxUnlocker.Forms
{
    /// <summary>
    /// نافذة اختيار نوع إعادة التشغيل: عادي / ريكفري / وضع الداونلود (Odin).
    /// </summary>
    public class RebootDialog : Form
    {
        /// <summary>يعيد "" لعادي، "recovery"، "download"، أو null عند الإلغاء.</summary>
        public static string Pick()
        {
            using (var dlg = new RebootDialog())
            {
                return dlg.ShowDialog() == DialogResult.OK ? dlg._choice : null;
            }
        }

        private string _choice;

        private RebootDialog()
        {
            Color clrBackground = Color.FromArgb(30, 30, 46);
            Color clrText = Color.FromArgb(205, 214, 244);
            Color clrAccent = Color.FromArgb(242, 143, 173);

            this.Text = "Reboot Device";
            this.Size = new Size(470, 170);
            this.BackColor = clrBackground;
            this.ForeColor = clrText;
            this.Font = new Font("Segoe UI", 9F);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var lbl = new Label();
            lbl.Text = "Choose reboot mode:";
            lbl.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lbl.ForeColor = clrAccent;
            lbl.Location = new Point(15, 12);
            lbl.AutoSize = true;
            this.Controls.Add(lbl);

            var btnNormal = MakeButton("Normal Reboot", 15, clrAccent);
            var btnRecovery = MakeButton("Recovery", 165, Color.FromArgb(137, 180, 250));
            var btnDownload = MakeButton("Download (Odin)", 315, Color.FromArgb(148, 226, 213));

            btnNormal.Click += (s, e) => Finish("");
            btnRecovery.Click += (s, e) => Finish("recovery");
            btnDownload.Click += (s, e) => Finish("download");
        }

        private void Finish(string choice)
        {
            _choice = choice;
            DialogResult = DialogResult.OK;
            Close();
        }

        private Button MakeButton(string text, int x, Color back)
        {
            var btn = new Button();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btn.Location = new Point(x, 45);
            btn.Size = new Size(140, 45);
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = back;
            btn.ForeColor = Color.FromArgb(30, 30, 46);
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
            this.Controls.Add(btn);
            return btn;
        }
    }
}
