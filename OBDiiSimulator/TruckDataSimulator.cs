using System;
using System.Threading;
using System.Threading.Tasks;

namespace OBDiiSimulator
{
    public class TruckDataSimulator
    {
        private TruckData currentData;
        private Random random;
        private bool isRunning = false;
        private bool isCriticalMode = false;
        private bool isDtcMode = false;
        private bool isManualMode = false;

        // Manual control values - valores mais realistas
        private double manualRPM = 1200; // RPM mínimo realista para diesel
        private double manualTemp = 88;
        private double manualSpeed = 0;

        private Task simulationTask;
        private Task databaseTask;
        private CancellationTokenSource cancellationTokenSource;

        // Simulation variables
        private DateTime startTime;
        private double baselineMileage;
        private DateTime lastDatabaseSend;

        // Database configuration
        private readonly Database database;
        private readonly int truckId;

        public event Action<TruckData> DataUpdated;

        public TruckDataSimulator(int truckId)
        {
            this.truckId = truckId;
            this.database = new Database(); // Usa connection string do app.config
            currentData = new TruckData();
            random = new Random();
            startTime = DateTime.Now;
            lastDatabaseSend = DateTime.Now;
            baselineMileage = currentData.Mileage;
        }

        public TruckDataSimulator(int truckId, Database database)
        {
            this.truckId = truckId;
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            currentData = new TruckData();
            random = new Random();
            startTime = DateTime.Now;
            lastDatabaseSend = DateTime.Now;
            baselineMileage = currentData.Mileage;
        }

        public void StartSimulation()
        {
            if (!isRunning)
            {
                isRunning = true;
                startTime = DateTime.Now;
                lastDatabaseSend = DateTime.Now;
                cancellationTokenSource = new CancellationTokenSource();

                simulationTask = Task.Run(() => SimulationLoop(cancellationTokenSource.Token));
                databaseTask = Task.Run(() => DatabaseSendLoop(cancellationTokenSource.Token));
            }
        }

        public void StopSimulation()
        {
            isRunning = false;
            cancellationTokenSource?.Cancel();
        }

        public void SetCriticalMode(bool critical)
        {
            isCriticalMode = critical;
        }

        public void SetDtcMode(bool dtcMode)
        {
            isDtcMode = dtcMode;
        }

        public void SetManualMode(bool manual)
        {
            isManualMode = manual;
        }

        public void SetManualRPM(double rpm)
        {
            manualRPM = Math.Max(1000, rpm); // Mínimo realista para diesel
        }

        public void SetManualTemperature(double temp)
        {
            manualTemp = temp;
        }

        public void SetManualSpeed(double speed)
        {
            manualSpeed = speed;
        }

        public void ClearDTCs()
        {
            currentData.ClearAllDTCs();
        }

        public TruckData GetCurrentData()
        {
            return currentData;
        }

