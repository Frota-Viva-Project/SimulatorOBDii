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

        // Delegate para callback de notificação
        public delegate Task NotificationCallback(int truckId, TruckData truckData);
        private NotificationCallback onDataInserted;

        public Database()
        {
            Console.WriteLine("\n🔍 DIAGNÓSTICO DE CONEXÃO COM POSTGRESQL");
            Console.WriteLine("=".PadRight(60, '='));

            // Tentar carregar .env de múltiplos locais
            string[] possiblePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
                ".env"
            };

            bool envLoaded = false;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Console.WriteLine($"✅ Arquivo .env encontrado em: {path}");
                    try
                    {
                        Env.Load(path);
                        envLoaded = true;
                        Console.WriteLine("✅ Arquivo .env carregado com sucesso!");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Erro ao carregar .env: {ex.Message}");
                    }
                }
            }

            if (!envLoaded)
            {
                Console.WriteLine("⚠️ Arquivo .env NÃO encontrado em nenhum local!");
                Console.WriteLine("📁 Locais verificados:");
                foreach (var path in possiblePaths)
                {
                    Console.WriteLine($"   - {Path.GetFullPath(path)}");
                }
            }

            // Ler variáveis de ambiente
            Console.WriteLine("\n📋 Variáveis de Ambiente:");
            string host = Environment.GetEnvironmentVariable("HOST_BANCO");
            string database = Environment.GetEnvironmentVariable("DATABASE_NAME");
            string username = Environment.GetEnvironmentVariable("USER_NAME");
            string password = Environment.GetEnvironmentVariable("SENHA_BANCO");
            string port = Environment.GetEnvironmentVariable("PORT");

            Console.WriteLine($"   HOST_BANCO: {(string.IsNullOrEmpty(host) ? "❌ NÃO CONFIGURADO" : "✅ " + host)}");
            Console.WriteLine($"   DATABASE_NAME: {(string.IsNullOrEmpty(database) ? "❌ NÃO CONFIGURADO" : "✅ " + database)}");
            Console.WriteLine($"   USER_NAME: {(string.IsNullOrEmpty(username) ? "❌ NÃO CONFIGURADO" : "✅ " + username)}");
            Console.WriteLine($"   SENHA_BANCO: {(string.IsNullOrEmpty(password) ? "❌ NÃO CONFIGURADO" : "✅ [OCULTA]")}");
            Console.WriteLine($"   PORT: {(string.IsNullOrEmpty(port) ? "❌ NÃO CONFIGURADO" : "✅ " + port)}");

            // Validar variáveis
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(database) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(port))
            {
                Console.WriteLine("\n❌ ERRO: Variáveis de ambiente não configuradas!");
                Console.WriteLine("\n🔧 SOLUÇÃO:");
                Console.WriteLine("   Crie um arquivo .env na pasta do projeto com:");
                Console.WriteLine("   HOST_BANCO=seu_host");
                Console.WriteLine("   DATABASE_NAME=seu_banco");
                Console.WriteLine("   USER_NAME=seu_usuario");
                Console.WriteLine("   SENHA_BANCO=sua_senha");
                Console.WriteLine("   PORT=5432");

                throw new InvalidOperationException("Variáveis de ambiente do banco não configuradas corretamente no .env");
            }

            // Construir connection string
            connectionString = $"Host={host};Database={database};Username={username};Password={password};Port={port}";

            Console.WriteLine("\n✅ Connection String construída com sucesso!");
            Console.WriteLine($"   Connection String: Host={host};Database={database};Username={username};Password=***;Port={port}");
            Console.WriteLine("=".PadRight(60, '=') + "\n");
        }

        public Database(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string não pode ser nula ou vazia", nameof(connectionString));

            this.connectionString = connectionString;
        }

        public string GetConnectionString() => connectionString;

        /// <summary>
        /// Registra um callback para ser executado após inserir dados
        /// </summary>
        public void SetNotificationCallback(NotificationCallback callback)
        {
            onDataInserted = callback;
        }

        /// <summary>
        /// Insere os dados do caminhão no banco de dados e dispara notificação
        /// </summary>
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

                Console.WriteLine($"✅ Dados enviados para PostgreSQL às {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                // DISPARAR CALLBACK DE NOTIFICAÇÃO APÓS INSERÇÃO BEM-SUCEDIDA
                if (onDataInserted != null)
                {
                    Console.WriteLine($"📲 Disparando callback de notificação...");
                    try
                    {
                        await onDataInserted(truckId, truckData);
                    }
                    catch (Exception notifEx)
                    {
                        Console.WriteLine($"⚠️ Erro ao enviar notificação (não afeta inserção no BD): {notifEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao inserir dados no PostgreSQL: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Testa a conexão com o banco de dados PostgreSQL
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                Console.WriteLine("\n🔌 Testando conexão com PostgreSQL...");

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    bool isOpen = connection.State == System.Data.ConnectionState.Open;

                    if (isOpen)
                    {
                        Console.WriteLine("✅ Conexão com PostgreSQL estabelecida!");

                        // Testar se a tabela existe
                        try
                        {
                            var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM arduino LIMIT 1", connection);
                            await cmd.ExecuteScalarAsync();
                            Console.WriteLine("✅ Tabela 'arduino' encontrada!");
                        }
                        catch (Exception tableEx)
                        {
                            Console.WriteLine($"⚠️ Tabela 'arduino' não encontrada ou sem permissão: {tableEx.Message}");
                        }
                    }

                    return isOpen;
                }
            }
            catch (NpgsqlException pgEx)
            {
                Console.WriteLine($"❌ Erro PostgreSQL: {pgEx.Message}");
                Console.WriteLine($"   Código: {pgEx.ErrorCode}");

                // Diagnósticos específicos
                if (pgEx.Message.Contains("password authentication failed"))
                {
                    Console.WriteLine("\n🔧 ERRO DE AUTENTICAÇÃO:");
                    Console.WriteLine("   - Verifique USER_NAME e SENHA_BANCO no arquivo .env");
                }
                else if (pgEx.Message.Contains("does not exist"))
                {
                    Console.WriteLine("\n🔧 BANCO NÃO EXISTE:");
                    Console.WriteLine("   - Verifique DATABASE_NAME no arquivo .env");
                }
                else if (pgEx.Message.Contains("Connection refused") || pgEx.Message.Contains("No such host"))
                {
                    Console.WriteLine("\n🔧 ERRO DE CONEXÃO:");
                    Console.WriteLine("   - Verifique HOST_BANCO e PORT no arquivo .env");
                    Console.WriteLine("   - Verifique se o PostgreSQL está rodando");
                    Console.WriteLine("   - Verifique firewall");
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao testar conexão: {ex.Message}");
                Console.WriteLine($"   Tipo: {ex.GetType().Name}");
                return false;
            }
        }

        /// <summary>
        /// Processa os DTCs para ajustar ao limite de caracteres do banco
        /// </summary>
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