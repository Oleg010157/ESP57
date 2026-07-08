using System;
using System.Drawing;
using System.Windows.Forms;

namespace ICDIBasic
{
    public class BootProgressForm : Form
    {
        private readonly ProgressBar _pb;
        private readonly Label _lbl;
        // Throttle UI updates to avoid flooding the UI thread when data arrives fast
        private readonly int _minUpdateMs = 100; // minimum interval between UI updates
        private DateTime _lastUpdate = DateTime.MinValue;
        private int _lastPercent = -1;

        public BootProgressForm()
        {
            Text = "Flash Erase";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(360, 110);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _lbl = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top,
                Height = 24,
                Padding = new Padding(8, 8, 8, 0),
                Text = "Preparing..."
            };

            _pb = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            Controls.Add(_lbl);
            Controls.Add(_pb);
        }

        public void SetProgress(int percent, string status = null)
        {
            if (percent < _pb.Minimum) percent = _pb.Minimum;
            if (percent > _pb.Maximum) percent = _pb.Maximum;

            // Use non-blocking marshal to UI thread to avoid slowing data producer threads
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetProgress(percent, status)));
                return;
            }

            // Throttle frequent updates: if nothing changed and interval too small, skip
            var now = DateTime.UtcNow;
            if (percent == _lastPercent && string.IsNullOrEmpty(status) && (now - _lastUpdate).TotalMilliseconds < _minUpdateMs)
                return;
            _lastUpdate = now;
            _lastPercent = percent;

            // Avoid unnecessary assignments that force redraws
            if (_pb.Value != percent)
                _pb.Value = percent;

            if (!string.IsNullOrEmpty(status))
            {
                if (_lbl.Text != status)
                    _lbl.Text = status;
            }
        }
    }
}
