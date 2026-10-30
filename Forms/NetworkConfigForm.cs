using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HmaxUnlocker.Engine.Models;
using HmaxUnlocker.Engine.Network;

namespace HmaxUnlocker
{
    public class NetworkConfigForm : System.Windows.Forms.Form
    {
        private readonly MccMncDatabase _db;
        private readonly NetworkStore _store;
        private ListBox lstCountries;
        private ListBox lstNetworks;
        private ListBox lstSelected;
        private Button btnAdd;
        private Button btnRemove;
        private Button btnSave;
        private Button btnClose;
        private Label lblCountryHeader;
        private Label lblNetworkHeader;
        private Label lblSelectedHeader;

        public event EventHandler NetworksSaved;

        public NetworkConfigForm(MccMncDatabase db, NetworkStore store)
        {
            _db = db;
            _store = store;
            InitializeComponents();
            LoadCountries();
            RefreshSelectedList();
        }

        private void InitializeComponents()
        {
            Color clrBackground = Color.FromArgb(30, 30, 46);
            Color clrPanel = Color.FromArgb(49, 50, 68);
            Color clrTextPrimary = Color.FromArgb(205, 214, 244);
            Color clrAccentBlue = Color.FromArgb(137, 180, 250);
            Color clrAccentRed = Color.FromArgb(243, 139, 168);
            Color clrAccentGreen = Color.FromArgb(166, 227, 161);
            Color clrAccentTeal = Color.FromArgb(148, 226, 213);
            Color clrInputBg = Color.FromArgb(23, 23, 36);

            this.Text = "Network Config - MCC/MNC Selector";
            this.Size = new Size(750, 550);
            this.BackColor = clrBackground;
            this.ForeColor = clrTextPrimary;
            this.Font = new Font("Segoe UI", 9F);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // Title
            var lblTitle = new Label();
            lblTitle.Text = "MCC/MNC Network Selector";
            lblTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblTitle.ForeColor = clrAccentRed;
            lblTitle.Location = new Point(15, 10);
            lblTitle.AutoSize = true;
            this.Controls.Add(lblTitle);

            // Countries
            lblCountryHeader = new Label();
            lblCountryHeader.Text = "Countries";
            lblCountryHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCountryHeader.ForeColor = clrAccentBlue;
            lblCountryHeader.Location = new Point(15, 45);
            lblCountryHeader.AutoSize = true;
            this.Controls.Add(lblCountryHeader);

            lstCountries = new ListBox();
            lstCountries.Location = new Point(15, 65);
            lstCountries.Size = new Size(200, 350);
            lstCountries.BackColor = clrInputBg;
            lstCountries.ForeColor = clrTextPrimary;
            lstCountries.BorderStyle = BorderStyle.FixedSingle;
            lstCountries.SelectedIndexChanged += LstCountries_SelectedIndexChanged;
            this.Controls.Add(lstCountries);

            // Networks
            lblNetworkHeader = new Label();
            lblNetworkHeader.Text = "Networks";
            lblNetworkHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblNetworkHeader.ForeColor = clrAccentBlue;
            lblNetworkHeader.Location = new Point(230, 45);
            lblNetworkHeader.AutoSize = true;
            this.Controls.Add(lblNetworkHeader);

            lstNetworks = new ListBox();
            lstNetworks.Location = new Point(230, 65);
            lstNetworks.Size = new Size(280, 350);
            lstNetworks.BackColor = clrInputBg;
            lstNetworks.ForeColor = clrTextPrimary;
            lstNetworks.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(lstNetworks);

            // Selected
            lblSelectedHeader = new Label();
            lblSelectedHeader.Text = "Selected Networks";
            lblSelectedHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSelectedHeader.ForeColor = clrAccentTeal;
            lblSelectedHeader.Location = new Point(530, 45);
            lblSelectedHeader.AutoSize = true;
            this.Controls.Add(lblSelectedHeader);

            lstSelected = new ListBox();
            lstSelected.Location = new Point(530, 65);
            lstSelected.Size = new Size(190, 350);
            lstSelected.BackColor = clrInputBg;
            lstSelected.ForeColor = clrTextPrimary;
            lstSelected.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(lstSelected);

            // Buttons
            btnAdd = new Button();
            btnAdd.Text = "Add >>";
            btnAdd.Location = new Point(530, 425);
            btnAdd.Size = new Size(90, 35);
            btnAdd.FlatStyle = FlatStyle.Flat;
            btnAdd.BackColor = clrAccentGreen;
            btnAdd.ForeColor = clrBackground;
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Cursor = Cursors.Hand;
            btnAdd.Click += BtnAdd_Click;
            this.Controls.Add(btnAdd);

            btnRemove = new Button();
            btnRemove.Text = "<< Remove";
            btnRemove.Location = new Point(630, 425);
            btnRemove.Size = new Size(90, 35);
            btnRemove.FlatStyle = FlatStyle.Flat;
            btnRemove.BackColor = clrAccentRed;
            btnRemove.ForeColor = clrBackground;
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.Cursor = Cursors.Hand;
            btnRemove.Click += BtnRemove_Click;
            this.Controls.Add(btnRemove);

            btnSave = new Button();
            btnSave.Text = "SAVE";
            btnSave.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSave.Location = new Point(15, 475);
            btnSave.Size = new Size(350, 40);
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.BackColor = clrAccentTeal;
            btnSave.ForeColor = clrBackground;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Cursor = Cursors.Hand;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnClose = new Button();
            btnClose.Text = "CLOSE";
            btnClose.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnClose.Location = new Point(380, 475);
            btnClose.Size = new Size(340, 40);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.BackColor = Color.FromArgb(69, 71, 90);
            btnClose.ForeColor = clrTextPrimary;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }

        private void LoadCountries()
        {
            var countries = _db.GetCountries();
            lstCountries.Items.Clear();

            // Yemen first
            if (countries.Contains("Yemen"))
            {
                lstCountries.Items.Add("Yemen");
                countries.Remove("Yemen");
            }

            foreach (var country in countries)
                lstCountries.Items.Add(country);

            if (lstCountries.Items.Count > 0)
                lstCountries.SelectedIndex = 0;
        }

        private void LstCountries_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstCountries.SelectedItem == null) return;
            string country = lstCountries.SelectedItem.ToString();
            var networks = _db.GetNetworksByCountry(country);

            lstNetworks.Items.Clear();
            foreach (var net in networks)
                lstNetworks.Items.Add($"{net.Operator} (MCC:{net.Mcc} MNC:{net.Mnc})"); // تم التصحيح إلى Mcc و Mnc
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            if (lstNetworks.SelectedItem == null) return;
            string country = lstCountries.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(country)) return;

            var networks = _db.GetNetworksByCountry(country);
            int idx = lstNetworks.SelectedIndex;
            if (idx >= 0 && idx < networks.Count)
            {
                _store.AddNetwork(networks[idx]);
                RefreshSelectedList();
            }
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (lstSelected.SelectedItem == null) return;
            int idx = lstSelected.SelectedIndex;
            if (idx >= 0 && idx < _store.SelectedNetworks.Count)
            {
                _store.RemoveNetwork(_store.SelectedNetworks[idx]);
                RefreshSelectedList();
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            NetworksSaved?.Invoke(this, EventArgs.Empty);
            this.Close();
        }

        private void RefreshSelectedList()
        {
            lstSelected.Items.Clear();
            foreach (var net in _store.SelectedNetworks)
                lstSelected.Items.Add($"{net.Operator} ({net.Mcc}/{net.Mnc})"); // تم التصحيح إلى Mcc و Mnc
        }
    }
}