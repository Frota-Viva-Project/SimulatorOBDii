using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace OBDiiSimulator
{
    /// <summary>
    /// Gerencia alertas do Arduino e integração com API externa
    /// </summary>
    public class AlertManager
    {
        private readonly Database database;
        private readonly HttpClient httpClient;
        private readonly string apiBaseUrl = "https://api-postgresql-kr87.onrender.com";
        private readonly AlertThresholds thresholds;

        // Token de autenticação (configure conforme necessário)
        private string authToken;

        public AlertManager(Database database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.httpClient = new HttpClient();
            this.thresholds = new AlertThresholds();

            // Configurar timeout para requisições HTTP
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Configura o token de autenticação para as requisições à API
        /// </summary>
        /// <param name="token">Token JWT</param>
        public void SetAuthToken(string token)
        {
            authToken = token;
            if (!string.IsNullOrEmpty(token))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        /// <summary>
        /// Consulta o ID do caminhão baseado no ID do usuário usando a API EXTERNA
        /// Endpoint: GET /v1/api/motorista/{id}
        /// </summary>
        /// <param name="idUsuario">ID do usuário/motorista</param>
        /// <returns>ID do caminhão ou null se não encontrado</returns>
        public async Task<int?> GetCaminhaoIdByUsuarioAsync(int idUsuario)
        {
            try
            {
                Console.WriteLine($"   🔍 Buscando caminhão via API externa para usuário {idUsuario}...");

                // Chamar API externa: GET /v1/api/motorista/{id}
                var response = await httpClient.GetAsync($"{apiBaseUrl}/v1/api/motorista/{idUsuario}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"   ⚠️ API externa retornou erro: {response.StatusCode}");

                    // Fallback: tentar banco local
                    return await GetCaminhaoIdFromLocalDatabaseAsync(idUsuario);
                }

                var content = await response.Content.ReadAsStringAsync();

                // Parse do JSON para extrair id_caminhao
                var jsonResponse = JObject.Parse(content);

                // Tentar pegar o id_caminhao de diferentes estruturas possíveis
                int? caminhaoId = null;

                if (jsonResponse["id_caminhao"] != null)
                {
                    caminhaoId = jsonResponse["id_caminhao"].ToObject<int>();
                }
                else if (jsonResponse["idCaminhao"] != null)
                {
                    caminhaoId = jsonResponse["idCaminhao"].ToObject<int>();
                }
                else if (jsonResponse["caminhao"] != null && jsonResponse["caminhao"]["id"] != null)
                {
                    caminhaoId = jsonResponse["caminhao"]["id"].ToObject<int>();
                }

                if (caminhaoId.HasValue && caminhaoId.Value > 0)
                {
                    Console.WriteLine($"   ✅ Caminhão encontrado via API externa: ID {caminhaoId.Value}");
                    return caminhaoId.Value;
                }
                else
                {
                    Console.WriteLine($"   ⚠️ Resposta da API não contém id_caminhao válido");

                    // Fallback: tentar banco local
                    return await GetCaminhaoIdFromLocalDatabaseAsync(idUsuario);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Erro ao consultar API externa: {ex.Message}");
                Console.WriteLine("   🔄 Tentando banco local como fallback...");

                // Fallback: tentar banco local
                return await GetCaminhaoIdFromLocalDatabaseAsync(idUsuario);
            }
        }

        /// <summary>
        /// Fallback: consulta o ID do caminhão no banco local (PostgreSQL)
        /// </summary>
        private async Task<int?> GetCaminhaoIdFromLocalDatabaseAsync(int idUsuario)
        {
            try
            {
                using (var connection = new NpgsqlConnection(database.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    var sql = "SELECT id FROM caminhao WHERE id_usuario = @idUsuario LIMIT 1";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("idUsuario", idUsuario);

                        var result = await command.ExecuteScalarAsync();

                        if (result != null && int.TryParse(result.ToString(), out int caminhaoId))
                        {
                            Console.WriteLine($"   ✅ Caminhão encontrado no BD local: ID {caminhaoId}");
                            return caminhaoId;
                        }
                        else
                        {
                            Console.WriteLine($"   ⚠️ Nenhum caminhão encontrado no BD local para usuário {idUsuario}");
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao consultar BD local: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Insere alerta na tabela alertas local
        /// </summary>
        /// <param name="alert">Informações do alerta</param>
        /// <param name="caminhaoId">ID do caminhão</param>
        /// <returns>ID do alerta inserido ou null se falhou</returns>
        private async Task<int?> InsertAlertToLocalDatabaseAsync(AlertInfo alert, int caminhaoId)
        {
            try
            {
                using (var connection = new NpgsqlConnection(database.GetConnectionString()))
                {
                    await connection.OpenAsync();

                    var sql = @"
                        INSERT INTO alertas (status, categoria, titulo, descricao, id_caminhao) 
                        VALUES (@status, @categoria, @titulo, @descricao, @id_caminhao) 
                        RETURNING id";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("status", "ATIVO");
                        command.Parameters.AddWithValue("categoria", alert.Type);
                        command.Parameters.AddWithValue("titulo", GetAlertTitle(alert.Type));
                        command.Parameters.AddWithValue("descricao", alert.Message);
                        command.Parameters.AddWithValue("id_caminhao", caminhaoId);

                        var result = await command.ExecuteScalarAsync();

                        if (result != null && int.TryParse(result.ToString(), out int alertId))
                        {
                            Console.WriteLine($"   ✅ Alerta inserido no BD local: ID {alertId}");
                            return alertId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Erro ao inserir alerta no BD local: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Obtém título do alerta baseado no tipo (compatível com C# 7.3)
        /// </summary>
        private string GetAlertTitle(string alertType)
        {
            if (alertType == "TEMPERATURA_ALTA")
                return "Temperatura do Motor Alta";
            else if (alertType == "PRESSAO_OLEO_BAIXA")
                return "Pressão do Óleo Baixa";
            else if (alertType == "COMBUSTIVEL_BAIXO")
                return "Nível de Combustível Baixo";
            else if (alertType == "BATERIA_BAIXA")
                return "Tensão da Bateria Baixa";
            else if (alertType == "RPM_ALTO")
                return "RPM do Motor Alto";
            else if (alertType == "DTC_ATIVO")
                return "Códigos de Diagnóstico Ativos";
            else
                return "Alerta do Sistema";
        }

        /// <summary>
        /// Verifica se os dados do Arduino indicam algum alerta
        /// </summary>
        /// <param name="truckData">Dados do caminhão</param>
        /// <returns>Lista de alertas detectados</returns>
        public List<AlertInfo> DetectAlerts(TruckData truckData)
        {
            var alerts = new List<AlertInfo>();

            // Verificar temperatura do motor
            if (truckData.CoolantTemp > thresholds.MaxCoolantTemp)
            {
                alerts.Add(new AlertInfo
                {
                    Type = "TEMPERATURA_ALTA",
                    Message = $"Temperatura do motor alta: {truckData.CoolantTemp:F1}°C",
                    Severity = "CRITICO",
                    Value = truckData.CoolantTemp,
                    Threshold = thresholds.MaxCoolantTemp
                });
            }

            // Verificar pressão do óleo
            if (truckData.OilPressure < thresholds.MinOilPressure)
            {
                alerts.Add(new AlertInfo
                {
                    Type = "PRESSAO_OLEO_BAIXA",
                    Message = $"Pressão do óleo baixa: {truckData.OilPressure:F0} kPa",
                    Severity = "CRITICO",
                    Value = truckData.OilPressure,
                    Threshold = thresholds.MinOilPressure
                });
            }

            // Verificar nível de combustível
            if (truckData.FuelLevel < thresholds.MinFuelLevel)
            {
                string severity = truckData.FuelLevel < 5 ? "CRITICO" : "ALERTA";
                alerts.Add(new AlertInfo
                {
                    Type = "COMBUSTIVEL_BAIXO",
                    Message = $"Nível de combustível baixo: {truckData.FuelLevel:F1}%",
                    Severity = severity,
                    Value = truckData.FuelLevel,
                    Threshold = thresholds.MinFuelLevel
                });
            }

            // Verificar tensão da bateria
            if (truckData.BatteryVoltage < thresholds.MinBatteryVoltage)
            {
                alerts.Add(new AlertInfo
                {
                    Type = "BATERIA_BAIXA",
                    Message = $"Tensão da bateria baixa: {truckData.BatteryVoltage:F1}V",
                    Severity = "ALERTA",
                    Value = truckData.BatteryVoltage,
                    Threshold = thresholds.MinBatteryVoltage
                });
            }

            // Verificar RPM muito alto
            if (truckData.EngineRPM > thresholds.MaxEngineRPM)
            {
                alerts.Add(new AlertInfo
                {
                    Type = "RPM_ALTO",
                    Message = $"RPM do motor alto: {truckData.EngineRPM:F0}",
                    Severity = "ALERTA",
                    Value = truckData.EngineRPM,
                    Threshold = thresholds.MaxEngineRPM
                });
            }

            // Verificar DTCs ativos
            if (truckData.ActiveDTCs.Count > 0)
            {
                alerts.Add(new AlertInfo
                {
                    Type = "DTC_ATIVO",
                    Message = $"Códigos de diagnóstico ativos: {string.Join(", ", truckData.ActiveDTCs)}",
                    Severity = "CRITICO",
                    Value = truckData.ActiveDTCs.Count,
                    Threshold = 0
                });
            }

            return alerts;
        }

        /// <summary>
        /// Processa alertas detectados: insere no BD local e envia para API externa
        /// </summary>
        /// <param name="alerts">Lista de alertas</param>
        /// <param name="caminhaoId">ID do caminhão</param>
        /// <param name="truckData">Dados do caminhão</param>
        public async Task ProcessAlertsAsync(List<AlertInfo> alerts, int caminhaoId, TruckData truckData)
        {
            if (alerts.Count == 0) return;

            Console.WriteLine($"\n🚨 {alerts.Count} alerta(s) detectado(s) para caminhão {caminhaoId}:");

            foreach (var alert in alerts)
            {
                Console.WriteLine($"   - {alert.Type}: {alert.Message}");

                try
                {
                    // 1. Inserir alerta na tabela alertas local
                    var alertId = await InsertAlertToLocalDatabaseAsync(alert, caminhaoId);

                    // 2. Inserir alerta na API externa (endpoint /api/alerta/inserir)
                    await InsertAlertToExternalAPIAsync(alert, caminhaoId, truckData);

                    // 3. Enviar notificação FCM (endpoint /api/fcm/send-notification)
                    await SendFCMNotificationAsync(alert, caminhaoId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Erro ao processar alerta {alert.Type}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Insere alerta na API externa usando o endpoint /api/alerta/inserir
        /// </summary>
        private async Task InsertAlertToExternalAPIAsync(AlertInfo alert, int caminhaoId, TruckData truckData)
        {
            try
            {
                // Verificar se há token de autenticação
                if (string.IsNullOrEmpty(authToken))
                {
                    Console.WriteLine("   ⚠️ Token de autenticação não configurado. Alerta NÃO será enviado para API externa.");
                    Console.WriteLine("   💡 Use POST /api/alerts/set-token para configurar o token");
                    return;
                }

                var alertPayload = new
                {
                    status = "ATIVO",
                    categoria = alert.Type,
                    titulo = GetAlertTitle(alert.Type),
                    descricao = alert.Message,
                    id_caminhao = caminhaoId,
                    dadosAdicionais = new
                    {
                        severidade = alert.Severity,
                        valor = alert.Value,
                        limiteDefinido = alert.Threshold,
                        rpm = truckData.EngineRPM,
                        velocidade = truckData.VehicleSpeed,
                        temperatura = truckData.CoolantTemp,
                        pressaoOleo = truckData.OilPressure,
                        nivelCombustivel = truckData.FuelLevel,
                        tensaoBateria = truckData.BatteryVoltage,
                        dataHora = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                    }
                };

                var json = JsonConvert.SerializeObject(alertPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Adicionar header de autenticação
                var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBaseUrl}/api/alerta/inserir")
                {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                Console.WriteLine($"   📤 Enviando alerta para API externa: {apiBaseUrl}/api/alerta/inserir");
                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"   ✅ Alerta inserido na API externa: {alert.Type}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"   ⚠️ Falha ao inserir alerta na API: {response.StatusCode}");
                    Console.WriteLine($"   Erro: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao inserir alerta na API externa: {ex.Message}");
            }
        }

        /// <summary>
        /// Envia notificação FCM usando o endpoint /api/fcm/send-notification
        /// </summary>
        private async Task SendFCMNotificationAsync(AlertInfo alert, int caminhaoId)
        {
            try
            {
                // Verificar se há token de autenticação
                if (string.IsNullOrEmpty(authToken))
                {
                    Console.WriteLine("   ⚠️ Token de autenticação não configurado. Notificação FCM NÃO será enviada.");
                    Console.WriteLine("   💡 Use POST /api/alerts/set-token para configurar o token");
                    return;
                }

                var fcmPayload = new
                {
                    to = "/topics/truck_alerts", // Ou token específico do usuário
                    notification = new
                    {
                        title = $"Alerta - Caminhão {caminhaoId}",
                        body = alert.Message,
                        icon = "ic_warning",
                        sound = "default"
                    },
                    data = new
                    {
                        alert_type = alert.Type,
                        truck_id = caminhaoId.ToString(),
                        severity = alert.Severity,
                        timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                    }
                };

                var json = JsonConvert.SerializeObject(fcmPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Adicionar header de autenticação
                var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBaseUrl}/api/fcm/send-notification")
                {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                Console.WriteLine($"   📤 Enviando notificação FCM: {apiBaseUrl}/api/fcm/send-notification");
                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"   ✅ Notificação FCM enviada: {alert.Type}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"   ⚠️ Falha ao enviar FCM: {response.StatusCode}");
                    Console.WriteLine($"   Erro: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao enviar notificação FCM: {ex.Message}");
            }
        }

        /// <summary>
        /// Monitora dados do Arduino e processa alertas automaticamente
        /// </summary>
        /// <param name="truckData">Dados do caminhão</param>
        /// <param name="idUsuario">ID do usuário</param>
        public async Task MonitorAndProcessAlertsAsync(TruckData truckData, int idUsuario)
        {
            try
            {
                // Consultar ID do caminhão usando id_usuario (via API externa com fallback para BD local)
                var caminhaoId = await GetCaminhaoIdByUsuarioAsync(idUsuario);

                if (!caminhaoId.HasValue)
                {
                    Console.WriteLine($"⚠️ Não foi possível encontrar caminhão para usuário {idUsuario}");
                    return;
                }

                // Detectar alertas nos dados do Arduino
                var alerts = DetectAlerts(truckData);

                // Processar alertas se houver
                if (alerts.Count > 0)
                {
                    await ProcessAlertsAsync(alerts, caminhaoId.Value, truckData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro no monitoramento de alertas: {ex.Message}");
            }
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Informações de um alerta detectado
    /// </summary>
    public class AlertInfo
    {
        public string Type { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; } // CRITICO, ALERTA, INFO
        public double Value { get; set; }
        public double Threshold { get; set; }
    }

    /// <summary>
    /// Limites para detecção de alertas
    /// </summary>
    public class AlertThresholds
    {
        public double MaxCoolantTemp { get; set; } = 110.0; // °C
        public double MinOilPressure { get; set; } = 150.0; // kPa
        public double MinFuelLevel { get; set; } = 15.0; // %
        public double MinBatteryVoltage { get; set; } = 22.0; // V
        public double MaxEngineRPM { get; set; } = 3000.0; // RPM
    }
}