using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OBDiiSimulator
{
    public partial class DeviceSelectionDialog : Form
    {
        public BluetoothDevice SelectedDevice { get; private set; }
        private BluetoothManager bluetoothManager;
        private ListView deviceListView;
        private Button connectButton;
        private Button refreshButton;
        private ProgressBar refreshProgress;
        private Label infoLabel;

        // CORREÇÃO: Construtor sem parâmetros (original)
        public DeviceSelectionDialog(List<BluetoothDevice> devices)
        {
            bluetoothManager = new BluetoothManager();
            InitializeComponent();
            SetupEvents();
            _ = RefreshDeviceList();
        }

        // CORREÇÃO: Construtor que aceita BluetoothManager como parâmetro
        public DeviceSelectionDialog(BluetoothManager manager)
        {
            bluetoothManager = manager ?? new BluetoothManager();
            InitializeComponent();
            SetupEvents();
            _ = RefreshDeviceList();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(700, 500);
            this.Text = "Seletor de Dispositivos Bluetooth - 32feet.NET";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Título e informações
            var titleLabel = new Label()
            {
                Text = "Dispositivos Bluetooth Disponíveis:",
                Location = new Point(20, 20),
                Size = new Size(300, 25),
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            infoLabel = new Label()
            {
                Text = "Verificando adaptador Bluetooth...",
                Location = new Point(20, 45),
                Size = new Size(400, 20),
                Font = new Font("Arial", 9),
                ForeColor = Color.Gray
            };

            // Lista de dispositivos
            deviceListView = new ListView()
            {
                Location = new Point(20, 75),
                Size = new Size(640, 280),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            deviceListView.Columns.Add("Nome do Dispositivo", 200);
            deviceListView.Columns.Add("Endereço MAC", 140);
            deviceListView.Columns.Add("Força do Sinal", 100);
            deviceListView.Columns.Add("Status", 100);
            deviceListView.Columns.Add("Serviços", 100);

            deviceListView.SelectedIndexChanged += DeviceListView_SelectedIndexChanged;
            deviceListView.DoubleClick += DeviceListView_DoubleClick;

            // Barra de progresso
            refreshProgress = new ProgressBar()
            {
                Location = new Point(20, 365),
                Size = new Size(300, 25),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            // Botões
            refreshButton = new Button()
            {
                Text = "🔄 Atualizar Lista",
                Location = new Point(20, 400),
                Size = new Size(130, 40),
                BackColor = Color.DodgerBlue,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };

            connectButton = new Button()
            {
                Text = "🔗 Conectar",
                Location = new Point(470, 400),
                Size = new Size(120, 40),
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Enabled = false,
                UseVisualStyleBackColor = false
            };

            var cancelButton = new Button()
            {
                Text = "❌ Cancelar",
                Location = new Point(600, 400),
                Size = new Size(80, 40),
                BackColor = Color.Gray,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = false
            };

            // Label de instruções
            var instructionLabel = new Label()
            {
                Text = "💡 Selecione um dispositivo da lista e clique em 'Conectar', ou dê duplo clique para conectar diretamente.",
                Location = new Point(160, 405),
                Size = new Size(300, 30),
                Font = new Font("Arial", 9),
                ForeColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };

            this.Controls.AddRange(new Control[] {
                titleLabel, infoLabel, deviceListView, refreshProgress,
                refreshButton, connectButton, cancelButton, instructionLabel
            });

            this.CancelButton = cancelButton;
            this.AcceptButton = connectButton;
        }

        private void SetupEvents()
        {
            refreshButton.Click += RefreshButton_Click;
            connectButton.Click += ConnectButton_Click;

            bluetoothManager.DevicesDiscovered += OnDevicesDiscovered;
            bluetoothManager.LogMessage += OnLogMessage;

            this.Load += (s, e) => CheckBluetoothStatus();
            this.FormClosing += (s, e) =>
            {
                // CORREÇÃO: Só dispose se criamos o manager internamente
                if (bluetoothManager != null)
                {
                    bluetoothManager.Dispose();
                }
            };
        }

        private void CheckBluetoothStatus()
        {
            if (bluetoothManager.IsBluetoothAvailable())
            {
                var radio = bluetoothManager.GetBluetoothRadio();
                if (radio != null)
                {
                    infoLabel.Text = $"✅ Adaptador: {radio.Name} - Modo: {radio.Mode}";
                    infoLabel.ForeColor = Color.Green;
                }
                else
                {
                    infoLabel.Text = "⚠️ Adaptador Bluetooth detectado, mas inacessível";
                    infoLabel.ForeColor = Color.Orange;
                }
            }
            else
            {
                infoLabel.Text = "❌ Bluetooth não disponível ou desabilitado";
                infoLabel.ForeColor = Color.Red;
            }
        }

        private async void RefreshButton_Click(object sender, EventArgs e)
        {
            await RefreshDeviceList();
        }

        private async Task RefreshDeviceList()
        {
            refreshButton.Enabled = false;
            refreshProgress.Visible = true;
            refreshButton.Text = "🔄 Procurando...";
            deviceListView.Items.Clear();

            try
            {
                await bluetoothManager.DiscoverDevicesAsync();
            }
            finally
            {
                refreshButton.Enabled = true;
                refreshProgress.Visible = false;
                refreshButton.Text = "🔄 Atualizar Lista";
            }
        }

        private void OnDevicesDiscovered(List<BluetoothDevice> devices)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<List<BluetoothDevice>>(OnDevicesDiscovered), devices);
                return;
            }

            deviceListView.Items.Clear();

            if (devices.Count == 0)
            {
                var noDeviceItem = new ListViewItem("Nenhum dispositivo encontrado");
                noDeviceItem.SubItems.Add("-");
                noDeviceItem.SubItems.Add("-");
                noDeviceItem.SubItems.Add("-");
                noDeviceItem.SubItems.Add("-");
                noDeviceItem.ForeColor = Color.Gray;
                noDeviceItem.Font = new Font(noDeviceItem.Font, FontStyle.Italic);
                deviceListView.Items.Add(noDeviceItem);
                return;
            }

            foreach (var device in devices.OrderByDescending(d => d.SignalStrength))
            {
                var item = new ListViewItem(device.Name);
                item.SubItems.Add(device.Address);
                item.SubItems.Add($"{device.SignalStrength} dBm");
                item.SubItems.Add(device.IsConnected ? "🔗 Conectado" : "📱 Disponível");
                item.SubItems.Add($"{device.AvailableServices.Count} serviços");
                item.Tag = device;

                // Definir cores baseadas no sinal
                if (device.SignalStrength > -50)
                {
                    item.ForeColor = Color.DarkGreen;
                    item.Font = new Font(item.Font, FontStyle.Bold);
                }
                else if (device.SignalStrength > -70)
                {
                    item.ForeColor = Color.DarkOrange;
                }
                else
                {
                    item.ForeColor = Color.DarkRed;
                }

                // Destacar dispositivos conectados
                if (device.IsConnected)
                {
                    item.BackColor = Color.LightGreen;
                    item.Font = new Font(item.Font, FontStyle.Bold);
                }

                // Adicionar ícone baseado no tipo
                if (device.Name.ToUpper().Contains("OBD") || device.Name.ToUpper().Contains("ELM"))
                {
                    item.ImageIndex = 0; // Ícone de diagnóstico
                }

                deviceListView.Items.Add(item);
            }

            // Auto-selecionar o melhor dispositivo
            if (deviceListView.Items.Count > 0)
            {
                var bestDevice = deviceListView.Items.Cast<ListViewItem>()
                    .Where(item => item.Tag is BluetoothDevice device && !device.IsConnected)
                    .OrderByDescending(item => ((BluetoothDevice)item.Tag).SignalStrength)
                    .FirstOrDefault();

                if (bestDevice != null)
                {
                    bestDevice.Selected = true;
                    bestDevice.EnsureVisible();
                }
            }
        }

        private void OnLogMessage(string message)
        {
            // Log interno - pode ser usado para debug se necessário
        }

        private void DeviceListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (deviceListView.SelectedItems.Count > 0)
            {
                var selectedItem = deviceListView.SelectedItems[0];
                var device = selectedItem.Tag as BluetoothDevice;

                if (device != null)
                {
                    connectButton.Enabled = !device.IsConnected;
                    connectButton.Text = device.IsConnected ? "🔗 Já Conectado" : "🔗 Conectar";

                    // Atualizar informações do dispositivo selecionado
                    var services = bluetoothManager.GetDeviceServices(device);
                    var serviceNames = services.Take(3).Select(s => BluetoothService.GetName(s) ?? "Desconhecido");

                    infoLabel.Text = $"📋 Dispositivo: {device.Name} | Serviços: {string.Join(", ", serviceNames)}";
                    infoLabel.ForeColor = Color.DarkBlue;
                }
            }
            else
            {
                connectButton.Enabled = false;
                connectButton.Text = "🔗 Conectar";
            }
        }

        private void DeviceListView_DoubleClick(object sender, EventArgs e)
        {
            if (deviceListView.SelectedItems.Count > 0)
            {
                var selectedItem = deviceListView.SelectedItems[0];
                var device = selectedItem.Tag as BluetoothDevice;

                if (device != null && !device.IsConnected)
                {
                    SelectedDevice = device;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (device?.IsConnected == true)
                {
                    MessageBox.Show("Este dispositivo já está conectado!", "Aviso",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (deviceListView.SelectedItems.Count > 0)
            {
                var selectedItem = deviceListView.SelectedItems[0];
                var device = selectedItem.Tag as BluetoothDevice;

                if (device != null && !device.IsConnected)
                {
                    SelectedDevice = device;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
        }
    }
}