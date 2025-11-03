using Microsoft.AspNetCore.Mvc;
using System.IO.Ports;
using System.Text.Json;

namespace OBDiiApiWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArduinoController : ControllerBase
    {
        private static SerialPort? _serialPort;
        private static readonly object _lockObject = new object();
        private readonly ILogger<ArduinoController> _logger;

        public ArduinoController(ILogger<ArduinoController> logger)
        {
            _logger = logger;
        }

        [HttpGet("ports")]
        public ActionResult<IEnumerable<string>> GetAvailablePorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames();
                _logger.LogInformation($"Found {ports.Length} available ports");
                return Ok(ports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available ports");
                return StatusCode(500, "Error retrieving available ports");
            }
        }

        [HttpPost("connect")]
        public ActionResult ConnectToArduino([FromBody] ArduinoConnectionRequest request)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_serialPort?.IsOpen == true)
                    {
                        return BadRequest("Arduino is already connected");
                    }

                    _serialPort = new SerialPort(request.PortName, request.BaudRate)
                    {
                        Parity = Parity.None,
                        DataBits = 8,
                        StopBits = StopBits.One,
                        Handshake = Handshake.None,
                        ReadTimeout = 3000,
                        WriteTimeout = 3000
                    };

                    _serialPort.Open();
                    _logger.LogInformation($"Connected to Arduino on port {request.PortName}");
                    
                    return Ok(new { message = "Connected successfully", port = request.PortName });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error connecting to Arduino on port {request.PortName}");
                return StatusCode(500, $"Failed to connect: {ex.Message}");
            }
        }

        [HttpPost("disconnect")]
        public ActionResult DisconnectFromArduino()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_serialPort?.IsOpen == true)
                    {
                        _serialPort.Close();
                        _serialPort.Dispose();
                        _serialPort = null;
                        _logger.LogInformation("Disconnected from Arduino");
                        return Ok(new { message = "Disconnected successfully" });
                    }
                    
                    return BadRequest("No Arduino connection found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from Arduino");
                return StatusCode(500, $"Failed to disconnect: {ex.Message}");
            }
        }  
      [HttpGet("status")]
        public ActionResult GetConnectionStatus()
        {
            try
            {
                lock (_lockObject)
                {
                    var isConnected = _serialPort?.IsOpen == true;
                    var portName = isConnected ? _serialPort?.PortName : null;
                    
                    return Ok(new 
                    { 
                        isConnected = isConnected,
                        portName = portName,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection status");
                return StatusCode(500, "Error retrieving connection status");
            }
        }

        [HttpPost("send")]
        public ActionResult SendCommand([FromBody] ArduinoCommandRequest request)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_serialPort?.IsOpen != true)
                    {
                        return BadRequest("Arduino is not connected");
                    }

                    _serialPort.WriteLine(request.Command);
                    _logger.LogInformation($"Sent command to Arduino: {request.Command}");
                    
                    return Ok(new { message = "Command sent successfully", command = request.Command });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending command: {request.Command}");
                return StatusCode(500, $"Failed to send command: {ex.Message}");
            }
        }

        [HttpGet("read")]
        public ActionResult ReadFromArduino()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_serialPort?.IsOpen != true)
                    {
                        return BadRequest("Arduino is not connected");
                    }

                    if (_serialPort.BytesToRead > 0)
                    {
                        var data = _serialPort.ReadExisting();
                        _logger.LogInformation($"Read data from Arduino: {data}");
                        
                        return Ok(new { data = data, timestamp = DateTime.UtcNow });
                    }
                    
                    return Ok(new { data = "", message = "No data available" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from Arduino");
                return StatusCode(500, $"Failed to read data: {ex.Message}");
            }
        }

        [HttpPost("obd/send")]
        public ActionResult SendObdCommand([FromBody] ObdCommandRequest request)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_serialPort?.IsOpen != true)
                    {
                        return BadRequest("Arduino is not connected");
                    }

                    // Format OBD-II command
                    var obdCommand = $"AT {request.Mode:X2} {request.Pid:X2}";
                    _serialPort.WriteLine(obdCommand);
                    
                    // Wait for response
                    Thread.Sleep(request.DelayMs);
                    
                    var response = "";
                    if (_serialPort.BytesToRead > 0)
                    {
                        response = _serialPort.ReadExisting();
                    }

                    _logger.LogInformation($"OBD Command: {obdCommand}, Response: {response}");
                    
                    return Ok(new 
                    { 
                        command = obdCommand,
                        response = response,
                        mode = request.Mode,
                        pid = request.Pid,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending OBD command: Mode {request.Mode:X2}, PID {request.Pid:X2}");
                return StatusCode(500, $"Failed to send OBD command: {ex.Message}");
            }
        }        [H
ttpGet("obd/pids")]
        public ActionResult GetSupportedPids()
        {
            var commonPids = new[]
            {
                new { Mode = 1, Pid = 0x00, Name = "PIDs supported [01-20]", Description = "Bit encoded" },
                new { Mode = 1, Pid = 0x01, Name = "Monitor status since DTCs cleared", Description = "Bit encoded" },
                new { Mode = 1, Pid = 0x04, Name = "Calculated engine load", Description = "%" },
                new { Mode = 1, Pid = 0x05, Name = "Engine coolant temperature", Description = "°C" },
                new { Mode = 1, Pid = 0x06, Name = "Short term fuel trim—Bank 1", Description = "%" },
                new { Mode = 1, Pid = 0x07, Name = "Long term fuel trim—Bank 1", Description = "%" },
                new { Mode = 1, Pid = 0x0C, Name = "Engine speed", Description = "rpm" },
                new { Mode = 1, Pid = 0x0D, Name = "Vehicle speed", Description = "km/h" },
                new { Mode = 1, Pid = 0x0F, Name = "Intake air temperature", Description = "°C" },
                new { Mode = 1, Pid = 0x10, Name = "Mass air flow sensor (MAF) air flow rate", Description = "grams/sec" },
                new { Mode = 1, Pid = 0x11, Name = "Throttle position", Description = "%" },
                new { Mode = 1, Pid = 0x1F, Name = "Run time since engine start", Description = "seconds" },
                new { Mode = 1, Pid = 0x21, Name = "Distance traveled with malfunction indicator lamp (MIL) on", Description = "km" },
                new { Mode = 1, Pid = 0x2F, Name = "Fuel Tank Level Input", Description = "%" },
                new { Mode = 1, Pid = 0x33, Name = "Absolute Barometric Pressure", Description = "kPa" },
                new { Mode = 1, Pid = 0x42, Name = "Control module voltage", Description = "V" },
                new { Mode = 1, Pid = 0x43, Name = "Absolute load value", Description = "%" },
                new { Mode = 1, Pid = 0x44, Name = "Commanded Air-Fuel Equivalence Ratio", Description = "ratio" },
                new { Mode = 1, Pid = 0x45, Name = "Relative throttle position", Description = "%" },
                new { Mode = 1, Pid = 0x46, Name = "Ambient air temperature", Description = "°C" },
                new { Mode = 1, Pid = 0x47, Name = "Absolute throttle position B", Description = "%" },
                new { Mode = 1, Pid = 0x49, Name = "Accelerator pedal position D", Description = "%" },
                new { Mode = 1, Pid = 0x4A, Name = "Accelerator pedal position E", Description = "%" },
                new { Mode = 1, Pid = 0x4C, Name = "Commanded throttle actuator", Description = "%" },
                new { Mode = 1, Pid = 0x51, Name = "Fuel Type", Description = "From fuel type table" },
                new { Mode = 1, Pid = 0x52, Name = "Ethanol fuel %", Description = "%" }
            };

            return Ok(commonPids);
        }

        [HttpPost("simulate")]
        public ActionResult SimulateObdData([FromBody] SimulationRequest request)
        {
            try
            {
                var simulatedData = new List<object>();
                var random = new Random();

                foreach (var pid in request.Pids)
                {
                    var value = pid switch
                    {
                        0x0C => random.Next(800, 6000), // Engine RPM
                        0x0D => random.Next(0, 120), // Vehicle Speed (km/h)
                        0x05 => random.Next(80, 105), // Coolant Temperature (°C)
                        0x0F => random.Next(15, 50), // Intake Air Temperature (°C)
                        0x04 => random.Next(10, 95), // Engine Load (%)
                        0x11 => random.Next(0, 100), // Throttle Position (%)
                        0x2F => random.Next(10, 100), // Fuel Level (%)
                        0x42 => Math.Round(random.NextDouble() * (14.4 - 11.0) + 11.0, 1), // Battery Voltage
                        _ => random.Next(0, 255)
                    };

                    simulatedData.Add(new
                    {
                        pid = $"0x{pid:X2}",
                        value = value,
                        timestamp = DateTime.UtcNow
                    });
                }

                _logger.LogInformation($"Generated simulation data for {request.Pids.Length} PIDs");
                
                return Ok(new
                {
                    simulatedData = simulatedData,
                    duration = request.DurationSeconds,
                    interval = request.IntervalMs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating simulation data");
                return StatusCode(500, $"Failed to generate simulation: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_lockObject)
                {
                    if (_serialPort?.IsOpen == true)
                    {
                        _serialPort.Close();
                        _serialPort.Dispose();
                        _serialPort = null;
                    }
                }
            }
            base.Dispose(disposing);
        }
    }

    // Request models
    public class ArduinoConnectionRequest
    {
        public string PortName { get; set; } = string.Empty;
        public int BaudRate { get; set; } = 9600;
    }

    public class ArduinoCommandRequest
    {
        public string Command { get; set; } = string.Empty;
    }

    public class ObdCommandRequest
    {
        public int Mode { get; set; } = 1;
        public int Pid { get; set; }
        public int DelayMs { get; set; } = 100;
    }

    public class SimulationRequest
    {
        public int[] Pids { get; set; } = Array.Empty<int>();
        public int DurationSeconds { get; set; } = 60;
        public int IntervalMs { get; set; } = 1000;
    }
}