        /// <summary>
        /// Força o envio imediato dos dados para o banco
        /// </summary>
        /// <returns>Task para operação assíncrona</returns>
        public async Task ForceSendToDatabase()
        {
            try
            {
                await database.InsertTruckDataAsync(truckId, currentData);
                lastDatabaseSend = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao forçar envio para o banco: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Testa a conexão com o banco de dados
        /// </summary>
        /// <returns>True se a conexão foi bem-sucedida</returns>
        public async Task<bool> TestDatabaseConnection()
        {
            return await database.TestConnectionAsync();
        }

        private void SimulationLoop(CancellationToken cancellationToken)
        {
            int dtcCounter = 0;

            while (isRunning && !cancellationToken.IsCancellationRequested)
            {
                UpdateData();

                // Update DTCs periodically if in DTC mode
                if (isDtcMode && ++dtcCounter % 50 == 0) // Every 5 seconds
                {
                    if (random.NextDouble() < 0.3) // 30% chance
                    {
                        currentData.AddRandomDTC();
                    }

                    if (random.NextDouble() < 0.2) // 20% chance to promote pending
                    {
                        currentData.PromotePendingDTCs();
                    }
                }

                DataUpdated?.Invoke(currentData);

                try
                {
                    Thread.Sleep(100); // 100ms update interval
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
            }
        }

        private async Task DatabaseSendLoop(CancellationToken cancellationToken)
        {
            while (isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Envia dados a cada 10 minutos
                    if (DateTime.Now - lastDatabaseSend >= TimeSpan.FromMinutes(10))
                    {
                        await database.InsertTruckDataAsync(truckId, currentData);
                        lastDatabaseSend = DateTime.Now;
                    }

                    await Task.Delay(30000, cancellationToken); // Verifica a cada 30 segundos
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao enviar dados para o banco: {ex.Message}");
                }
            }
        }

        private void UpdateData()
        {
            currentData.Timestamp = DateTime.Now;

            // Calculate engine run time
            currentData.EngineRunTime = (int)(DateTime.Now - startTime).TotalSeconds;

            UpdateEngineParameters();
            UpdateTemperatures();
            UpdatePressures();
            UpdateVehicleParameters();
            UpdateFuelParameters();
            UpdateSensorData();
            UpdateElectricalSystems();
        }

        private void UpdateEngineParameters()
        {
            if (isManualMode)
            {
                currentData.EngineRPM = Math.Max(1000, manualRPM); // Força mínimo realista
            }
            else
            {
                if (isCriticalMode)
                {
                    // Critical mode: erratic RPM
                    currentData.EngineRPM = random.NextDouble() > 0.5 ?
                        random.Next(2300, 2800) : random.Next(3200, 3800);
                }
                else
                {
                    // Normal fluctuation - RPM mínimo realista para diesel
                    double baseRpm = 1100 + Math.Sin(DateTime.Now.Millisecond / 1000.0 * Math.PI) * 50;

                    if (currentData.VehicleSpeed > 5)
                    {
                        // Driving RPM - mais baixo para diesel
                        baseRpm = 1300 + (currentData.VehicleSpeed / 120.0) * 1200;
                    }

                    currentData.EngineRPM = baseRpm + random.NextDouble() * 80 - 40;
                    currentData.EngineRPM = Math.Max(1000, Math.Min(3200, currentData.EngineRPM)); // Range realista diesel
                }
            }

            // Engine load based on RPM and speed - mais realista para diesel
            double rpmFactor = Math.Max(0, (currentData.EngineRPM - 1000) / 2200);
            double speedFactor = currentData.VehicleSpeed / 120.0;

            if (isCriticalMode)
            {
                currentData.EngineLoad = random.NextDouble() * 100;
            }
            else
            {
                // Carga mínima em marcha lenta para diesel
                double baseLoad = currentData.VehicleSpeed < 5 ? 12 : 20;
                currentData.EngineLoad = baseLoad + (rpmFactor * 45 + speedFactor * 35) + random.NextDouble() * 15 - 7.5;
                currentData.EngineLoad = Math.Max(8, Math.Min(100, currentData.EngineLoad));
            }

            // Throttle position correlates with engine load
            currentData.ThrottlePosition = (currentData.EngineLoad - 8) * 0.85 + random.NextDouble() * 8;
            currentData.ThrottlePosition = Math.Max(0, Math.Min(100, currentData.ThrottlePosition));
        }

        private void UpdateTemperatures()
        {
            if (isManualMode)
            {
                currentData.CoolantTemp = manualTemp;
            }
            else
            {
                if (isCriticalMode)
                {
                    // Critical temperatures
                    currentData.CoolantTemp = random.NextDouble() > 0.5 ?
                        random.Next(45, 60) : random.Next(125, 150);
                }
                else
                {
                    // Normal operating temperature - diesel opera mais quente
                    double targetTemp = 90 + (currentData.EngineLoad / 100.0) * 18;
                    currentData.CoolantTemp += (targetTemp - currentData.CoolantTemp) * 0.08;
                    currentData.CoolantTemp += random.NextDouble() * 3 - 1.5;
                    currentData.CoolantTemp = Math.Max(75, Math.Min(115, currentData.CoolantTemp));
                }
            }

            // Intake air temperature - mais alta para diesel
            double ambientTemp = 25 + Math.Sin(DateTime.Now.Hour / 24.0 * 2 * Math.PI) * 12;
            currentData.IntakeAirTemp = ambientTemp + (currentData.EngineLoad / 100.0) * 25 + random.NextDouble() * 6;
            currentData.IntakeAirTemp = Math.Max(15, Math.Min(80, currentData.IntakeAirTemp));

            // Transmission temperature
            currentData.TransmissionTemp = 85 + (currentData.EngineLoad / 100.0) * 35 + random.NextDouble() * 12;
            currentData.TransmissionTemp = Math.Max(75, Math.Min(140, currentData.TransmissionTemp));
        }

        private void UpdatePressures()
        {
            // Oil pressure - mais alta para motores diesel
            if (isCriticalMode)
            {
                currentData.OilPressure = random.Next(80, 150);
            }
            else
            {
                double rpmFactor = currentData.EngineRPM / 3000.0;
                currentData.OilPressure = 350 + rpmFactor * 300 + random.NextDouble() * 60;
                currentData.OilPressure = Math.Max(200, Math.Min(800, currentData.OilPressure));
            }

            // Fuel pressure - sistema common rail diesel
            if (isCriticalMode)
            {
                currentData.FuelPressure = random.Next(200, 300);
            }
            else
            {
                currentData.FuelPressure = 1200 + (currentData.EngineLoad / 100.0) * 400 + random.NextDouble() * 100;
                currentData.FuelPressure = Math.Max(800, Math.Min(1800, currentData.FuelPressure));
            }

            // Manifold pressure (MAP) - diferente para diesel (turbo)
            if (currentData.EngineRPM < 1100)
            {
                currentData.ManifoldPressure = 95 + random.NextDouble() * 10; // Próximo atmosférico
            }
            else
            {
                double loadFactor = currentData.ThrottlePosition / 100.0;
                double turboBoost = loadFactor * 80; // Turbo pressure
                currentData.ManifoldPressure = 100 + turboBoost + random.NextDouble() * 15;
                currentData.ManifoldPressure = Math.Max(90, Math.Min(200, currentData.ManifoldPressure));
            }
        }

        private void UpdateVehicleParameters()
        {
            if (isManualMode)
            {
                currentData.VehicleSpeed = manualSpeed;
            }
            else
            {
                // Speed based on RPM and load - considerando peso do caminhão
                if (currentData.EngineRPM < 1100)
                {
                    currentData.VehicleSpeed = Math.Max(0, currentData.VehicleSpeed - 3);
                }
                else
                {
                    double targetSpeed = Math.Max(0, (currentData.EngineRPM - 1100) / 2100.0 * 100);
                    currentData.VehicleSpeed += (targetSpeed - currentData.VehicleSpeed) * 0.08;
                    currentData.VehicleSpeed += random.NextDouble() * 3 - 1.5;
                }
                currentData.VehicleSpeed = Math.Max(0, Math.Min(120, currentData.VehicleSpeed)); // Velocidade máxima caminhão
            }

            // Update mileage based on speed
            if (currentData.VehicleSpeed > 1)
            {
                currentData.Mileage += currentData.VehicleSpeed * (0.1 / 3600.0); // Distance in 100ms
            }

            // Determine current gear based on speed - marchas de caminhão
            if (currentData.VehicleSpeed < 5)
                currentData.CurrentGear = 1;
            else if (currentData.VehicleSpeed < 12)
                currentData.CurrentGear = 2;
            else if (currentData.VehicleSpeed < 20)
                currentData.CurrentGear = 3;
            else if (currentData.VehicleSpeed < 35)
                currentData.CurrentGear = 4;
            else if (currentData.VehicleSpeed < 50)
                currentData.CurrentGear = 5;
            else if (currentData.VehicleSpeed < 70)
                currentData.CurrentGear = 6;
            else if (currentData.VehicleSpeed < 90)
                currentData.CurrentGear = 7;
            else
                currentData.CurrentGear = 8;
        }

        private void UpdateFuelParameters()
        {
            // Fuel consumption based on load and RPM - mais realista para diesel
            double baseConsumption = 25; // L/100km base para caminhão diesel
            double loadFactor = currentData.EngineLoad / 100.0;
            double rpmFactor = Math.Max(0, (currentData.EngineRPM - 1000) / 2200.0);

            currentData.FuelConsumption = baseConsumption + loadFactor * 20 + rpmFactor * 12;
            currentData.FuelConsumption = Math.Max(18, Math.Min(65, currentData.FuelConsumption));

            // Fuel level decreases over time based on consumption
            if (currentData.VehicleSpeed > 1)
            {
                double consumptionRate = currentData.FuelConsumption / 100000.0; // Per 100ms
                currentData.FuelLevel -= consumptionRate * currentData.VehicleSpeed;
                currentData.FuelLevel = Math.Max(0, currentData.FuelLevel);
            }

            // Fuel system status
            if (currentData.FuelLevel < 10)
            {
                currentData.FuelSystemStatus = "OPEN_LOOP_FAULT";
            }
            else if (currentData.CoolantTemp < 75)
            {
                currentData.FuelSystemStatus = "OPEN_LOOP";
            }
            else if (isCriticalMode)
            {
                currentData.FuelSystemStatus = "CLOSED_LOOP_FAULT";
            }
            else
            {
                currentData.FuelSystemStatus = "CLOSED_LOOP";
            }
        }

        private void UpdateSensorData()
        {
            // Oxygen sensors - simulate lambda values para diesel
            if (currentData.FuelSystemStatus == "CLOSED_LOOP")
            {
                // Normal operation around stoichiometric (0.45V)
                currentData.OxygenSensor1 = 0.45 + Math.Sin(DateTime.Now.Millisecond / 100.0) * 0.25;
                currentData.OxygenSensor2 = 0.45 + Math.Sin((DateTime.Now.Millisecond + 500) / 100.0) * 0.25;
            }
            else
            {
                // Open loop or fault conditions
                currentData.OxygenSensor1 = 0.1 + random.NextDouble() * 0.8;
                currentData.OxygenSensor2 = 0.1 + random.NextDouble() * 0.8;
            }

            currentData.OxygenSensor1 = Math.Max(0.1, Math.Min(0.9, currentData.OxygenSensor1));
            currentData.OxygenSensor2 = Math.Max(0.1, Math.Min(0.9, currentData.OxygenSensor2));
        }

        private void UpdateElectricalSystems()
        {
            // Battery voltage - sistema 24V de caminhão
            if (currentData.EngineRPM > 1200)
            {
                // Engine running - alternator charging
                currentData.BatteryVoltage = 27.6 + random.NextDouble() * 1.2;
            }
            else
            {
                // Engine off or idling
                currentData.BatteryVoltage = 24.8 + random.NextDouble() * 1.6;
            }

            if (isCriticalMode)
            {
                currentData.BatteryVoltage = 21.0 + random.NextDouble() * 4.0; // Low voltage
            }

            currentData.BatteryVoltage = Math.Max(18.0, Math.Min(30.0, currentData.BatteryVoltage));
        }
    }
}