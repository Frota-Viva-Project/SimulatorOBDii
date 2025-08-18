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
            this.Size = new Size(800, 600);
            this.Text = "Gerenciador Bluetooth - 32feet.NET";
            this.StartPosition = FormStartPosition.CenterScreen;

            // Painel principal
            var mainPanel = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4
            };

            // Configurar colunas e linhas
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // Painel de controle superior
            var controlPanel = new Panel() { Dock = DockStyle.Fill };

            discoverButton = new Button()
            {
                Text = "Descobrir Dispositivos",
                Location = new Point(10, 10),
                Size = new Size(150, 35),
                BackColor = Color.DodgerBlue,
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };

            connectButton = new Button()
            {
                Text = "Conectar",
                Location = new Point(170, 10),
                Size = new Size(100, 35),
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Enabled = false
            };

            disconnectButton = new Button()
            {
                Text = "Desconectar",
                Location = new Point(280, 10),
                Size = new Size(100, 35),
                BackColor = Color.Red,
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Enabled = false
            };

            discoveryProgress = new ProgressBar()
            {
                Location = new Point(390, 15),
                Size = new Size(150, 25),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            controlPanel.Controls.AddRange(new Control[] { discoverButton, connectButton, disconnectButton, discoveryProgress });

            // Status
            var statusPanel = new Panel() { Dock = DockStyle.Fill };
            statusLabel = new Label()
            {
                Text = "Status: Desconectado",
                Location = new Point(10, 15),
                Size = new Size(200, 25),
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.Red
            };

            serverButton = new Button()
            {
                Text = "Iniciar Servidor",
                Location = new Point(10, 40),
                Size = new Size(120, 30),
                BackColor = Color.Purple,
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };

            statusPanel.Controls.AddRange(new Control[] { statusLabel, serverButton });

            // Lista de dispositivos
            deviceListView = new ListView()
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            deviceListView.Columns.Add("Nome", 180);
            deviceListView.Columns.Add("Endereço", 120);
            deviceListView.Columns.Add("Sinal", 60);
            deviceListView.Columns.Add("Status", 80);

            // Painel de mensagem
            var messagePanel = new Panel() { Dock = DockStyle.Fill };

            var messageLabel = new Label()
            {
                Text = "Mensagem:",
                Location = new Point(5, 5),
                Size = new Size(70, 20)
            };

            messageTextBox = new TextBox()
            {
                Location = new Point(5, 25),
                Size = new Size(250, 25),
                Text = "AT+VERSION"
            };

            sendButton = new Button()
            {
                Text = "Enviar",
                Location = new Point(265, 25),
                Size = new Size(70, 25),
                BackColor = Color.Orange,
                ForeColor = Color.White,
                Enabled = false
            };

            messagePanel.Controls.AddRange(new Control[] { messageLabel, messageTextBox, sendButton });

            // Log de eventos
            logTextBox = new TextBox()
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9)
            };

            // Adicionar controles ao layout principal
            mainPanel.Controls.Add(controlPanel, 0, 0);
            mainPanel.Controls.Add(statusPanel, 1, 0);
            mainPanel.Controls.Add(deviceListView, 0, 1);
            mainPanel.Controls.Add(messagePanel, 0, 2);
            mainPanel.Controls.Add(logTextBox, 0, 3);

            mainPanel.SetColumnSpan(logTextBox, 2);

            this.Controls.Add(mainPanel);
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
