using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OBDiiSimulator
{
    public partial class Form1 : Form
    {
        private TruckDataSimulator dataSimulator;
        private BluetoothSimulator bluetoothSimulator;
        private bool isSimulationRunning = false;
        private System.Windows.Forms.Timer updateTimer;

        public Form1()
        {
            InitializeComponent();
            InitializeSimulators();
            SetupUpdateTimer();
            RefreshComPorts();
        }

        private void InitializeSimulators()
        {
            dataSimulator = new TruckDataSimulator();
            bluetoothSimulator = new BluetoothSimulator();

            bluetoothSimulator.SetDataSimulator(dataSimulator);

            // Subscribe to events
            bluetoothSimulator.CommandReceived += OnCommandReceived;
            bluetoothSimulator.ConnectionStatusChanged += OnConnectionStatusChanged;
            bluetoothSimulator.DevicesDiscovered += OnDevicesDiscovered;
            bluetoothSimulator.LogMessage += OnLogMessage;
            dataSimulator.DataUpdated += OnDataUpdated;
        }

        private void SetupUpdateTimer()
        {
            updateTimer = new System.Windows.Forms.Timer()
            {
                Interval = 200, // Update UI every 200ms
                Enabled = false
            };
            updateTimer.Tick += UpdateTimer_Tick;
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (isSimulationRunning)
            {
                UpdateDashboard();
            }
        }

        private void UpdateDashboard()
        {
            var data = dataSimulator.GetCurrentData();

            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateDashboard()));
                return;
            }

            try
            {
                // Engine parameters
                UpdateLabel("rpmLabel", $"RPM: {data.EngineRPM:F0}");
                UpdateLabel("engineLoadLabel", $"Carga Motor: {data.EngineLoad:F1}%");
                UpdateLabel("throttleLabel", $"Posição Acelerador: {data.ThrottlePosition:F1}%");
                UpdateLabel("runtimeLabel", $"Tempo Ligado: {TimeSpan.FromSeconds(data.EngineRunTime):hh\\:mm\\:ss}");
                UpdateLabel("intakeAirTempLabel", $"Temp Ar Admissão: {data.IntakeAirTemp:F1}°C");
                UpdateLabel("manifoldPressureLabel", $"Pressão Coletor: {data.ManifoldPressure:F1} kPa");

                // Temperature and pressure
                UpdateLabel("tempLabel", $"Temp Líquido: {data.CoolantTemp:F1}°C");
                UpdateLabel("oilPressureLabel", $"Pressão Óleo: {data.OilPressure:F0} kPa");
                UpdateLabel("fuelPressureLabel", $"Pressão Combustível: {data.FuelPressure:F0} kPa");
                UpdateLabel("batteryVoltageLabel", $"Tensão Bateria: {data.BatteryVoltage:F1}V");

                // Vehicle parameters
                UpdateLabel("speedLabel", $"Velocidade: {data.VehicleSpeed:F1} km/h");
                UpdateLabel("gearLabel", $"Marcha: {data.CurrentGear}");
                UpdateLabel("mileageLabel", $"Quilometragem: {data.Mileage:F1} km");

                // Fuel parameters
                UpdateLabel("fuelConsumptionLabel", $"Consumo: {data.FuelConsumption:F1} L/100km");
                UpdateLabel("fuelLevelLabel", $"Nível Combustível: {data.FuelLevel:F1}%");
                UpdateLabel("fuelSystemLabel", $"Sistema Combustível: {GetFuelSystemDescription(data.FuelSystemStatus)}");
                UpdateLabel("oxygenSensorLabel", $"Sensor O2: {data.OxygenSensor1:F2}V / {data.OxygenSensor2:F2}V");

                // DTC Status
                UpdateLabel("dtcStatusLabel", $"DTCs: {data.GetDTCStatus()}");

                // Update warning colors
                UpdateWarningColors(data);
            }
            catch (Exception ex)
            {
                OnLogMessage($"Erro ao atualizar interface: {ex.Message}");
            }
        }

        private void UpdateLabel(string name, string text)
        {
            var label = FindControl(this, name) as Label;
            if (label != null)
            {
                label.Text = text;
            }
        }

        private void UpdateWarningColors(TruckData data)
        {
            // Temperature warnings
            var tempLabel = FindControl(this, "tempLabel") as Label;
            if (tempLabel != null)
            {
                if (data.CoolantTemp > 110)
                    tempLabel.ForeColor = Color.Red;
                else if (data.CoolantTemp > 100)
                    tempLabel.ForeColor = Color.Yellow;
                else
                    tempLabel.ForeColor = Color.Orange;
            }

            // RPM warnings
            var rpmLabel = FindControl(this, "rpmLabel") as Label;
            if (rpmLabel != null)
            {
                if (data.EngineRPM > 3000 || data.EngineRPM < 500)
                    rpmLabel.ForeColor = Color.Red;
                else if (data.EngineRPM > 2500)
                    rpmLabel.ForeColor = Color.Yellow;
                else
                    rpmLabel.ForeColor = Color.Lime;
            }

            // Oil pressure warnings
            var oilLabel = FindControl(this, "oilPressureLabel") as Label;
            if (oilLabel != null)
            {
                if (data.OilPressure < 150)
                    oilLabel.ForeColor = Color.Red;
                else if (data.OilPressure < 200)
                    oilLabel.ForeColor = Color.Yellow;
                else
                    oilLabel.ForeColor = Color.Yellow;
            }

            // Fuel level warnings
            var fuelLabel = FindControl(this, "fuelLevelLabel") as Label;
            if (fuelLabel != null)
            {
                if (data.FuelLevel < 10)
                    fuelLabel.ForeColor = Color.Red;
                else if (data.FuelLevel < 25)
                    fuelLabel.ForeColor = Color.Yellow;
                else
                    fuelLabel.ForeColor = Color.Red;
            }

            // Battery voltage warnings
            var batteryLabel = FindControl(this, "batteryVoltageLabel") as Label;
            if (batteryLabel != null)
            {
                if (data.BatteryVoltage < 11.5)
                    batteryLabel.ForeColor = Color.Red;
                else if (data.BatteryVoltage < 12.0)
                    batteryLabel.ForeColor = Color.Yellow;
                else
                    batteryLabel.ForeColor = Color.LightBlue;
            }
        }

        private string GetFuelSystemDescription(string status)
        {
            switch (status)
            {
                case "OPEN_LOOP": return "Malha Aberta";
                case "CLOSED_LOOP": return "Malha Fechada";
                case "OPEN_LOOP_DRIVE": return "Malha Aberta (Dirigindo)";
                case "OPEN_LOOP_FAULT": return "Malha Aberta (Falha)";
                case "CLOSED_LOOP_FAULT": return "Malha Fechada (Falha)";
                default: return status;
            }
        }

        private Control FindControl(Control container, string name)
        {
            if (container.Name == name)
                return container;

            foreach (Control control in container.Controls)
            {
                var found = FindControl(control, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        // Event Handlers
        private void StartStopButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;

            if (!isSimulationRunning)
            {
                dataSimulator.StartSimulation();
                updateTimer.Start();
                isSimulationRunning = true;

                button.Text = "Parar Simulação";
                button.BackColor = Color.Red;

                OnLogMessage("Simulação iniciada.");
            }
            else
            {
                dataSimulator.StopSimulation();
                updateTimer.Stop();
                isSimulationRunning = false;

                button.Text = "Iniciar Simulação";
                button.BackColor = Color.Green;

                OnLogMessage("Simulação parada.");
            }
        }

        private void ClearDtcButton_Click(object sender, EventArgs e)
        {
            dataSimulator.ClearDTCs();
            OnLogMessage("DTCs limpos.");
        }

        private void RPMTrackBar_ValueChanged(object sender, EventArgs e)
        {
            var trackBar = sender as TrackBar;
            var label = FindControl(this, "rpmControlLabel") as Label;

            if (trackBar != null && label != null)
            {
                dataSimulator.SetManualRPM(trackBar.Value);
                label.Text = $"RPM: {trackBar.Value}";
            }
        }

        private void TempTrackBar_ValueChanged(object sender, EventArgs e)
        {
            var trackBar = sender as TrackBar;
            var label = FindControl(this, "tempControlLabel") as Label;

            if (trackBar != null && label != null)
            {
                dataSimulator.SetManualTemperature(trackBar.Value);
                label.Text = $"Temperatura: {trackBar.Value}°C";
            }
        }

        private void SpeedTrackBar_ValueChanged(object sender, EventArgs e)
        {
            var trackBar = sender as TrackBar;
            var label = FindControl(this, "speedControlLabel") as Label;

            if (trackBar != null && label != null)
            {
                dataSimulator.SetManualSpeed(trackBar.Value);
                label.Text = $"Velocidade: {trackBar.Value} km/h";
            }
        }

        private void CriticalMode_CheckedChanged(object sender, EventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                dataSimulator.SetCriticalMode(checkBox.Checked);
                OnLogMessage($"Modo crítico: {(checkBox.Checked ? "Ativado" : "Desativado")}");
            }
        }

        private void DtcMode_CheckedChanged(object sender, EventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                dataSimulator.SetDtcMode(checkBox.Checked);
                OnLogMessage($"Simulação de DTCs: {(checkBox.Checked ? "Ativada" : "Desativada")}");
            }
        }

        private void ManualMode_CheckedChanged(object sender, EventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                dataSimulator.SetManualMode(checkBox.Checked);
                OnLogMessage($"Modo manual: {(checkBox.Checked ? "Ativado" : "Desativado")}");
            }
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            var comPortCombo = FindControl(this, "comPortCombo") as ComboBox;

            if (!bluetoothSimulator.IsConnected)
            {
                if (comPortCombo?.SelectedItem != null)
                {
                    string selectedPort = comPortCombo.SelectedItem.ToString();

                    if (selectedPort != "Nenhuma porta COM disponível")
                    {
                        try
                        {
                            bluetoothSimulator.StartEmulation(selectedPort);
                            button.Text = "Desconectar";
                            button.BackColor = Color.Red;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Erro ao conectar: {ex.Message}", "Erro",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            else
            {
                bluetoothSimulator.StopEmulation();
                button.Text = "Conectar";
                button.BackColor = Color.Blue;
            }
        }

        private void RefreshPortsButton_Click(object sender, EventArgs e)
        {
            RefreshComPorts();
        }

        private async void DiscoverButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;

            button.Enabled = false;
            button.Text = "Descobrindo...";

            try
            {
                var devices = await bluetoothSimulator.DiscoverDevicesAsync();

                if (devices.Count > 0)
                {
                    var deviceDialog = new DeviceSelectionDialog(devices);
                    if (deviceDialog.ShowDialog() == DialogResult.OK && deviceDialog.SelectedDevice != null)
                    {
                        bool connected = await bluetoothSimulator.ConnectToDeviceAsync(deviceDialog.SelectedDevice);

                        if (connected)
                        {
                            var connectButton = FindControl(this, "connectButton") as Button;
                            if (connectButton != null)
                            {
                                connectButton.Text = "Desconectar";
                                connectButton.BackColor = Color.Red;
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Nenhum dispositivo Bluetooth encontrado.", "Descoberta",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro na descoberta: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                button.Enabled = true;
                button.Text = "Descobrir Dispositivos";
            }
        }

        private void RefreshComPorts()
        {
            var comPortCombo = FindControl(this, "comPortCombo") as ComboBox;
            if (comPortCombo != null)
            {
                comPortCombo.Items.Clear();

                var ports = bluetoothSimulator.GetAvailablePorts();
                if (ports.Count > 0)
                {
                    comPortCombo.Items.AddRange(ports.ToArray());
                    comPortCombo.SelectedIndex = 0;
                }
                else
                {
                    comPortCombo.Items.Add("Nenhuma porta COM disponível");
                    comPortCombo.SelectedIndex = 0;
                }
            }
        }

        // Event handlers for simulator events
        private void OnCommandReceived(string command, string response)
        {
            // Already logged in the Bluetooth simulator
        }

        private void OnConnectionStatusChanged(bool isConnected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(OnConnectionStatusChanged), isConnected);
                return;
            }

            var statusLabel = FindControl(this, "bluetoothStatusLabel") as Label;
            if (statusLabel != null)
            {
                if (isConnected)
                {
                    if (bluetoothSimulator.ConnectedDevice != null)
                    {
                        statusLabel.Text = $"Status: Conectado ({bluetoothSimulator.ConnectedDevice.Name})";
                        statusLabel.ForeColor = Color.Green;
                    }
                    else
                    {
                        statusLabel.Text = "Status: Conectado (Serial)";
                        statusLabel.ForeColor = Color.Green;
                    }
                }
                else
                {
                    statusLabel.Text = "Status: Desconectado";
                    statusLabel.ForeColor = Color.Red;
                }
            }
        }

        private void OnDevicesDiscovered(List<BluetoothDevice> devices)
        {
            OnLogMessage($"Descobertos {devices.Count} dispositivos Bluetooth.");
        }

        private void OnLogMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(OnLogMessage), message);
                return;
            }

            var logTextBox = FindControl(this, "logTextBox") as TextBox;
            if (logTextBox != null)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string logEntry = $"[{timestamp}] {message}\r\n";

                logTextBox.AppendText(logEntry);

                // Keep log manageable
                if (logTextBox.Lines.Length > 1000)
                {
                    var lines = logTextBox.Lines.Skip(500).ToArray();
                    logTextBox.Lines = lines;
                }

                logTextBox.ScrollToCaret();
            }
        }

        private void OnDataUpdated(TruckData data)
        {
            // Data updates are handled by the UI timer for better performance
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            dataSimulator?.StopSimulation();
            bluetoothSimulator?.StopEmulation();
            updateTimer?.Stop();

            base.OnFormClosing(e);
        }
    }
}