using System;
using System.Drawing;
using System.Windows.Forms;

namespace HmaxUnlocker.Engine.Logging
{
    public class WinFormsLogger : ILogger
    {
        private readonly RichTextBox _textBox;

        public WinFormsLogger(RichTextBox textBox)
        {
            _textBox = textBox;
        }

        public void Info(string msg)
        {
            AppendColored("[INFO]  " + DateTime.Now.ToString("HH:mm:ss") + " " + msg,
                Color.FromArgb(205, 214, 244));
        }

        public void Warn(string msg)
        {
            AppendColored("[WARN]  " + DateTime.Now.ToString("HH:mm:ss") + " " + msg,
                Color.FromArgb(249, 226, 175));
        }

        public void Error(string msg)
        {
            AppendColored("[ERROR] " + DateTime.Now.ToString("HH:mm:ss") + " " + msg,
                Color.FromArgb(243, 139, 168));
        }

        public void Debug(string msg)
        {
            AppendColored("[DEBUG] " + DateTime.Now.ToString("HH:mm:ss") + " " + msg,
                Color.FromArgb(166, 173, 200));
        }

        public void Log(string msg)
        {
            Info(msg);
        }

        private void AppendColored(string message, Color color)
        {
            if (_textBox == null) return;

            if (_textBox.InvokeRequired)
            {
                _textBox.Invoke(new Action(() => AppendColored(message, color)));
                return;
            }

            _textBox.SelectionStart = _textBox.TextLength;
            _textBox.SelectionLength = 0;
            _textBox.SelectionColor = color;
            _textBox.AppendText(message + Environment.NewLine);
            _textBox.ScrollToCaret();
        }
    }
}