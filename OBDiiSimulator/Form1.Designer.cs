using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace OBDiiSimulator
{
    partial class Form1
    {
        /// <summary>
        /// Variável de designer necessária.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpar os recursos que estão sendo usados.
        /// </summary>
        /// <param name="disposing">true se for necessário descartar os recursos gerenciados; caso contrário, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código gerado pelo Windows Form Designer

        /// <summary>
        /// Método necessário para suporte ao Designer - não modifique 
        /// o conteúdo deste método com o editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form configuration
            this.Size = new Size(1400, 900);
            this.Text = "OBDII ELM327 Simulator - Caminhões Avançado";
            this.BackColor = Color.DarkBlue;
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            CreateDashboardPanel();
            CreateControlPanel();
            CreateLogPanel();
            CreateBluetoothPanel();

            this.ResumeLayout(false);
        }

        private void CreateDashboardPanel()
        {
            // Dashboard Panel
            Panel dashboardPanel = new Panel()
            {
                Location = new Point(10, 10),
                Size = new Size(680, 450),
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Create labels for all parameters
            CreateDashboardLabels(dashboardPanel);

            this.Controls.Add(dashboardPanel);
        }

        private void CreateDashboardLabels(Panel parent)
        {
            int x = 20, y = 20;
            int labelWidth = 200;
            int labelHeight = 25;
            int spacing = 30;

            // Engine Parameters
            var engineGroup = CreateGroupBox("Parâmetros do Motor", x, y, 300, 200);
            parent.Controls.Add(engineGroup);

            CreateLabel(engineGroup, "rpmLabel", "RPM: 0", 10, 25, labelWidth, labelHeight, Color.Lime);
            CreateLabel(engineGroup, "engineLoadLabel", "Carga Motor: 0%", 10, 25 + spacing, labelWidth, labelHeight, Color.Orange);
            CreateLabel(engineGroup, "throttleLabel", "Posição Acelerador: 0%", 10, 25 + spacing * 2, labelWidth, labelHeight, Color.Yellow);
            CreateLabel(engineGroup, "runtimeLabel", "Tempo Ligado: 0h", 10, 25 + spacing * 3, labelWidth, labelHeight, Color.LightGreen);
            CreateLabel(engineGroup, "intakeAirTempLabel", "Temp Ar Admissão: 0°C", 10, 25 + spacing * 4, labelWidth, labelHeight, Color.Cyan);
            CreateLabel(engineGroup, "manifoldPressureLabel", "Pressão Coletor: 0 kPa", 10, 25 + spacing * 5, labelWidth, labelHeight, Color.Pink);

            // Temperature and Pressure
            var tempGroup = CreateGroupBox("Temperatura e Pressão", x + 320, y, 300, 200);
            parent.Controls.Add(tempGroup);

            CreateLabel(tempGroup, "tempLabel", "Temp Líquido: 0°C", 10, 25, labelWidth, labelHeight, Color.Orange);
            CreateLabel(tempGroup, "oilPressureLabel", "Pressão Óleo: 0 kPa", 10, 25 + spacing, labelWidth, labelHeight, Color.Yellow);
            CreateLabel(tempGroup, "fuelPressureLabel", "Pressão Combustível: 0 kPa", 10, 25 + spacing * 2, labelWidth, labelHeight, Color.Magenta);
            CreateLabel(tempGroup, "batteryVoltageLabel", "Tensão Bateria: 0V", 10, 25 + spacing * 3, labelWidth, labelHeight, Color.LightBlue);

            // Vehicle Parameters
            var vehicleGroup = CreateGroupBox("Parâmetros do Veículo", x, y + 220, 300, 200);
            parent.Controls.Add(vehicleGroup);

            CreateLabel(vehicleGroup, "speedLabel", "Velocidade: 0 km/h", 10, 25, labelWidth, labelHeight, Color.Cyan);
            CreateLabel(vehicleGroup, "gearLabel", "Marcha: N", 10, 25 + spacing, labelWidth, labelHeight, Color.LightBlue);
            CreateLabel(vehicleGroup, "mileageLabel", "Quilometragem: 0 km", 10, 25 + spacing * 2, labelWidth, labelHeight, Color.White);

            // Fuel Parameters
            var fuelGroup = CreateGroupBox("Combustível", x + 320, y + 220, 300, 200);
            parent.Controls.Add(fuelGroup);

            CreateLabel(fuelGroup, "fuelConsumptionLabel", "Consumo: 0 L/100km", 10, 25, labelWidth, labelHeight, Color.Gold);
            CreateLabel(fuelGroup, "fuelLevelLabel", "Nível Combustível: 0%", 10, 25 + spacing, labelWidth, labelHeight, Color.Red);
            CreateLabel(fuelGroup, "fuelSystemLabel", "Sistema Combustível: OK", 10, 25 + spacing * 2, labelWidth, labelHeight, Color.Green);
            CreateLabel(fuelGroup, "oxygenSensorLabel", "Sensor O2: 0.0V", 10, 25 + spacing * 3, labelWidth, labelHeight, Color.Purple);
        }

        private GroupBox CreateGroupBox(string text, int x, int y, int width, int height)
        {
            return new GroupBox()
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
        }

        private void CreateLabel(Control parent, string name, string text, int x, int y, int width, int height, Color color)
        {
            Label label = new Label()
            {
                Name = name,
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                Font = new Font("Arial", 9, FontStyle.Bold),
                ForeColor = color
            };
            parent.Controls.Add(label);
        }

        private void CreateControlPanel()
        {
            // Control Panel
            Panel controlPanel = new Panel()
            {
                Location = new Point(700, 10),
                Size = new Size(680, 450),
                BackColor = Color.DarkGray,
                BorderStyle = BorderStyle.FixedSingle
            };

            CreateControlElements(controlPanel);
            this.Controls.Add(controlPanel);
        }

        private void CreateControlElements(Panel parent)
        {
            // Start/Stop Button
            Button startStopButton = new Button()
            {
                Text = "Iniciar Simulação",
                Location = new Point(20, 20),
                Size = new Size(150, 40),
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Name = "startStopButton"
            };
            startStopButton.Click += StartStopButton_Click;
            parent.Controls.Add(startStopButton);

            // Clear DTCs Button
            Button clearDtcButton = new Button()
            {
                Text = "Limpar DTCs",
                Location = new Point(180, 20),
                Size = new Size(120, 40),
                BackColor = Color.Orange,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Name = "clearDtcButton"
            };
            clearDtcButton.Click += ClearDtcButton_Click;
            parent.Controls.Add(clearDtcButton);

            // DTC Status
            Label dtcStatusLabel = new Label()
            {
                Text = "DTCs Ativos: 0",
                Location = new Point(320, 30),
                Size = new Size(150, 30),
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.Black,
                Name = "dtcStatusLabel"
            };
            parent.Controls.Add(dtcStatusLabel);

            // Send to Database Button
            Button sendToDatabase = new Button()
            {
                Text = "Mandar pro Banco de Dados",
                Location = new Point(500, 20), 
                Size = new Size(150, 40),
                BackColor = Color.LightBlue,
                ForeColor = Color.DarkBlue,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Name = "sendToDbButton"
            };

            sendToDatabase.Click += SendToDatabase_Click;
            parent.Controls.Add(sendToDatabase);

            // Manual Controls Group
            GroupBox manualGroup = new GroupBox()
            {
                Text = "Controles Manuais",
                Location = new Point(20, 80),
                Size = new Size(640, 200),
                ForeColor = Color.Black,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            CreateManualControls(manualGroup);
            parent.Controls.Add(manualGroup);

            // Simulation Modes
            GroupBox modesGroup = new GroupBox()
            {
                Text = "Modos de Simulação",
                Location = new Point(20, 290),
                Size = new Size(640, 150),
                ForeColor = Color.Black,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            CreateSimulationModes(modesGroup);
            parent.Controls.Add(modesGroup);
        }

        private void CreateManualControls(GroupBox parent)
        {
            // RPM Control
            CreateTrackBarWithLabel(parent, "rpmTrackBar", "rpmControlLabel",
                "RPM: 1000", 20, 30, 600, 3000, 800, RPMTrackBar_ValueChanged);

            // Temperature Control
            CreateTrackBarWithLabel(parent, "tempTrackBar", "tempControlLabel",
                "Temperatura: 85°C", 20, 80, 40, 150, 85, TempTrackBar_ValueChanged);

            // Speed Control
            CreateTrackBarWithLabel(parent, "speedTrackBar", "speedControlLabel",
                "Velocidade: 0 km/h", 20, 130, 0, 120, 0, SpeedTrackBar_ValueChanged);
        }

        private void CreateTrackBarWithLabel(GroupBox parent, string trackBarName, string labelName,
            string labelText, int x, int y, int min, int max, int value, EventHandler valueChanged)
        {
            TrackBar trackBar = new TrackBar()
            {
                Location = new Point(x, y),
                Size = new Size(250, 45),
                Minimum = min,
                Maximum = max,
                Value = value,
                TickFrequency = (max - min) / 10,
                Name = trackBarName
            };
            trackBar.ValueChanged += valueChanged;

            Label label = new Label()
            {
                Text = labelText,
                Location = new Point(x + 270, y + 10),
                Size = new Size(150, 20),
                ForeColor = Color.Black,
                Name = labelName
            };

            parent.Controls.AddRange(new Control[] { trackBar, label });
        }

        private void CreateSimulationModes(GroupBox parent)
        {
            CheckBox criticalModeCheckBox = new CheckBox()
            {
                Text = "Modo Crítico",
                Location = new Point(20, 30),
                Size = new Size(150, 30),
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.Black,
                Name = "criticalModeCheckBox"
            };
            criticalModeCheckBox.CheckedChanged += CriticalMode_CheckedChanged;

            CheckBox dtcModeCheckBox = new CheckBox()
            {
                Text = "Simular DTCs",
                Location = new Point(180, 30),
                Size = new Size(150, 30),
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.Black,
                Name = "dtcModeCheckBox"
            };
            dtcModeCheckBox.CheckedChanged += DtcMode_CheckedChanged;

            CheckBox manualModeCheckBox = new CheckBox()
            {
                Text = "Modo Manual",
                Location = new Point(340, 30),
                Size = new Size(150, 30),
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.Black,
                Name = "manualModeCheckBox"
            };
            manualModeCheckBox.CheckedChanged += ManualMode_CheckedChanged;

            parent.Controls.AddRange(new Control[] { criticalModeCheckBox, dtcModeCheckBox, manualModeCheckBox });
        }

        private void CreateBluetoothPanel()
        {
            GroupBox bluetoothGroup = new GroupBox()
            {
                Text = "Conexão Bluetooth/Serial",
                Location = new Point(10, 470),
                Size = new Size(680, 120),
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            // Status Label
            Label bluetoothStatusLabel = new Label()
            {
                Text = "Status: Desconectado",
                Location = new Point(20, 30),
                Size = new Size(200, 30),
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.Red,
                Name = "bluetoothStatusLabel"
            };

            // COM Port Selection
            Label comPortLabel = new Label()
            {
                Text = "Porta COM:",
                Location = new Point(20, 60),
                Size = new Size(80, 30),
                Font = new Font("Arial", 9),
                ForeColor = Color.White
            };

            ComboBox comPortCombo = new ComboBox()
            {
                Location = new Point(100, 62),
                Size = new Size(120, 25),
                Name = "comPortCombo",
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Connect/Disconnect Button
            Button connectButton = new Button()
            {
                Text = "Conectar",
                Location = new Point(240, 60),
                Size = new Size(100, 30),
                BackColor = Color.Blue,
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Name = "connectButton"
            };
            connectButton.Click += ConnectButton_Click;

            // Refresh Ports Button
            Button refreshPortsButton = new Button()
            {
                Text = "Atualizar Portas",
                Location = new Point(350, 60),
                Size = new Size(120, 30),
                BackColor = Color.Gray,
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Name = "refreshPortsButton"
            };
            refreshPortsButton.Click += RefreshPortsButton_Click;

            // Device Discovery Button
            Button discoverButton = new Button()
            {
                Text = "Descobrir Dispositivos",
                Location = new Point(480, 60),
                Size = new Size(150, 30),
                BackColor = Color.Purple,
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold),
                Name = "discoverButton"
            };
            discoverButton.Click += DiscoverButton_Click;

            bluetoothGroup.Controls.AddRange(new Control[] {
                bluetoothStatusLabel, comPortLabel, comPortCombo,
                connectButton, refreshPortsButton, discoverButton
            });

            this.Controls.Add(bluetoothGroup);
        }

        private void CreateLogPanel()
        {
            // Log Panel
            Panel logPanel = new Panel()
            {
                Location = new Point(700, 470),
                Size = new Size(680, 400),
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };

            TextBox logTextBox = new TextBox()
            {
                Location = new Point(10, 10),
                Size = new Size(660, 380),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                Name = "logTextBox"
            };

            logPanel.Controls.Add(logTextBox);
            this.Controls.Add(logPanel);
        }
 
        #endregion
    }
}
