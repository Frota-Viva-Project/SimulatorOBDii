using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OBDiiSimulator
{
    public class BluetoothManager
    {
        private BluetoothClient bluetoothClient;
        private BluetoothListener bluetoothListener;
        private List<BluetoothDevice> discoveredDevices;
        private BluetoothDevice connectedDevice;
        private Stream bluetoothStream;
        private bool isDiscovering = false;
        private bool isListening = false;
        private bool isRunning = false;
        private CancellationTokenSource cancellationTokenSource;
        private Random random;

        // Eventos
        public event Action<List<BluetoothDevice>> DevicesDiscovered;
        public event Action<BluetoothDevice> DeviceConnected;
        public event Action<BluetoothDevice> DeviceDisconnected;
        public event Action<string, string> DataReceived;
        public event Action<string> LogMessage;
        public event Action<bool> ConnectionStatusChanged;

        // GUIDs de serviços Bluetooth
        public static readonly Guid SerialPortServiceGuid = BluetoothService.SerialPort;
        public static readonly Guid ObexObjectPushServiceGuid = BluetoothService.ObexObjectPush;
        public static readonly Guid HumanInterfaceDeviceServiceGuid = BluetoothService.HumanInterfaceDevice;
        public static readonly Guid AudioSinkServiceGuid = BluetoothService.AudioSink;
        public static readonly Guid PersonalAreaNetworkGuid = BluetoothService.Panu;

        public BluetoothManager()
        {
            discoveredDevices = new List<BluetoothDevice>();
            bluetoothClient = new BluetoothClient();
            random = new Random();
            cancellationTokenSource = new CancellationTokenSource();
        }

        // Verifica disponibilidade do Bluetooth
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

        // Obtém informações do adaptador Bluetooth
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

        // Descoberta de dispositivos Bluetooth
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
                    await DiscoverRealBluetoothDevices();
                }
                else
                {
                    LogMessage?.Invoke("Bluetooth não disponível. Gerando dispositivos de teste...");
                    await GenerateTestDevices();
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro na descoberta: {ex.Message}");
                await GenerateTestDevices();
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
            LogMessage?.Invoke("Procurando dispositivos Bluetooth reais (até 15 segundos)...");

            var devices = await Task.Run(() =>
            {
                try
                {
                    return bluetoothClient.DiscoverDevices(15, true, true, false);
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro durante descoberta: {ex.Message}");
                    return new BluetoothDeviceInfo[0];
                }
            });

            foreach (var device in devices)
            {
                try
                {
                    var bluetoothDevice = new BluetoothDevice(device);
                    discoveredDevices.Add(bluetoothDevice);
                    LogMessage?.Invoke($"Dispositivo encontrado: {bluetoothDevice.Name} ({bluetoothDevice.Address})");

                    if (device.InstalledServices?.Length > 0)
                    {
                        LogMessage?.Invoke($"  Serviços disponíveis: {device.InstalledServices.Length}");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro ao processar dispositivo: {ex.Message}");
                }
            }

            if (discoveredDevices.Count == 0)
            {
                LogMessage?.Invoke("Nenhum dispositivo real encontrado. Gerando dispositivos de teste...");
                await GenerateTestDevices();
            }
        }

        private async Task GenerateTestDevices()
        {
            string[] testDeviceNames = {
                "OBD-II Scanner Pro", "ELM327 Bluetooth", "Car Diagnostic Tool",
                "OBDII Reader v2.1", "Auto Scanner Plus", "Vehicle Monitor BT",
                "Torque Bluetooth", "CarGuru Scanner", "Diagnostic Interface"
            };

            for (int i = 0; i < 6; i++)
            {
                await Task.Delay(300);

                if (random.NextDouble() < 0.8)
                {
                    string deviceName = testDeviceNames[random.Next(testDeviceNames.Length)];
                    string address = GenerateRandomMacAddress();
                    int signalStrength = random.Next(-85, -25);

                    var device = new BluetoothDevice(deviceName, address, signalStrength);
                    device.AvailableServices.Add(SerialPortServiceGuid);

                    if (random.NextDouble() < 0.3)
                        device.AvailableServices.Add(ObexObjectPushServiceGuid);

                    discoveredDevices.Add(device);
                    LogMessage?.Invoke($"Dispositivo teste criado: {device.Name} - {signalStrength}dBm");
                }
            }
        }

        // Conecta a um dispositivo específico
        public async Task<bool> ConnectToDeviceAsync(BluetoothDevice device)
        {
            if (device == null || isRunning)
                return false;

            LogMessage?.Invoke($"Conectando com {device.Name}...");

            try
            {
                if (device.RealDevice != null)
                {
                    return await ConnectToRealDevice(device);
                }
                else
                {
                    return await ConnectToTestDevice(device);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro na conexão: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ConnectToRealDevice(BluetoothDevice device)
        {
            try
            {
                Guid serviceGuid = SerialPortServiceGuid;

                if (device.AvailableServices.Contains(SerialPortServiceGuid))
                {
                    serviceGuid = SerialPortServiceGuid;
                }
                else if (device.AvailableServices.Any())
                {
                    serviceGuid = device.AvailableServices.First();
                }

                LogMessage?.Invoke($"Usando serviço: {BluetoothService.GetName(serviceGuid)}");

                var endpoint = new BluetoothEndPoint(device.RealDevice.DeviceAddress, serviceGuid);

                bool connected = await Task.Run(() =>
                {
                    try
                    {
                        bluetoothClient.Connect(endpoint);
                        return bluetoothClient.Connected;
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke($"Erro na conexão: {ex.Message}");
                        return false;
                    }
                });

                if (connected)
                {
                    bluetoothStream = bluetoothClient.GetStream();
                    connectedDevice = device;
                    device.IsConnected = true;
                    isRunning = true;

                    LogMessage?.Invoke($"Conectado com sucesso via Bluetooth: {device.Name}");
                    ConnectionStatusChanged?.Invoke(true);
                    DeviceConnected?.Invoke(device);

                    // Inicia loop de comunicação
                    _ = Task.Run(() => CommunicationLoop());
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Falha na conexão: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ConnectToTestDevice(BluetoothDevice device)
        {
            await Task.Delay(1500);

            bool success = device.SignalStrength > -75 && random.NextDouble() < 0.85;

            if (success)
            {
                connectedDevice = device;
                device.IsConnected = true;
                isRunning = true;

                LogMessage?.Invoke($"Conectado (simulado): {device.Name}");
                ConnectionStatusChanged?.Invoke(true);
                DeviceConnected?.Invoke(device);

                return true;
            }
            else
            {
                LogMessage?.Invoke($"Falha na conexão: Sinal fraco ou dispositivo ocupado");
                return false;
            }
        }

        // CORREÇÃO: Loop de comunicação modificado para lidar com Stream sem DataAvailable
        private async void CommunicationLoop()
        {
            byte[] buffer = new byte[1024];

            while (isRunning && bluetoothStream != null && bluetoothClient.Connected)
            {
                try
                {
                    // SOLUÇÃO 1: Usar ReadTimeout e try/catch para verificar dados disponíveis
                    // Configure um timeout pequeno para evitar bloqueio
                    bluetoothStream.ReadTimeout = 100; // 100ms timeout

                    try
                    {
                        int bytesRead = await bluetoothStream.ReadAsync(buffer, 0, buffer.Length,
                            cancellationTokenSource.Token);

                        if (bytesRead > 0)
                        {
                            string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                            if (!string.IsNullOrEmpty(receivedData))
                            {
                                LogMessage?.Invoke($"Dados recebidos: {receivedData}");
                                DataReceived?.Invoke(receivedData, DateTime.Now.ToString("HH:mm:ss"));
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Timeout normal - continua o loop
                    }
                    catch (OperationCanceledException)
                    {
                        // Operação cancelada - sair do loop
                        break;
                    }

                    await Task.Delay(50, cancellationTokenSource.Token);
                }
                catch (IOException)
                {
                    LogMessage?.Invoke("Conexão perdida.");
                    break;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro na comunicação: {ex.Message}");
                    break;
                }
            }

            // Reconexão automática
            if (isRunning && connectedDevice != null)
            {
                LogMessage?.Invoke("Tentando reconectar automaticamente...");
                await Task.Delay(3000);
                await ConnectToDeviceAsync(connectedDevice);
            }
        }

        // SOLUÇÃO ALTERNATIVA: Método para verificar dados disponíveis usando NetworkStream
        private bool HasDataAvailable()
        {
            try
            {
                if (bluetoothStream is NetworkStream networkStream)
                {
                    return networkStream.DataAvailable;
                }

                // Para outros tipos de Stream, use CanRead e Length (se suportado)
                if (bluetoothStream.CanRead && bluetoothStream.CanSeek)
                {
                    return bluetoothStream.Length > bluetoothStream.Position;
                }

                return true; // Assume que há dados se não puder verificar
            }
            catch
            {
                return false;
            }
        }

        // Envia dados via Bluetooth
        public async Task<bool> SendDataAsync(string data)
        {
            if (bluetoothStream == null || !isRunning)
            {
                LogMessage?.Invoke("Nenhuma conexão ativa para envio de dados.");
                return false;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data + "\r\n");
                await bluetoothStream.WriteAsync(bytes, 0, bytes.Length);
                await bluetoothStream.FlushAsync();

                LogMessage?.Invoke($"Dados enviados: {data}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao enviar dados: {ex.Message}");
                return false;
            }
        }

        // Inicia servidor Bluetooth
        public void StartBluetoothServer(Guid serviceGuid = default)
        {
            if (isListening)
            {
                LogMessage?.Invoke("Servidor já está executando.");
                return;
            }

            try
            {
                if (serviceGuid == default(Guid))
                    serviceGuid = SerialPortServiceGuid;

                bluetoothListener = new BluetoothListener(serviceGuid);
                bluetoothListener.Start();
                isListening = true;

                LogMessage?.Invoke($"Servidor iniciado - Serviço: {BluetoothService.GetName(serviceGuid)}");

                _ = Task.Run(AcceptConnections);
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao iniciar servidor: {ex.Message}");
            }
        }

        private async void AcceptConnections()
        {
            while (isListening && bluetoothListener != null)
            {
                try
                {
                    var client = await Task.Run(() => bluetoothListener.AcceptBluetoothClient());
                    LogMessage?.Invoke($"Cliente conectado: {client.RemoteMachineName}");

                    _ = Task.Run(() => HandleClient(client));
                }
                catch (Exception ex)
                {
                    if (isListening)
                        LogMessage?.Invoke($"Erro ao aceitar conexão: {ex.Message}");
                }
            }
        }

        private async void HandleClient(BluetoothClient client)
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
                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        LogMessage?.Invoke($"Servidor recebeu: {receivedData}");

                        string response = $"Echo: {receivedData}";
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\r\n");
                        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro no cliente: {ex.Message}");
            }
            finally
            {
                client?.Close();
                LogMessage?.Invoke("Cliente desconectado do servidor.");
            }
        }

        // Para servidor Bluetooth
        public void StopBluetoothServer()
        {
            isListening = false;

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
        }

        // Desconecta dispositivo atual
        public void Disconnect()
        {
            isRunning = false;
            cancellationTokenSource?.Cancel();

            if (bluetoothStream != null)
            {
                try
                {
                    bluetoothStream.Close();
                    bluetoothStream = null;
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro ao fechar stream: {ex.Message}");
                }
            }

            if (bluetoothClient?.Connected == true)
            {
                try
                {
                    bluetoothClient.Close();
                    LogMessage?.Invoke("Conexão Bluetooth fechada.");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"Erro ao fechar cliente: {ex.Message}");
                }
            }

            if (connectedDevice != null)
            {
                connectedDevice.IsConnected = false;
                DeviceDisconnected?.Invoke(connectedDevice);
                connectedDevice = null;
            }

            ConnectionStatusChanged?.Invoke(false);
        }

        // Obtém serviços de um dispositivo
        public List<Guid> GetDeviceServices(BluetoothDevice device)
        {
            try
            {
                if (device.RealDevice != null)
                {
                    var services = device.RealDevice.InstalledServices;
                    return services?.ToList() ?? new List<Guid>();
                }

                return device.AvailableServices;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Erro ao obter serviços: {ex.Message}");
                return new List<Guid>();
            }
        }

        private string GenerateRandomMacAddress()
        {
            byte[] mac = new byte[6];
            random.NextBytes(mac);
            mac[0] = (byte)(mac[0] & 0xFE);
            return string.Join(":", mac.Select(b => b.ToString("X2")));
        }

        // Propriedades públicas
        public bool IsConnected => isRunning && ((bluetoothClient?.Connected == true) || (connectedDevice?.IsConnected == true));
        public bool IsServerRunning => isListening;
        public BluetoothDevice ConnectedDevice => connectedDevice;
        public List<BluetoothDevice> DiscoveredDevices => new List<BluetoothDevice>(discoveredDevices);

        public void Dispose()
        {
            Disconnect();
            StopBluetoothServer();
            cancellationTokenSource?.Cancel();
            bluetoothClient?.Dispose();
            cancellationTokenSource?.Dispose();
        }
    }
}
