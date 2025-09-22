using DotNetEnv;
using Npgsql;
using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace OBDiiSimulator
{
    public class Database
    {
        private readonly string connectionString;
        public Database()
        {
            // Garante que o .env foi carregado
            Env.Load();

            string envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            Console.WriteLine($"Procurando .env em: {envPath}");
            Console.WriteLine($"Arquivo existe: {File.Exists(envPath)}");

            if (File.Exists(envPath))
            {
                Console.WriteLine("Conteúdo do .env:");
                Console.WriteLine(File.ReadAllText(envPath));
            }

            string host = Environment.GetEnvironmentVariable("HOST_BANCO");
            string database = Environment.GetEnvironmentVariable("DATABASE_NAME");
            string username = Environment.GetEnvironmentVariable("USER_NAME");
            string password = Environment.GetEnvironmentVariable("SENHA_BANCO");
            string port = Environment.GetEnvironmentVariable("PORT");

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(database) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(port))
            {
                throw new InvalidOperationException("Variáveis de ambiente do banco não configuradas corretamente no .env");
            }

            connectionString = $"Host={host};Database={database};Username={username};Password={password};Port={port}";
        }

        public Database(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string não pode ser nula ou vazia", nameof(connectionString));

            this.connectionString = connectionString;
        }

        public string GetConnectionString() => connectionString;


        /// <summary>
        /// Insere os dados do caminhão no banco de dados
        /// </summary>
        /// <param name="truckId">ID do caminhão</param>
        /// <param name="truckData">Dados do caminhão</param>
        /// <returns>Task para operação assíncrona</returns>
        public async Task InsertTruckDataAsync(int truckId, TruckData truckData)
        {
            if (truckData == null)
            {
                throw new ArgumentNullException(nameof(truckData));
            }

            try
            {
                // Atualizar DTCs antes de enviar para o banco
                truckData.UpdateDTCs();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var sql = @"
INSERT INTO arduino (
    id_caminhao,
    velocidade_veiculo,
    rotacoes_minuto_motor,
    carga_motor,
    tempo_motor_ligado,
    posicao_acelerador,
    consumo_combustivel,
    nivel_combustivel,
    status_sistema_combustivel,
    quilometragem,
    temperatura_liquido_arrefecimento,
    temperatura_ar_admissao,
    pressao_oleo_motor,
    pressao_coletor_admissao,
    leitura_sensores_oxigenio,
    leitura_sensor_oxigenio_2,
    tensao_bateria,
    codigos_diagnostico_ativos,
    codigos_diagnostico_pendentes,
    data_hora_leitura
) VALUES (
    @id_caminhao,
    @velocidade_veiculo,
    @rotacoes_minuto_motor,
    @carga_motor,
    @tempo_motor_ligado,
    @posicao_acelerador,
    @consumo_combustivel,
    @nivel_combustivel,
    @status_sistema_combustivel,
    @quilometragem,
    @temperatura_liquido_arrefecimento,
    @temperatura_ar_admissao,
    @pressao_oleo_motor,
    @pressao_coletor_admissao,
    @leitura_sensores_oxigenio,
    @leitura_sensor_oxigenio_2,
    @tensao_bateria,
    @codigos_diagnostico_ativos,
    @codigos_diagnostico_pendentes,
    @data_hora_leitura
)";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        // Processar DTCs com limitação de caracteres
                        var (activeDtcsString, pendingDtcsString) = ProcessDTCs(truckData);

                        // Adicionar parâmetros com valores arredondados e conversões corretas
                        AddCommandParameters(command, truckId, truckData, activeDtcsString, pendingDtcsString);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                Console.WriteLine($"Dados enviados para o banco de dados às {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao inserir dados no banco: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw para permitir tratamento na camada superior
            }
        }

        /// <summary>
        /// Testa a conexão com o banco de dados
        /// </summary>
        /// <returns>True se a conexão foi bem-sucedida, False caso contrário</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return connection.State == System.Data.ConnectionState.Open;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao testar conexão com o banco: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Processa os DTCs para ajustar ao limite de caracteres do banco
        /// </summary>
        /// <param name="truckData">Dados do caminhão</param>
        /// <returns>Tupla com DTCs ativos e pendentes processados</returns>
        private (string activeDtcs, string pendingDtcs) ProcessDTCs(TruckData truckData)
        {
            string activeDtcsString = "";
            string pendingDtcsString = "";

            try
            {
                // Usar os métodos da classe TruckData e truncar se necessário
                string fullActiveDtcs = truckData.GetActiveDTCsAsString() ?? "";
                string fullPendingDtcs = truckData.GetPendingDTCsAsString() ?? "";

                // Truncar para 10 caracteres máximo (limite da coluna varchar(10))
                activeDtcsString = fullActiveDtcs.Length > 10 ? fullActiveDtcs.Substring(0, 10) : fullActiveDtcs;
                pendingDtcsString = fullPendingDtcs.Length > 10 ? fullPendingDtcs.Substring(0, 10) : fullPendingDtcs;

                // Log do status dos DTCs
                var dtcStatus = truckData.GetDTCStatus();
                Console.WriteLine($"Status DTCs: {dtcStatus}");

                if (!string.IsNullOrEmpty(fullActiveDtcs))
                {
                    Console.WriteLine($"DTCs Ativos (original): {fullActiveDtcs}");
                    if (fullActiveDtcs.Length > 10)
                    {
                        Console.WriteLine($"DTCs Ativos (truncado): {activeDtcsString}");
                    }
                }

                if (!string.IsNullOrEmpty(fullPendingDtcs))
                {
                    Console.WriteLine($"DTCs Pendentes (original): {fullPendingDtcs}");
                    if (fullPendingDtcs.Length > 10)
                    {
                        Console.WriteLine($"DTCs Pendentes (truncado): {pendingDtcsString}");
                    }
                }
            }
            catch (Exception dtcEx)
            {
                Console.WriteLine($"Erro ao processar DTCs: {dtcEx.Message}");
                activeDtcsString = "";
                pendingDtcsString = "";
            }

            return (activeDtcsString, pendingDtcsString);
        }

        /// <summary>
        /// Adiciona os parâmetros ao comando SQL
        /// </summary>
        /// <param name="command">Comando SQL</param>
        /// <param name="truckId">ID do caminhão</param>
        /// <param name="truckData">Dados do caminhão</param>
        /// <param name="activeDtcsString">DTCs ativos processados</param>
        /// <param name="pendingDtcsString">DTCs pendentes processados</param>
        private void AddCommandParameters(NpgsqlCommand command, int truckId, TruckData truckData,
            string activeDtcsString, string pendingDtcsString)
        {
            command.Parameters.AddWithValue("id_caminhao", truckId);
            command.Parameters.AddWithValue("velocidade_veiculo", Math.Round((decimal)truckData.VehicleSpeed, 2));
            command.Parameters.AddWithValue("rotacoes_minuto_motor", (int)Math.Round(truckData.EngineRPM, 0));
            command.Parameters.AddWithValue("carga_motor", Math.Round((decimal)truckData.EngineLoad, 2));
            command.Parameters.AddWithValue("tempo_motor_ligado", truckData.EngineRunTime);
            command.Parameters.AddWithValue("posicao_acelerador", Math.Round((decimal)truckData.ThrottlePosition, 2));
            command.Parameters.AddWithValue("consumo_combustivel", Math.Round((decimal)truckData.FuelConsumption, 2));
            command.Parameters.AddWithValue("nivel_combustivel", Math.Round((decimal)truckData.FuelLevel, 2));
            command.Parameters.AddWithValue("status_sistema_combustivel", truckData.FuelSystemStatus ?? "UNKNOWN");
            command.Parameters.AddWithValue("quilometragem", Math.Round((decimal)truckData.Mileage, 2));
            command.Parameters.AddWithValue("temperatura_liquido_arrefecimento", Math.Round((decimal)truckData.CoolantTemp, 2));
            command.Parameters.AddWithValue("temperatura_ar_admissao", Math.Round((decimal)truckData.IntakeAirTemp, 2));
            command.Parameters.AddWithValue("pressao_oleo_motor", Math.Round((decimal)truckData.OilPressure, 2));
            command.Parameters.AddWithValue("pressao_coletor_admissao", Math.Round((decimal)truckData.ManifoldPressure, 2));
            command.Parameters.AddWithValue("leitura_sensores_oxigenio", Math.Round((decimal)truckData.OxygenSensor1, 2));
            command.Parameters.AddWithValue("leitura_sensor_oxigenio_2", Math.Round((decimal)truckData.OxygenSensor2, 2));
            command.Parameters.AddWithValue("tensao_bateria", Math.Round((decimal)truckData.BatteryVoltage, 2));
            command.Parameters.AddWithValue("codigos_diagnostico_ativos", activeDtcsString);
            command.Parameters.AddWithValue("codigos_diagnostico_pendentes", pendingDtcsString);
            command.Parameters.AddWithValue("data_hora_leitura", truckData.Timestamp);
        }
    }
}