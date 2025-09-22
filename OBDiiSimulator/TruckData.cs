using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OBDiiSimulator
{
    public class TruckData
    {
        // Parâmetros Básicos do Motor
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public double EngineRPM { get; set; } = 900; // RPM mínimo mais realista para diesel
        public double EngineLoad { get; set; } = 5; // Carga mínima em marcha lenta
        public double ThrottlePosition { get; set; } = 0; // Posição do acelerador (%)
        public int EngineRunTime { get; set; } = 0; // Tempo de motor ligado (segundos)

        // Temperaturas
        public double CoolantTemp { get; set; } = 85; // Temperatura do líquido de arrefecimento
        public double IntakeAirTemp { get; set; } = 25; // Temperatura do ar de admissão
        public double TransmissionTemp { get; set; } = 90;

        // Pressões
        public double OilPressure { get; set; } = 450; // Pressão do óleo mais alta para diesel (kPa)
        public double FuelPressure { get; set; } = 500; // Pressão do combustível (kPa)
        public double ManifoldPressure { get; set; } = 30; // Pressão do coletor de admissão (kPa)

        // Parâmetros do Veículo
        public double VehicleSpeed { get; set; } = 0; // Velocidade (km/h)
        public int CurrentGear { get; set; } = 1; // Marcha atual
        public double Mileage { get; set; } = 125000; // Quilometragem (km)

        // Combustível
        public double FuelConsumption { get; set; } = 35; // Consumo realista para caminhão (L/100km)
        public double FuelLevel { get; set; } = 75; // Nível de combustível (%)
        public string FuelSystemStatus { get; set; } = "CLOSED_LOOP"; // Status do sistema

        // Sensores
        public double OxygenSensor1 { get; set; } = 0.45; // Sensor O2 banco 1 (V)
        public double OxygenSensor2 { get; set; } = 0.45; // Sensor O2 banco 2 (V)
        public double BatteryVoltage { get; set; } = 24.0; // Tensão da bateria 24V para caminhão

        // ID do caminhão
        public int TruckId { get; set; } = 1;

        // Códigos de Diagnóstico
        public List<string> ActiveDTCs { get; set; } = new List<string>();
        public List<string> PendingDTCs { get; set; } = new List<string>();

        // Controles internos para simulação
        private Random _random = new Random();
        private DateTime _lastDtcCheck = DateTime.Now;
        private DateTime _lastDtcGeneration = DateTime.Now;

        // Possíveis DTCs para simulação de caminhão
        private static readonly string[] PossibleDTCs = {
            "P0300", // Random/Multiple Cylinder Misfire
            "P0171", // System Too Lean (Bank 1)
            "P0172", // System Too Rich (Bank 1)
            "P0101", // Mass Air Flow Circuit Range/Performance
            "P0113", // Intake Air Temperature Circuit High Input
            "P0118", // Engine Coolant Temperature Circuit High Input
            "P0201", // Injector Circuit Malfunction - Cylinder 1
            "P0325", // Knock Sensor 1 Circuit Malfunction
            "P0340", // Camshaft Position Sensor Circuit Malfunction
            "P0401", // EGR Flow Insufficient
            "P0420", // Catalyst System Efficiency Below Threshold
            "P0505", // Idle Control System Malfunction
            "P0562", // System Voltage Low
            "P0563", // System Voltage High
            "P0602", // Control Module Programming Error
            "P2146", // Fuel Injector Group A Supply Voltage Circuit/Open
        };

        // Construtor - pode inicializar com alguns DTCs se necessário
        public TruckData()
        {
            // Simular condição inicial com possibilidade de DTCs existentes
            if (_random.NextDouble() < 0.3) // 30% chance de começar com algum DTC
            {
                GenerateInitialDTCs();
            }
        }

        // Método para gerar DTCs iniciais
        private void GenerateInitialDTCs()
        {
            int numDtcs = _random.Next(1, 3); // 1 a 2 DTCs iniciais

            for (int i = 0; i < numDtcs; i++)
            {
                string dtc = PossibleDTCs[_random.Next(PossibleDTCs.Length)];

                if (!ActiveDTCs.Contains(dtc) && !PendingDTCs.Contains(dtc))
                {
                    if (_random.NextDouble() < 0.6) // 60% chance de ser pendente inicialmente
                    {
                        PendingDTCs.Add(dtc);
                    }
                    else
                    {
                        ActiveDTCs.Add(dtc);
                    }
                }
            }
        }

        // Método para atualizar DTCs baseado em condições do veículo
        public void UpdateDTCs()
        {
            Timestamp = DateTime.Now;

            // Atualiza DTCs a cada 30 segundos
            if ((DateTime.Now - _lastDtcCheck).TotalSeconds >= 30)
            {
                _lastDtcCheck = DateTime.Now;

                // Promover DTCs pendentes para ativos
                PromotePendingDTCs();

                // Gerar novos DTCs baseados em condições
                GenerateDTCsBasedOnConditions();

                // Ocasionalmente limpar DTCs (simulando reparo)
                if (_random.NextDouble() < 0.05) // 5% chance a cada verificação
                {
                    ClearSomeDTCs();
                }
            }
        }

        // Gerar DTCs baseado nas condições atuais do veículo
        private void GenerateDTCsBasedOnConditions()
        {
            // Só gera novos DTCs se não passou muito tempo desde a última geração
            if ((DateTime.Now - _lastDtcGeneration).TotalMinutes < 5)
                return;

            List<string> potentialDTCs = new List<string>();

            // Condições que podem gerar DTCs específicos
            if (CoolantTemp > 110) // Superaquecimento
            {
                potentialDTCs.Add("P0118");
            }

            if (BatteryVoltage < 22 || BatteryVoltage > 28) // Problemas de voltagem
            {
                potentialDTCs.Add(BatteryVoltage < 22 ? "P0562" : "P0563");
            }

            if (IntakeAirTemp > 60) // Temperatura de admissão alta
            {
                potentialDTCs.Add("P0113");
            }

            if (EngineLoad > 90) // Carga muito alta
            {
                potentialDTCs.Add("P0300"); // Pode causar misfire
            }

            if (FuelLevel < 10) // Combustível baixo pode causar problemas
            {
                potentialDTCs.Add("P0171"); // Sistema lean
            }

            // Adicionar DTCs potenciais ou aleatórios
            if (potentialDTCs.Count > 0)
            {
                string dtc = potentialDTCs[_random.Next(potentialDTCs.Count)];
                AddDTCIfNotExists(dtc);
                _lastDtcGeneration = DateTime.Now;
            }
            else if (_random.NextDouble() < 0.15) // 15% chance de DTC aleatório
            {
                AddRandomDTC();
                _lastDtcGeneration = DateTime.Now;
            }
        }

        // Método auxiliar para adicionar DTC se não existir
        private void AddDTCIfNotExists(string dtc)
        {
            if (!ActiveDTCs.Contains(dtc) && !PendingDTCs.Contains(dtc))
            {
                if (_random.NextDouble() < 0.7) // 70% chance de ir para pendente primeiro
                {
                    PendingDTCs.Add(dtc);
                    Console.WriteLine($"DTC Pendente adicionado: {dtc}");
                }
                else
                {
                    ActiveDTCs.Add(dtc);
                    Console.WriteLine($"DTC Ativo adicionado: {dtc}");
                }
            }
        }

        public void AddRandomDTC()
        {
            string dtc = PossibleDTCs[_random.Next(PossibleDTCs.Length)];
            AddDTCIfNotExists(dtc);
        }

        public void PromotePendingDTCs()
        {
            for (int i = PendingDTCs.Count - 1; i >= 0; i--)
            {
                if (_random.NextDouble() < 0.3) // 30% chance de promover
                {
                    string dtc = PendingDTCs[i];
                    PendingDTCs.RemoveAt(i);
                    if (!ActiveDTCs.Contains(dtc))
                    {
                        ActiveDTCs.Add(dtc);
                        Console.WriteLine($"DTC promovido para ativo: {dtc}");
                    }
                }
            }
        }

        // Limpar alguns DTCs (simulando reparos)
        private void ClearSomeDTCs()
        {
            if (PendingDTCs.Count > 0 && _random.NextDouble() < 0.5)
            {
                string clearedDTC = PendingDTCs[0];
                PendingDTCs.RemoveAt(0);
                Console.WriteLine($"DTC Pendente limpo: {clearedDTC}");
            }

            if (ActiveDTCs.Count > 0 && _random.NextDouble() < 0.3)
            {
                string clearedDTC = ActiveDTCs[0];
                ActiveDTCs.RemoveAt(0);
                Console.WriteLine($"DTC Ativo limpo: {clearedDTC}");
            }
        }

        public void ClearAllDTCs()
        {
            int totalCleared = ActiveDTCs.Count + PendingDTCs.Count;
            ActiveDTCs.Clear();
            PendingDTCs.Clear();
            if (totalCleared > 0)
            {
                Console.WriteLine($"Todos os DTCs foram limpos ({totalCleared} códigos)");
            }
        }

        public int GetTotalDTCCount()
        {
            return ActiveDTCs.Count + PendingDTCs.Count;
        }

        public string GetDTCStatus()
        {
            if (ActiveDTCs.Count > 0)
                return $"ATIVO: {ActiveDTCs.Count}, PENDENTE: {PendingDTCs.Count}";
            else if (PendingDTCs.Count > 0)
                return $"PENDENTE: {PendingDTCs.Count}";
            else
                return "SEM DTCs";
        }

        // Métodos para conversão de DTCs para string (para banco de dados)
        public string GetActiveDTCsAsString()
        {
            return ActiveDTCs.Count > 0 ? string.Join(",", ActiveDTCs) : "";
        }

        public string GetPendingDTCsAsString()
        {
            return PendingDTCs.Count > 0 ? string.Join(",", PendingDTCs) : "";
        }

        // Método para forçar adição de DTCs (útil para testes)
        public void ForceAddDTC(string dtc, bool isActive = false)
        {
            if (isActive)
            {
                if (!ActiveDTCs.Contains(dtc))
                {
                    ActiveDTCs.Add(dtc);
                    Console.WriteLine($"DTC forçado como ativo: {dtc}");
                }
            }
            else
            {
                if (!PendingDTCs.Contains(dtc))
                {
                    PendingDTCs.Add(dtc);
                    Console.WriteLine($"DTC forçado como pendente: {dtc}");
                }
            }
        }

        public string GetOBDResponse(string pid)
        {
            // Atualizar DTCs antes de processar comandos OBD
            UpdateDTCs();

            try
            {
                switch (pid.ToUpper())
                {
                    // Mode 01 - Current Data
                    case "0100": // PIDs supported [01 - 20]
                        return "41 00 BE 1F B8 10";

                    case "0101": // Monitor status since DTCs cleared
                        return $"41 01 {(ActiveDTCs.Count > 0 ? "82" : "07")} 07 65 04";

                    case "0102": // Freeze frame DTC
                        return ActiveDTCs.Count > 0 ? $"41 02 {GetDTCHex(ActiveDTCs[0])}" : "41 02 00 00";

                    case "0103": // Fuel system status
                        return GetFuelSystemStatusResponse();

                    case "0104": // Calculated engine load
                        int loadHex = (int)(EngineLoad * 2.55);
                        return $"41 04 {loadHex:X2}";

                    case "0105": // Engine coolant temperature
                        int tempHex = (int)(CoolantTemp + 40);
                        return $"41 05 {tempHex:X2}";

                    case "0106": // Short term fuel trim - Bank 1
                        return "41 06 80"; // 0% trim

                    case "0107": // Long term fuel trim - Bank 1
                        return "41 07 80"; // 0% trim

                    case "010A": // Fuel pressure (gauge pressure)
                        int fuelPressureHex = (int)(FuelPressure / 3);
                        return $"41 0A {fuelPressureHex:X2}";

                    case "010B": // Intake manifold absolute pressure
                        int manifoldHex = (int)ManifoldPressure;
                        return $"41 0B {manifoldHex:X2}";

                    case "010C": // Engine RPM
                        int rpmHex = (int)(EngineRPM * 4);
                        return $"41 0C {(rpmHex >> 8):X2} {(rpmHex & 0xFF):X2}";

                    case "010D": // Vehicle speed
                        return $"41 0D {(int)VehicleSpeed:X2}";

                    case "010E": // Timing advance
                        return "41 0E 80"; // 0° advance

                    case "010F": // Intake air temperature
                        int intakeTemp = (int)(IntakeAirTemp + 40);
                        return $"41 0F {intakeTemp:X2}";

                    case "0110": // Mass air flow rate
                        int mafRate = (int)((EngineLoad / 100.0) * 655.35);
                        return $"41 10 {(mafRate >> 8):X2} {(mafRate & 0xFF):X2}";

                    case "0111": // Throttle position
                        int throttleHex = (int)(ThrottlePosition * 2.55);
                        return $"41 11 {throttleHex:X2}";

                    case "0112": // Commanded secondary air status
                        return "41 12 01"; // Upstream

                    case "0113": // Oxygen sensors present
                        return "41 13 03"; // Bank 1 sensors 1,2

                    case "0114": // Oxygen Sensor 1
                        int o2_1 = (int)(OxygenSensor1 * 200);
                        return $"41 14 {o2_1:X2} FF";

                    case "0115": // Oxygen Sensor 2
                        int o2_2 = (int)(OxygenSensor2 * 200);
                        return $"41 15 {o2_2:X2} FF";

                    case "011F": // Run time since engine start
                        int runtime = EngineRunTime;
                        return $"41 1F {(runtime >> 8):X2} {(runtime & 0xFF):X2}";

                    case "0120": // PIDs supported [21 - 40]
                        return "41 20 80 05 B0 15";

                    case "0121": // Distance traveled with malfunction indicator lamp (MIL) on
                        return "41 21 00 00";

                    case "0122": // Fuel Rail Pressure (relative to manifold vacuum)
                        int railPressure = (int)(FuelPressure * 0.079);
                        return $"41 22 {(railPressure >> 8):X2} {(railPressure & 0xFF):X2}";

                    case "0123": // Fuel Rail Gauge Pressure (diesel, or gasoline direct injection)
                        int gaugePressure = (int)(FuelPressure * 10);
                        return $"41 23 {(gaugePressure >> 8):X2} {(gaugePressure & 0xFF):X2}";

                    case "012F": // Fuel Tank Level Input
                        int fuelLevelHex = (int)(FuelLevel * 2.55);
                        return $"41 2F {fuelLevelHex:X2}";

                    case "0131": // Distance traveled since codes cleared
                        return "41 31 00 00";

                    case "0140": // PIDs supported [41 - 60]
                        return "41 40 48 00 00 10";

                    case "0142": // Control module voltage
                        int voltageHex = (int)(BatteryVoltage * 1000);
                        return $"41 42 {(voltageHex >> 8):X2} {(voltageHex & 0xFF):X2}";

                    case "0151": // Fuel Type
                        return "41 51 02"; // Diesel

                    // Mode 03 - Request trouble codes
                    case "03":
                        return GetDTCResponse();

                    // Mode 04 - Clear trouble codes
                    case "04":
                        ClearAllDTCs();
                        return "44";

                    // Mode 07 - Request pending trouble codes
                    case "07":
                        return GetPendingDTCResponse();

                    // Mode 09 - Request vehicle information
                    case "0900": // PIDs supported
                        return "49 00 54 40 00 00";

                    case "0902": // Vehicle Identification Number (VIN)
                        return "49 02 01 31 47 43 34 44 35 39 45 46 31 32 33 34 35 36 37"; // Example VIN

                    case "090A": // ECU name
                        return "49 0A 01 45 4C 4D 33 32 37 00 00 00 00 00 00 00 00 00 00 00"; // "ELM327"

                    default:
                        return "NO DATA";
                }
            }
            catch (Exception)
            {
                return "NO DATA";
            }
        }

        private string GetFuelSystemStatusResponse()
        {
            switch (FuelSystemStatus)
            {
                case "OPEN_LOOP":
                    return "41 03 01";
                case "CLOSED_LOOP":
                    return "41 03 02";
                case "OPEN_LOOP_DRIVE":
                    return "41 03 04";
                case "OPEN_LOOP_FAULT":
                    return "41 03 08";
                case "CLOSED_LOOP_FAULT":
                    return "41 03 10";
                default:
                    return "41 03 02"; // Default to closed loop
            }
        }

        private string GetDTCResponse()
        {
            if (ActiveDTCs.Count == 0)
                return "43 00"; // No DTCs

            StringBuilder response = new StringBuilder("43 ");
            response.Append($"{ActiveDTCs.Count:X2}");

            foreach (string dtc in ActiveDTCs)
            {
                response.Append($" {GetDTCHex(dtc)}");
            }

            return response.ToString();
        }

        private string GetPendingDTCResponse()
        {
            if (PendingDTCs.Count == 0)
                return "47 00"; // No pending DTCs

            StringBuilder response = new StringBuilder("47 ");
            response.Append($"{PendingDTCs.Count:X2}");

            foreach (string dtc in PendingDTCs)
            {
                response.Append($" {GetDTCHex(dtc)}");
            }

            return response.ToString();
        }

        private string GetDTCHex(string dtc)
        {
            if (string.IsNullOrEmpty(dtc) || dtc.Length != 5)
                return "00 00";

            // Convert DTC like "P0300" to hex format
            char firstChar = dtc[0];
            string numberPart = dtc.Substring(1);

            int firstByte = 0;
            switch (firstChar)
            {
                case 'P': firstByte = 0x00; break; // Powertrain
                case 'C': firstByte = 0x40; break; // Chassis
                case 'B': firstByte = 0x80; break; // Body
                case 'U': firstByte = 0xC0; break; // Network
            }

            if (int.TryParse(numberPart, out int dtcNumber))
            {
                firstByte |= (dtcNumber >> 8) & 0x3F;
                int secondByte = dtcNumber & 0xFF;
                return $"{firstByte:X2} {secondByte:X2}";
            }

            return "00 00";
        }
    }
}