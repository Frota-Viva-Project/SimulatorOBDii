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

        // Manual control values
        private double manualRPM = 800;
        private double manualTemp = 85;
        private double manualSpeed = 0;

        private Task simulationTask;
        private CancellationTokenSource cancellationTokenSource;

        // Simulation variables
        private DateTime startTime;
        private double baselineMileage;

        public event Action<TruckData> DataUpdated;

        public TruckDataSimulator()
        {
            currentData = new TruckData();
            random = new Random();
            startTime = DateTime.Now;
            baselineMileage = currentData.Mileage;
        }

        public void StartSimulation()
        {
            if (!isRunning)
            {
                isRunning = true;
                startTime = DateTime.Now;
                cancellationTokenSource = new CancellationTokenSource();
                simulationTask = Task.Run(() => SimulationLoop(cancellationTokenSource.Token));
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
            manualRPM = rpm;
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
                currentData.EngineRPM = manualRPM;
            }
            else
            {
                if (isCriticalMode)
                {
                    // Critical mode: erratic RPM
                    currentData.EngineRPM = random.NextDouble() > 0.5 ?
                        random.Next(0, 400) : random.Next(3200, 3800);
                }
                else
                {
                    // Normal fluctuation around idle or driving RPM
                    double baseRpm = 600 + Math.Sin(DateTime.Now.Millisecond / 1000.0 * Math.PI) * 50;
                    if (currentData.VehicleSpeed > 5)
                    {
                        baseRpm = 1000 + (currentData.VehicleSpeed / 120.0) * 1500;
                    }
                    currentData.EngineRPM = baseRpm + random.NextDouble() * 100 - 50;
                    currentData.EngineRPM = Math.Max(500, Math.Min(3500, currentData.EngineRPM));
                }
            }

            // Engine load based on RPM and speed
            double rpmFactor = Math.Max(0, (currentData.EngineRPM - 600) / 2400);
            double speedFactor = currentData.VehicleSpeed / 120.0;

            if (isCriticalMode)
            {
                currentData.EngineLoad = random.NextDouble() * 100;
            }
            else
            {
                currentData.EngineLoad = (rpmFactor * 60 + speedFactor * 40) + random.NextDouble() * 20 - 10;
                currentData.EngineLoad = Math.Max(0, Math.Min(100, currentData.EngineLoad));
            }

            // Throttle position correlates with engine load
            currentData.ThrottlePosition = currentData.EngineLoad * 0.8 + random.NextDouble() * 10;
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
                        random.Next(30, 45) : random.Next(125, 145);
                }
                else
                {
                    // Normal operating temperature with gradual changes
                    double targetTemp = 85 + (currentData.EngineLoad / 100.0) * 15;
                    currentData.CoolantTemp += (targetTemp - currentData.CoolantTemp) * 0.1;
                    currentData.CoolantTemp += random.NextDouble() * 4 - 2;
                    currentData.CoolantTemp = Math.Max(60, Math.Min(110, currentData.CoolantTemp));
                }
            }

            // Intake air temperature
            double ambientTemp = 25 + Math.Sin(DateTime.Now.Hour / 24.0 * 2 * Math.PI) * 10;
            currentData.IntakeAirTemp = ambientTemp + (currentData.EngineLoad / 100.0) * 20 + random.NextDouble() * 5;
            currentData.IntakeAirTemp = Math.Max(10, Math.Min(70, currentData.IntakeAirTemp));

            // Transmission temperature
            currentData.TransmissionTemp = 80 + (currentData.EngineLoad / 100.0) * 30 + random.NextDouble() * 10;
            currentData.TransmissionTemp = Math.Max(70, Math.Min(130, currentData.TransmissionTemp));
        }

        private void UpdatePressures()
        {
            // Oil pressure
            if (isCriticalMode)
            {
                currentData.OilPressure = random.Next(50, 90);
            }
            else
            {
                double rpmFactor = currentData.EngineRPM / 3000.0;
                currentData.OilPressure = 250 + rpmFactor * 250 + random.NextDouble() * 50;
                currentData.OilPressure = Math.Max(150, Math.Min(600, currentData.OilPressure));
            }

            // Fuel pressure
            if (isCriticalMode)
            {
                currentData.FuelPressure = random.Next(100, 180);
            }
            else
            {
                currentData.FuelPressure = 400 + (currentData.EngineLoad / 100.0) * 200 + random.NextDouble() * 50;
                currentData.FuelPressure = Math.Max(300, Math.Min(700, currentData.FuelPressure));
            }

            // Manifold pressure (MAP)
            if (currentData.EngineRPM < 800)
            {
                currentData.ManifoldPressure = 20 + random.NextDouble() * 10; // Vacuum at idle
            }
            else
            {
                double loadFactor = currentData.ThrottlePosition / 100.0;
                currentData.ManifoldPressure = 30 + loadFactor * 70 + random.NextDouble() * 10;
                currentData.ManifoldPressure = Math.Max(15, Math.Min(100, currentData.ManifoldPressure));
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
                // Speed based on RPM and load
                if (currentData.EngineRPM < 700)
                {
                    currentData.VehicleSpeed = Math.Max(0, currentData.VehicleSpeed - 2);
                }
                else
                {
                    double targetSpeed = (currentData.EngineRPM - 600) / 2400.0 * 120;
                    currentData.VehicleSpeed += (targetSpeed - currentData.VehicleSpeed) * 0.1;
                    currentData.VehicleSpeed += random.NextDouble() * 4 - 2;
                }
                currentData.VehicleSpeed = Math.Max(0, Math.Min(150, currentData.VehicleSpeed));
            }

            // Update mileage based on speed
            if (currentData.VehicleSpeed > 1)
            {
                currentData.Mileage += currentData.VehicleSpeed * (0.1 / 3600.0); // Distance in 100ms
            }

            // Determine current gear based on speed
            if (currentData.VehicleSpeed < 5)
                currentData.CurrentGear = 1;
            else if (currentData.VehicleSpeed < 15)
                currentData.CurrentGear = 2;
            else if (currentData.VehicleSpeed < 30)
                currentData.CurrentGear = 3;
            else if (currentData.VehicleSpeed < 50)
                currentData.CurrentGear = 4;
            else if (currentData.VehicleSpeed < 70)
                currentData.CurrentGear = 5;
            else
                currentData.CurrentGear = 6;
        }

        private void UpdateFuelParameters()
        {
            // Fuel consumption based on load and RPM
            double baseConsumption = 20; // L/100km base
            double loadFactor = currentData.EngineLoad / 100.0;
            double rpmFactor = Math.Max(0, (currentData.EngineRPM - 600) / 2400.0);

            currentData.FuelConsumption = baseConsumption + loadFactor * 15 + rpmFactor * 10;
            currentData.FuelConsumption = Math.Max(15, Math.Min(50, currentData.FuelConsumption));

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
            else if (currentData.CoolantTemp < 70)
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
            // Oxygen sensors - simulate lambda values
            if (currentData.FuelSystemStatus == "CLOSED_LOOP")
            {
                // Normal operation around stoichiometric (0.45V)
                currentData.OxygenSensor1 = 0.45 + Math.Sin(DateTime.Now.Millisecond / 100.0) * 0.3;
                currentData.OxygenSensor2 = 0.45 + Math.Sin((DateTime.Now.Millisecond + 500) / 100.0) * 0.3;
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
            // Battery voltage
            if (currentData.EngineRPM > 1000)
            {
                // Engine running - alternator charging
                currentData.BatteryVoltage = 13.8 + random.NextDouble() * 0.6;
            }
            else
            {
                // Engine off or idling
                currentData.BatteryVoltage = 12.4 + random.NextDouble() * 0.8;
            }

            if (isCriticalMode)
            {
                currentData.BatteryVoltage = 10.5 + random.NextDouble() * 2.0; // Low voltage
            }

            currentData.BatteryVoltage = Math.Max(9.0, Math.Min(15.0, currentData.BatteryVoltage));
        }
    }
}