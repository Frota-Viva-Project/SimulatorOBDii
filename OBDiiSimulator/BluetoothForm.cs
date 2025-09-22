using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OBDiiSimulator
{
    public partial class BluetoothForm : Form
    {
        private BluetoothManager bluetoothManager;
        private ListView deviceListView;
        private TextBox messageTextBox;
        private TextBox logTextBox;
        private Button discoverButton;
        private Button connectButton;
        private Button disconnectButton;
        private Button sendButton;
        private Button serverButton;
        private Label statusLabel;
        private TableLayoutPanel mainPanel;
        private Panel controlPanel;
        private Panel statusPanel;
        private Panel messagePanel;
        private Label messageLabel;
        private ProgressBar discoveryProgress;

        public BluetoothForm()
        {
            bluetoothManager = new BluetoothManager();
            InitializeComponent();
            SetupEventHandlers();
            UpdateUI();
        }

        private void InitializeComponent()
        {
            this.mainPanel = new System.Windows.Forms.TableLayoutPanel();
            this.controlPanel = new System.Windows.Forms.Panel();
            this.discoverButton = new System.Windows.Forms.Button();
            this.connectButton = new System.Windows.Forms.Button();
            this.disconnectButton = new System.Windows.Forms.Button();
            this.discoveryProgress = new System.Windows.Forms.ProgressBar();
            this.statusPanel = new System.Windows.Forms.Panel();
            this.statusLabel = new System.Windows.Forms.Label();
            this.serverButton = new System.Windows.Forms.Button();
            this.deviceListView = new System.Windows.Forms.ListView();
            this.messagePanel = new System.Windows.Forms.Panel();
            this.messageLabel = new System.Windows.Forms.Label();
            this.messageTextBox = new System.Windows.Forms.TextBox();
            this.sendButton = new System.Windows.Forms.Button();
            this.logTextBox = new System.Windows.Forms.TextBox();
            this.mainPanel.SuspendLayout();
            this.controlPanel.SuspendLayout();
            this.statusPanel.SuspendLayout();
            this.messagePanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainPanel
            // 
            this.mainPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.mainPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.mainPanel.Controls.Add(this.controlPanel, 0, 0);
            this.mainPanel.Controls.Add(this.statusPanel, 1, 0);
            this.mainPanel.Controls.Add(this.deviceListView, 0, 1);
            this.mainPanel.Controls.Add(this.messagePanel, 0, 2);
            this.mainPanel.Controls.Add(this.logTextBox, 0, 3);
            this.mainPanel.Location = new System.Drawing.Point(0, 0);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.mainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.mainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.mainPanel.Size = new System.Drawing.Size(200, 100);
            this.mainPanel.TabIndex = 0;
            // 
            // controlPanel
            // 
            this.controlPanel.Controls.Add(this.discoverButton);
            this.controlPanel.Controls.Add(this.connectButton);
            this.controlPanel.Controls.Add(this.disconnectButton);
            this.controlPanel.Controls.Add(this.discoveryProgress);
            this.controlPanel.Location = new System.Drawing.Point(3, 3);
            this.controlPanel.Name = "controlPanel";
            this.controlPanel.Size = new System.Drawing.Size(114, 54);
            this.controlPanel.TabIndex = 0;
            // 
            // discoverButton
            // 
            this.discoverButton.Location = new System.Drawing.Point(0, 0);
            this.discoverButton.Name = "discoverButton";
            this.discoverButton.Size = new System.Drawing.Size(75, 23);
            this.discoverButton.TabIndex = 0;
            // 
            // connectButton
            // 
            this.connectButton.Location = new System.Drawing.Point(0, 0);
            this.connectButton.Name = "connectButton";
            this.connectButton.Size = new System.Drawing.Size(75, 23);
            this.connectButton.TabIndex = 1;
            // 
            // disconnectButton
            // 
            this.disconnectButton.Location = new System.Drawing.Point(0, 0);
            this.disconnectButton.Name = "disconnectButton";
            this.disconnectButton.Size = new System.Drawing.Size(75, 23);
            this.disconnectButton.TabIndex = 2;
            // 
            // discoveryProgress
            // 
            this.discoveryProgress.Location = new System.Drawing.Point(0, 0);
            this.discoveryProgress.Name = "discoveryProgress";
            this.discoveryProgress.Size = new System.Drawing.Size(100, 23);
            this.discoveryProgress.TabIndex = 3;
            // 
            // statusPanel
            // 
            this.statusPanel.Controls.Add(this.statusLabel);
            this.statusPanel.Controls.Add(this.serverButton);
            this.statusPanel.Location = new System.Drawing.Point(123, 3);
            this.statusPanel.Name = "statusPanel";
            this.statusPanel.Size = new System.Drawing.Size(74, 54);
            this.statusPanel.TabIndex = 1;
            // 
            // statusLabel
            // 
            this.statusLabel.Location = new System.Drawing.Point(0, 0);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(100, 23);
            this.statusLabel.TabIndex = 0;
            // 
            // serverButton
            // 
            this.serverButton.Location = new System.Drawing.Point(0, 0);
            this.serverButton.Name = "serverButton";
            this.serverButton.Size = new System.Drawing.Size(75, 23);
            this.serverButton.TabIndex = 1;
            // 
            // deviceListView
            // 
            this.deviceListView.HideSelection = false;
            this.deviceListView.Location = new System.Drawing.Point(3, 63);
            this.deviceListView.Name = "deviceListView";
            this.deviceListView.Size = new System.Drawing.Size(114, 1);
            this.deviceListView.TabIndex = 2;
            this.deviceListView.UseCompatibleStateImageBehavior = false;
            // 
            // messagePanel
            // 
            this.messagePanel.Controls.Add(this.messageLabel);
            this.messagePanel.Controls.Add(this.messageTextBox);
            this.messagePanel.Controls.Add(this.sendButton);
            this.messagePanel.Location = new System.Drawing.Point(3, 43);
            this.messagePanel.Name = "messagePanel";
            this.messagePanel.Size = new System.Drawing.Size(114, 74);
            this.messagePanel.TabIndex = 3;
            // 
            // messageLabel
            // 
            this.messageLabel.Location = new System.Drawing.Point(0, 0);
            this.messageLabel.Name = "messageLabel";
            this.messageLabel.Size = new System.Drawing.Size(100, 23);
            this.messageLabel.TabIndex = 0;
            // 
            // messageTextBox
            // 
            this.messageTextBox.Location = new System.Drawing.Point(0, 0);
            this.messageTextBox.Name = "messageTextBox";
            this.messageTextBox.Size = new System.Drawing.Size(100, 22);
            this.messageTextBox.TabIndex = 1;
            // 
            // sendButton
            // 
            this.sendButton.Location = new System.Drawing.Point(0, 0);
            this.sendButton.Name = "sendButton";
            this.sendButton.Size = new System.Drawing.Size(75, 23);
            this.sendButton.TabIndex = 2;
            // 
            // logTextBox
            // 
            this.mainPanel.SetColumnSpan(this.logTextBox, 2);
            this.logTextBox.Location = new System.Drawing.Point(3, 123);
            this.logTextBox.Name = "logTextBox";
            this.logTextBox.Size = new System.Drawing.Size(100, 22);
            this.logTextBox.TabIndex = 4;
            // 
            // BluetoothForm
            // 
            this.ClientSize = new System.Drawing.Size(782, 553);
            this.Controls.Add(this.mainPanel);
            this.Name = "BluetoothForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Gerenciador Bluetooth - 32feet.NET";
            this.mainPanel.ResumeLayout(false);
            this.mainPanel.PerformLayout();
            this.controlPanel.ResumeLayout(false);
            this.statusPanel.ResumeLayout(false);
            this.messagePanel.ResumeLayout(false);
            this.messagePanel.PerformLayout();
            this.ResumeLayout(false);

        }

        private void SetupEventHandlers()
        {
            discoverButton.Click += DiscoverButton_Click;
            connectButton.Click += ConnectButton_Click;
            disconnectButton.Click += DisconnectButton_Click;
            sendButton.Click += SendButton_Click;
            serverButton.Click += ServerButton_Click;
            deviceListView.SelectedIndexChanged += DeviceListView_SelectedIndexChanged;

            bluetoothManager.DevicesDiscovered += OnDevicesDiscovered;
            bluetoothManager.DeviceConnected += OnDeviceConnected;
            bluetoothManager.DeviceDisconnected += OnDeviceDisconnected;
            bluetoothManager.DataReceived += OnDataReceived;
            bluetoothManager.LogMessage += OnLogMessage;
            bluetoothManager.ConnectionStatusChanged += OnConnectionStatusChanged;

            this.FormClosing += (s, e) => bluetoothManager.Dispose();
        }

        private async void DiscoverButton_Click(object sender, EventArgs e)
        {
            discoverButton.Enabled = false;
            discoveryProgress.Visible = true;
            deviceListView.Items.Clear();

            try
            {
                await bluetoothManager.DiscoverDevicesAsync();
            }
            finally
            {
                discoverButton.Enabled = true;
                discoveryProgress.Visible = false;
            }
        }

        private async void ConnectButton_Click(object sender, EventArgs e)
        {
            if (deviceListView.SelectedItems.Count > 0)
            {
                var selectedItem = deviceListView.SelectedItems[0];
                var device = selectedItem.Tag as BluetoothDevice;

                if (device != null)
                {
                    connectButton.Enabled = false;
                    await bluetoothManager.ConnectToDeviceAsync(device);
                    connectButton.Enabled = true;
                }
            }
        }

        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            bluetoothManager.Disconnect();
        }

        private async void SendButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(messageTextBox.Text))
            {
                await bluetoothManager.SendDataAsync(messageTextBox.Text);
                messageTextBox.Clear();
            }
        }

        private void ServerButton_Click(object sender, EventArgs e)
        {
            if (!bluetoothManager.IsServerRunning)
            {
                bluetoothManager.StartBluetoothServer();
                serverButton.Text = "Parar Servidor";
                serverButton.BackColor = Color.DarkRed;
            }
            else
            {
                bluetoothManager.StopBluetoothServer();
                serverButton.Text = "Iniciar Servidor";
                serverButton.BackColor = Color.Purple;
            }
        }

        private void DeviceListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            var hasSelection = deviceListView.SelectedItems.Count > 0;
            connectButton.Enabled = hasSelection && !bluetoothManager.IsConnected;
        }

        private void OnDevicesDiscovered(List<BluetoothDevice> devices)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<List<BluetoothDevice>>(OnDevicesDiscovered), devices);
                return;
            }

            deviceListView.Items.Clear();

            foreach (var device in devices.OrderByDescending(d => d.SignalStrength))
            {
                var item = new ListViewItem(device.Name);
                item.SubItems.Add(device.Address);
                item.SubItems.Add($"{device.SignalStrength} dBm");
                item.SubItems.Add(device.IsConnected ? "Conectado" : "Disponível");
                item.Tag = device;

                // Colorir baseado no sinal
                if (device.SignalStrength > -50)
                    item.ForeColor = Color.Green;
                else if (device.SignalStrength > -70)
                    item.ForeColor = Color.Orange;
                else
                    item.ForeColor = Color.Red;

                deviceListView.Items.Add(item);
            }
        }

        private void OnDeviceConnected(BluetoothDevice device)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<BluetoothDevice>(OnDeviceConnected), device);
                return;
            }

            UpdateUI();
        }

        private void OnDeviceDisconnected(BluetoothDevice device)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<BluetoothDevice>(OnDeviceDisconnected), device);
                return;
            }

            UpdateUI();
        }

        private void OnDataReceived(string data, string timestamp)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, string>(OnDataReceived), data, timestamp);
                return;
            }

            AppendLog($"[{timestamp}] Recebido: {data}");
        }

        private void OnLogMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(OnLogMessage), message);
                return;
            }

            AppendLog($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private void OnConnectionStatusChanged(bool connected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(OnConnectionStatusChanged), connected);
                return;
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            bool connected = bluetoothManager.IsConnected;

            connectButton.Enabled = !connected && deviceListView.SelectedItems.Count > 0;
            disconnectButton.Enabled = connected;
            sendButton.Enabled = connected;

            statusLabel.Text = connected ?
                $"Status: Conectado com {bluetoothManager.ConnectedDevice?.Name}" :
                "Status: Desconectado";
            statusLabel.ForeColor = connected ? Color.Green : Color.Red;
        }

        private void AppendLog(string message)
        {
            logTextBox.AppendText(message + Environment.NewLine);
            logTextBox.SelectionStart = logTextBox.Text.Length;
            logTextBox.ScrollToCaret();

            // Limitar tamanho do log
            if (logTextBox.Lines.Length > 500)
            {
                var lines = logTextBox.Lines.Skip(100).ToArray();
                logTextBox.Lines = lines;
            }
        }
    }

}
