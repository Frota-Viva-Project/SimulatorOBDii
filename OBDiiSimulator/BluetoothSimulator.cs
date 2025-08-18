using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System.IO;

namespace OBDiiSimulator
{
    public class BluetoothDevice
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public int SignalStrength { get; set; }
        public bool IsConnected { get; set; }
        public bool IsAuthenticated { get; set; } // Propriedade adicionada
        public List<Guid> AvailableServices { get; set; }
        public BluetoothDeviceInfo RealDevice { get; set; }

        // Construtor para dispositivos reais
        public BluetoothDevice(BluetoothDeviceInfo deviceInfo)
        {
            if (deviceInfo == null)
                throw new ArgumentNullException(nameof(deviceInfo));

            RealDevice = deviceInfo;
            Name = deviceInfo.DeviceName ?? "Dispositivo Desconhecido";
            Address = deviceInfo.DeviceAddress.ToString();
            SignalStrength = CalculateSignalStrength(deviceInfo);
            IsConnected = deviceInfo.Connected;
            IsAuthenticated = deviceInfo.Authenticated; // Inicializa com base no dispositivo real
            AvailableServices = new List<Guid>();

            // Adicionar serviços instalados
            if (deviceInfo.InstalledServices != null)
            {
                AvailableServices.AddRange(deviceInfo.InstalledServices);
            }
        }

        // Construtor para dispositivos de teste
        public BluetoothDevice(string name, string address, int signalStrength)
        {
            Name = name ?? "Dispositivo Teste";
            Address = address ?? "00:00:00:00:00:00";
            SignalStrength = signalStrength;
            IsConnected = false;
            IsAuthenticated = false; // Inicializa como false para dispositivos simulados
            AvailableServices = new List<Guid>();
            RealDevice = null;
        }

        private int CalculateSignalStrength(BluetoothDeviceInfo deviceInfo)
        {
            // Simula força do sinal baseada na última vez que foi visto
            // Em uma implementação real, você pode usar outros métodos
            Random random = new Random();
            return random.Next(-90, -30); // Valores típicos de RSSI Bluetooth
        }

        private int EstimateSignalStrength(InTheHand.Net.Sockets.BluetoothDeviceInfo device)
        {
            // Como o 32feet.NET não fornece RSSI diretamente, estimamos baseado em outros fatores
            int baseStrength = -60;

            if (device.Connected) baseStrength += 10;
            if (device.Authenticated) baseStrength += 5;
            if (device.Remembered) baseStrength += 5;

            return baseStrength + new Random().Next(-20, 20);
        }

        public bool HasService(Guid serviceGuid)
        {
            return AvailableServices.Contains(serviceGuid);
        }

        // Método ToString() unificado e corrigido
        public override string ToString()
        {
            string status = IsConnected ? "Conectado" : "Disponível";
            string auth = IsAuthenticated ? " (Autenticado)" : "";
            return $"{Name} ({Address}) - {SignalStrength}dBm - {status}{auth}";
        }
    }

    public class BluetoothSimulator
    {
        private SerialPort serialPort;
        private BluetoothClient bluetoothClient;
        private BluetoothListener bluetoothListener;
        private TruckDataSimulator dataSimulator;
        private bool isRunning = false;
        private bool isDiscovering = false;
        private bool isListening = false;
        private List<BluetoothDevice> discoveredDevices;
        private BluetoothDevice connectedDevice;
        private Timer connectionTimer;
        private Random random;
        private CancellationTokenSource cancellationTokenSource;
        private Stream bluetoothStream;

        public event Action<string, string> CommandReceived;
        public event Action<bool> ConnectionStatusChanged;
        public event Action<List<BluetoothDevice>> DevicesDiscovered;
        public event Action<string> LogMessage;
        public event Action<BluetoothDevice> DeviceConnected;
        public event Action<BluetoothDevice> DeviceDisconnected;

        // GUIDs para serviços Bluetooth comuns
        public static readonly Guid SerialPortServiceGuid = BluetoothService.SerialPort;
        public static readonly Guid ObexObjectPushServiceGuid = BluetoothService.ObexObjectPush;
        public static readonly Guid HumanInterfaceDeviceServiceGuid = BluetoothService.HumanInterfaceDevice;

        // Dispositivos simulados para fallback
        private readonly string[] SimulatedDeviceNames = {
            "OBD-II Scanner",
            "ELM327 Bluetooth",
            "Torque Pro",
            "Car Diagnostic",
            "OBDII Reader",
            "Bluetooth OBD",
            "Scanner Tool",
            "Vehicle Monitor",
            "Auto Scanner",
            "Diagnostic Tool"
        };

        public BluetoothSimulator()
        {
            discoveredDevices = new List<BluetoothDevice>();
            random = new Random();
            connectionTimer = new Timer(CheckConnectionHealth, null, Timeout.Infinite, Timeout.Infinite);
            cancellationTokenSource = new CancellationTokenSource();
            bluetoothClient = new BluetoothClient();
        }

        public void SetDataSimulator(TruckDataSimulator simulator)
        {
            dataSimulator = simulator;
        }

        public bool IsBluetoothAvailable()
        {
            try
            {
                return BluetoothRadio.IsSupported && BluetoothRadio.PrimaryRadio != null;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao verificar Bluetooth: {ex.Message}");
                return false;
            }
        }

        public BluetoothRadio GetBluetoothRadio()
        {
            try
            {
                return BluetoothRadio.PrimaryRadio;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao obter rádio Bluetooth: {ex.Message}");
                return null;
            }
        }

        public async Task<List<BluetoothDevice>> DiscoverDevicesAsync()
        {
            if (isDiscovering)
                return discoveredDevices;

            isDiscovering = true;
            discoveredDevices.Clear();

            LogMessage?.Invoke("Iniciando descoberta de dispositivos Bluetooth...");

            try
            {
                if (IsBluetoothAvailable())
                {
                    // Descoberta real de dispositivos Bluetooth
                    await DiscoverRealBluetoothDevices();
                }
                else
                {
                    LogMessage?.Invoke("Bluetooth não disponível. Usando dispositivos simulados...");
                    await DiscoverSimulatedDevices();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro na descoberta: {ex.Message}");
                // Fallback para dispositivos simulados
                await DiscoverSimulatedDevices();
            }
            finally
            {
                isDiscovering = false;
            }

            LogMessage?.Invoke($"Descoberta concluída. {discoveredDevices.Count} dispositivos encontrados.");
            DevicesDiscovered?.Invoke(new List<BluetoothDevice>(discoveredDevices));
            return discoveredDevices;
        }

        private async Task DiscoverRealBluetoothDevices()
        {
            LogMessage?.Invoke("Procurando dispositivos Bluetooth reais...");
            LogMessage?.Invoke("Esta operação pode demorar até 15 segundos...");

            var devices = await Task.Run(() =>
            {
                try
                {
                    return bluetoothClient.DiscoverDevices(15, true, true, false);
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro durante descoberta real: {ex.Message}");
                    return new InTheHand.Net.Sockets.BluetoothDeviceInfo[0];
                }
            });

            foreach (var device in devices)
            {
                try
                {
                    var bluetoothDevice = new BluetoothDevice(device);
                    discoveredDevices.Add(bluetoothDevice);
                    LogMessage?.Invoke($"Dispositivo real encontrado: {bluetoothDevice}");

                    // Lista serviços disponíveis
                    if (device.InstalledServices?.Length > 0)
                    {
                        LogMessage?.Invoke($"  Serviços: {device.InstalledServices.Length}");
                        foreach (var service in device.InstalledServices.Take(3))
                        {
                            string serviceName = BluetoothService.GetName(service) ?? service.ToString();
                            LogMessage?.Invoke($"    - {serviceName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro ao processar dispositivo: {ex.Message}");
                }
            }
        }

        private async Task DiscoverSimulatedDevices()
        {
            LogMessage?.Invoke("Gerando dispositivos simulados...");

            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(500);

                if (random.NextDouble() < 0.7)
                {
                    string deviceName = SimulatedDeviceNames[random.Next(SimulatedDeviceNames.Length)];
                    string address = GenerateRandomMacAddress();
                    int signalStrength = random.Next(-80, -30);

                    var device = new BluetoothDevice(deviceName, address, signalStrength);
                    device.AvailableServices.Add(SerialPortServiceGuid);

                    if (!discoveredDevices.Any(d => d.Address == address))
                    {
                        discoveredDevices.Add(device);
                        LogMessage?.Invoke($"Dispositivo simulado criado: {device}");
                    }
                }
            }
        }

        public async Task<bool> ConnectToDeviceAsync(BluetoothDevice device)
        {
            if (device == null || isRunning)
                return false;

            LogMessage?.Invoke($"Tentando conectar com {device.Name}...");

            try
            {
                if (device.RealDevice != null)
                {
                    // Conexão real via Bluetooth
                    return await ConnectToRealBluetoothDevice(device);
                }
                else
                {
                    // Conexão simulada
                    return await ConnectToSimulatedDevice(device);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro na conexão: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ConnectToRealBluetoothDevice(BluetoothDevice device)
        {
            try
            {
                // Determina o serviço a usar (Serial Port por padrão)
                Guid serviceGuid = SerialPortServiceGuid;
                if (device.AvailableServices.Contains(SerialPortServiceGuid))
                {
                    serviceGuid = SerialPortServiceGuid;
                }
                else if (device.AvailableServices.Any())
                {
                    serviceGuid = device.AvailableServices.First();
                }

                LogMessage?.Invoke($"Conectando via serviço: {BluetoothService.GetName(serviceGuid)}");

                var endpoint = new BluetoothEndPoint(device.RealDevice.DeviceAddress, serviceGuid);

                var connected = await Task.Run(() =>
                {
                    try
                    {
                        bluetoothClient.Connect(endpoint);
                        return bluetoothClient.Connected;
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke($"Erro na conexão Bluetooth: {ex.Message}");
                        return false;
                    }
                });

                if (connected)
                {
                    bluetoothStream = bluetoothClient.GetStream();
                    connectedDevice = device;
                    device.IsConnected = true;
                    isRunning = true;

                    LogMessage?.Invoke($"Conectado via Bluetooth com {device.Name}");
                    ConnectionStatusChanged?.Invoke(true);
                    DeviceConnected?.Invoke(device);

                    // Inicia monitoramento de dados
                    _ = Task.Run(() => BluetoothDataLoop());
                    connectionTimer.Change(5000, 5000);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro na conexão Bluetooth: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ConnectToSimulatedDevice(BluetoothDevice device)
        {
            await Task.Delay(2000); // Simula tempo de conexão

            bool connectionSuccess = device.SignalStrength > -70 && random.NextDouble() < 0.8;

            if (connectionSuccess)
            {
                connectedDevice = device;
                device.IsConnected = true;
                isRunning = true;

                LogMessage?.Invoke($"Conectado (simulado) com {device.Name}");
                ConnectionStatusChanged?.Invoke(true);
                DeviceConnected?.Invoke(device);

                connectionTimer.Change(5000, 5000);
                return true;
            }
            else
            {
                LogMessage?.Invoke($"Falha na conexão com {device.Name}. Sinal fraco ou dispositivo indisponível.");
                return false;
            }
        }

        private async void BluetoothDataLoop()
        {
            byte[] buffer = new byte[1024];

            while (isRunning && bluetoothStream != null && bluetoothClient.Connected &&
                   !cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Usa ReadAsync com timeout através de CancellationToken
                    using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
                    using (var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationTokenSource.Token, timeoutCts.Token))
                    {
                        try
                        {
                            int bytesRead = await bluetoothStream.ReadAsync(buffer, 0, buffer.Length, combinedCts.Token);
                            if (bytesRead > 0)
                            {
                                string command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                                if (!string.IsNullOrEmpty(command))
                                {
                                    string response = ProcessCommand(command);

                                    byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\r");
                                    await bluetoothStream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationTokenSource.Token);
                                    await bluetoothStream.FlushAsync();

                                    CommandReceived?.Invoke(command, response);
                                    LogMessage?.Invoke($"BT CMD: {command} -> RSP: {response}");
                                }
                            }
                        }
                        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                        {
                            // Timeout normal, continua o loop
                            continue;
                        }
                    }

                    await Task.Delay(50, cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) when (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Operação cancelada pelo token principal, sair do loop
                    break;
                }
                catch (IOException ioEx)
                {
                    LogMessage?.Invoke($"Conexão Bluetooth perdida: {ioEx.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro na comunicação Bluetooth: {ex.Message}");
                    break;
                }
            }

            // Reconecta automaticamente se a conexão for perdida
            if (isRunning && connectedDevice != null && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                LogMessage?.Invoke("Tentando reconectar...");
                try
                {
                    await Task.Delay(2000, cancellationTokenSource.Token);
                    if (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await ConnectToDeviceAsync(connectedDevice);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancelado durante a reconexão
                }
            }
        }

        public void StartEmulation(string portName)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    StopEmulation();
                }

                serialPort = new SerialPort(portName, 38400, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    NewLine = "\r",
                    DtrEnable = true,
                    RtsEnable = true
                };

                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.ErrorReceived += SerialPort_ErrorReceived;

                serialPort.Open();
                isRunning = true;

                LogMessage?.Invoke($"Emulação Serial iniciada na porta {portName}");
                ConnectionStatusChanged?.Invoke(true);

                connectionTimer.Change(5000, 5000);
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao abrir porta {portName}: {ex.Message}");
                throw new Exception($"Erro ao abrir porta {portName}: {ex.Message}");
            }
        }

        public void StartBluetoothServer(Guid serviceGuid = default)
        {
            if (isListening)
            {
                LogMessage?.Invoke("Servidor Bluetooth já está ativo.");
                return;
            }

            try
            {
                if (serviceGuid == default(Guid))
                {
                    serviceGuid = SerialPortServiceGuid;
                }

                bluetoothListener = new BluetoothListener(serviceGuid);
                bluetoothListener.Start();
                isListening = true;

                LogMessage?.Invoke($"Servidor Bluetooth iniciado com serviço: {BluetoothService.GetName(serviceGuid)}");

                _ = Task.Run(AcceptBluetoothConnections);
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao iniciar servidor Bluetooth: {ex.Message}");
            }
        }

        private async void AcceptBluetoothConnections()
        {
            while (isListening && bluetoothListener != null)
            {
                try
                {
                    var client = await Task.Run(() => bluetoothListener.AcceptBluetoothClient());
                    LogMessage?.Invoke($"Cliente Bluetooth conectado: {client.RemoteMachineName}");

                    _ = Task.Run(() => HandleBluetoothClient(client));
                }
                catch (Exception ex)
                {
                    if (isListening)
                    {
                        LogMessage?.Invoke($"Erro ao aceitar conexão Bluetooth: {ex.Message}");
                    }
                }
            }
        }

        private async void HandleBluetoothClient(BluetoothClient client)
        {
            try
            {
                var stream = client.GetStream();
                byte[] buffer = new byte[1024];

                while (client.Connected && isListening)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        if (!string.IsNullOrEmpty(command))
                        {
                            string response = ProcessCommand(command);

                            byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\r");
                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                            await stream.FlushAsync();

                            CommandReceived?.Invoke(command, response);
                            LogMessage?.Invoke($"Server CMD: {command} -> RSP: {response}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro no cliente Bluetooth: {ex.Message}");
            }
            finally
            {
                client?.Close();
                LogMessage?.Invoke("Cliente Bluetooth desconectado.");
            }
        }

        public void StopEmulation()
        {
            isRunning = false;
            isListening = false;

            connectionTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Para conexão serial
            if (serialPort?.IsOpen == true)
            {
                try
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.ErrorReceived -= SerialPort_ErrorReceived;
                    serialPort.Close();
                    LogMessage?.Invoke("Conexão serial fechada.");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro ao fechar serial: {ex.Message}");
                }
                finally
                {
                    serialPort?.Dispose();
                    serialPort = null;
                }
            }

            // Para conexão Bluetooth
            if (bluetoothClient?.Connected == true)
            {
                try
                {
                    bluetoothStream?.Close();
                    bluetoothClient.Close();
                    LogMessage?.Invoke("Conexão Bluetooth fechada.");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro ao fechar Bluetooth: {ex.Message}");
                }
            }

            // Para servidor Bluetooth
            if (bluetoothListener != null)
            {
                try
                {
                    bluetoothListener.Stop();
                    bluetoothListener = null;
                    LogMessage?.Invoke("Servidor Bluetooth parado.");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro ao parar servidor: {ex.Message}");
                }
            }

            if (connectedDevice != null)
            {
                connectedDevice.IsConnected = false;
                DeviceDisconnected?.Invoke(connectedDevice);
                connectedDevice = null;
                LogMessage?.Invoke("Dispositivo desconectado.");
            }

            ConnectionStatusChanged?.Invoke(false);
        }

        public bool SendBluetoothData(string data)
        {
            if (bluetoothStream == null || !bluetoothClient.Connected)
            {
                LogMessage?.Invoke("Nenhuma conexão Bluetooth ativa.");
                return false;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data + "\r");
                bluetoothStream.Write(bytes, 0, bytes.Length);
                bluetoothStream.Flush();

                LogMessage?.Invoke($"Dados enviados via Bluetooth: {data}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao enviar dados Bluetooth: {ex.Message}");
                return false;
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!isRunning || serialPort == null || !serialPort.IsOpen)
                return;

            try
            {
                string command = serialPort.ReadExisting().Trim();
                if (!string.IsNullOrEmpty(command))
                {
                    string response = ProcessCommand(command);

                    Thread.Sleep(random.Next(50, 200));

                    serialPort.WriteLine(response);
                    CommandReceived?.Invoke(command, response);

                    LogMessage?.Invoke($"Serial CMD: {command} -> RSP: {response}");
                }
            }
            catch (TimeoutException)
            {
                LogMessage?.Invoke("Timeout na comunicação serial.");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro na comunicação serial: {ex.Message}");
            }
        }

        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            LogMessage?.Invoke($"Erro na porta serial: {e.EventType}");
        }

        private void CheckConnectionHealth(object state)
        {
            if (!isRunning) return;

            try
            {
                // Verifica saúde da conexão serial
                if (serialPort != null && serialPort.IsOpen)
                {
                    if (random.NextDouble() < 0.05)
                    {
                        LogMessage?.Invoke("Instabilidade detectada na conexão serial.");
                    }
                }

                // Verifica saúde da conexão Bluetooth
                if (bluetoothClient?.Connected == true && connectedDevice != null)
                {
                    connectedDevice.SignalStrength += random.Next(-10, 10);
                    connectedDevice.SignalStrength = Math.Max(-90, Math.Min(-20, connectedDevice.SignalStrength));

                    if (connectedDevice.SignalStrength < -85)
                    {
                        LogMessage?.Invoke("Sinal Bluetooth muito fraco. Tentando reconectar...");
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(1000);
                            await ConnectToDeviceAsync(connectedDevice);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro no monitoramento: {ex.Message}");
            }
        }

        private string ProcessCommand(string command)
        {
            command = command.ToUpper().Replace(" ", "").Replace("\r", "").Replace("\n", "");

            Thread.Sleep(random.Next(20, 100));

            if (command.StartsWith("AT"))
            {
                return ProcessATCommand(command);
            }

            if (command.Length >= 4)
            {
                TruckData currentData = GetCurrentTruckData();

                if (random.NextDouble() < 0.1)
                {
                    Thread.Sleep(500);
                    return "SEARCHING...\r" + currentData.GetOBDResponse(command);
                }

                return currentData.GetOBDResponse(command);
            }

            return "?";
        }

        private string ProcessATCommand(string command)
        {
            switch (command)
            {
                case "ATZ":
                    Thread.Sleep(1500);
                    return "ELM327 v2.1\r>";

                case "ATRV":
                    var data = GetCurrentTruckData();
                    return $"{data.BatteryVoltage:F1}V";

                case "ATDP":
                    return connectedDevice?.RealDevice != null ? "Bluetooth Connection" : "AUTO, ISO 15765-4 (CAN 11/500)";

                case "ATDPN":
                    return "A6";

                case "ATSP0":
                case "ATSP6":
                case "ATSP7":
                case "ATSP8":
                case "ATSP9":
                case "ATE0":
                case "ATE1":
                case "ATL0":
                case "ATL1":
                case "ATH0":
                case "ATH1":
                case "ATS0":
                case "ATS1":
                case "ATMA":
                case "ATAT0":
                case "ATAT1":
                case "ATAT2":
                case "ATST":
                case "ATKW":
                    return "OK";

                case "ATWS":
                    return "ELM327 v2.1 Bluetooth Enhanced";

                case "ATIGN":
                    return GetCurrentTruckData().EngineRPM > 500 ? "ON" : "OFF";

                case "ATVD":
                    return GetCurrentTruckData().BatteryVoltage.ToString("F1") + "V";

                default:
                    return "?";
            }
        }

        private TruckData GetCurrentTruckData()
        {
            return dataSimulator?.GetCurrentData() ?? new TruckData();
        }

        private string GenerateRandomMacAddress()
        {
            byte[] mac = new byte[6];
            random.NextBytes(mac);
            mac[0] = (byte)(mac[0] & 0xFE);
            return string.Join(":", mac.Select(b => b.ToString("X2")));
        }

        public List<string> GetAvailablePorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames().ToList();
                LogMessage?.Invoke($"Portas COM disponíveis: {string.Join(", ", ports)}");
                return ports;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao listar portas: {ex.Message}");
                return new List<string>();
            }
        }

        public List<Guid> GetDeviceServices(BluetoothDevice device)
        {
            try
            {
                if (device.RealDevice != null)
                {
                    var services = device.RealDevice.InstalledServices;
                    LogMessage?.Invoke($"Serviços em {device.Name}: {services?.Length ?? 0}");

                    if (services != null)
                    {
                        foreach (var service in services)
                        {
                            string serviceName = BluetoothService.GetName(service) ?? service.ToString();
                            LogMessage?.Invoke($"  - {serviceName}");
                        }
                    }

                    return services?.ToList() ?? new List<Guid>();
                }
                else
                {
                    return device.AvailableServices;
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao obter serviços: {ex.Message}");
                return new List<Guid>();
            }
        }

        public bool IsConnected => isRunning && ((serialPort?.IsOpen == true) ||
                                   (bluetoothClient?.Connected == true) ||
                                   (connectedDevice?.IsConnected == true));

        public BluetoothDevice ConnectedDevice => connectedDevice;
        public List<BluetoothDevice> DiscoveredDevices => new List<BluetoothDevice>(discoveredDevices);
        public bool IsBluetoothConnected => bluetoothClient?.Connected == true;
        public bool IsSerialConnected => serialPort?.IsOpen == true;
        public bool IsServerRunning => isListening;

        public void Dispose()
        {
            StopEmulation();
            cancellationTokenSource?.Cancel();
            connectionTimer?.Dispose();
            bluetoothClient?.Dispose();
            cancellationTokenSource?.Dispose();
        }
    }
}