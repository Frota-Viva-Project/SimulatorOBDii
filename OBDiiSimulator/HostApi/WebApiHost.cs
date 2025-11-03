using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OBDiiSimulator
{
    /// <summary>
    /// Host HTTP simples para a API sem dependência do ASP.NET Core
    /// </summary>
    public static class WebApiHost
    {
        private static HttpListener listener;
        private static bool isRunning = false;
        private static Database database;
        private static TruckDataSimulator dataSimulator;
        private static int port = 5000;
        private static bool databaseInitialized = false;
        private static readonly HttpClient httpClient = new HttpClient();

        public static void Start(string[] args)
        {
            // Tentar portas alternativas se 5000 não funcionar
            int[] portsToTry = { 5000, 5001, 5002, 8080, 8000 };

            foreach (int tryPort in portsToTry)
            {
                if (TryStartOnPort(tryPort))
                {
                    port = tryPort;
                    break;
                }
            }

            if (!isRunning)
            {
                Console.WriteLine("\n❌ NÃO FOI POSSÍVEL INICIAR A API!");
                Console.WriteLine("\n🔧 SOLUÇÕES:");
                Console.WriteLine("   1. Execute como ADMINISTRADOR (botão direito > Executar como admin)");
                Console.WriteLine("   2. OU execute este comando no CMD como Admin:");
                Console.WriteLine("      netsh http add urlacl url=http://+:5000/ user=Everyone");
                Console.WriteLine("   3. Verifique seu Firewall/Antivírus");
                return;
            }

            Console.WriteLine("\n✅ API iniciada com sucesso!");
            Console.WriteLine($"💡 Acesse no navegador: http://localhost:{port}/");
            Console.WriteLine("📌 O banco de dados será conectado na primeira requisição");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();

            // Iniciar processamento assíncrono
            Task.Run(() => HandleRequests());
        }

        private static bool TryStartOnPort(int tryPort)
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{tryPort}/");
                listener.Start();
                isRunning = true;

                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine($"🚀 API INICIADA COM SUCESSO na porta {tryPort}!");
                Console.WriteLine("=".PadRight(60, '='));
                Console.WriteLine($"\n📡 Base URL: http://localhost:{tryPort}");
                Console.WriteLine("\n📋 Endpoints disponíveis:");
                Console.WriteLine($"   GET  http://localhost:{tryPort}/");
                Console.WriteLine($"   POST http://localhost:{tryPort}/api/arduino/create/{{truckId}}");
                Console.WriteLine($"   POST http://localhost:{tryPort}/api/arduino/create");
                Console.WriteLine($"   GET  http://localhost:{tryPort}/api/arduino/test-connection");

                return true;
            }
            catch (HttpListenerException ex)
            {
                if (tryPort == 5000)
                {
                    Console.WriteLine($"\n⚠️ Porta {tryPort} não disponível (Código: {ex.ErrorCode})");
                    Console.WriteLine("   Tentando outras portas...");
                }

                listener?.Close();
                listener = null;
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Erro inesperado na porta {tryPort}: {ex.Message}");
                listener?.Close();
                listener = null;
                return false;
            }
        }

        private static async Task HandleRequests()
        {
            Console.WriteLine("🔄 Aguardando requisições...\n");

            while (isRunning && listener != null && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch (HttpListenerException ex)
                {
                    if (ex.ErrorCode == 64 || ex.ErrorCode == 995)
                    {
                        continue;
                    }

                    if (isRunning)
                    {
                        Console.WriteLine($"⚠️ Listener parou (Código: {ex.ErrorCode})");
                    }
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Console.WriteLine($"❌ Erro ao aguardar requisição: {ex.Message}");
                    }
                }
            }
        }

        private static bool EnsureDatabaseInitialized()
        {
            if (databaseInitialized)
                return true;

            try
            {
                Console.WriteLine("\n🔧 Inicializando banco de dados e simulador...");
                database = new Database();
                dataSimulator = new TruckDataSimulator(1);

                // Registrar callback para enviar notificação sempre que dados forem inseridos
                database.SetNotificationCallback(async (truckId, truckData) =>
                {
                    await SendNotificationAfterInsert(truckId, truckData);
                });

                databaseInitialized = true;
                Console.WriteLine("✅ Inicialização concluída!\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Erro ao inicializar banco de dados:");
                Console.WriteLine($"   {ex.Message}");
                Console.WriteLine($"\n💡 Verifique se o arquivo .env existe e está configurado corretamente.\n");
                return false;
            }
        }

        /// <summary>
        /// Método chamado automaticamente após inserir dados no banco
        /// </summary>
        private static async Task SendNotificationAfterInsert(int truckId, TruckData truckData)
        {
            try
            {
                Console.WriteLine($"   🌐 Buscando usuário do caminhão {truckId} para enviar notificação...");

                // Buscar ID do usuário na API externa
                int? userId = await GetUserIdFromExternalApi(truckId);

                if (!userId.HasValue)
                {
                    Console.WriteLine($"   ⚠️ Usuário não encontrado - notificação não será enviada");
                    return;
                }

                // Enviar notificação FCM
                Console.WriteLine($"   📲 Enviando notificação FCM para usuário {userId.Value}...");
                bool notificationSent = await SendFcmNotification(
                    userId.Value,
                    "Dados do Arduino Recebidos",
                    $"Caminhão #{truckId}: RPM {(int)Math.Round(truckData.EngineRPM)}, Velocidade {Math.Round(truckData.VehicleSpeed, 2)} km/h",
                    truckData
                );

                if (notificationSent)
                {
                    Console.WriteLine($"   ✅ Notificação enviada com sucesso!");
                }
                else
                {
                    Console.WriteLine($"   ⚠️ Falha ao enviar notificação");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao enviar notificação: {ex.Message}");
            }
        }

        private static async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // Configurar CORS
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                // Lidar com preflight OPTIONS
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                string responseString = "";
                string contentType = "application/json";

                string path = request.Url.AbsolutePath.ToLower();
                string method = request.HttpMethod;

                // Log da requisição
                Console.WriteLine($"\n📨 {DateTime.Now:HH:mm:ss} | {method} {path}");

                // ========== ROTEAMENTO ==========

                // Arduino endpoints
                if (path.StartsWith("/api/arduino/create/") && method == "POST")
                {
                    string truckIdStr = path.Replace("/api/arduino/create/", "");
                    responseString = await CreateArduinoRecord(truckIdStr);
                    response.StatusCode = 200;
                }
                else if (path == "/api/arduino/create" && method == "POST")
                {
                    string body = await ReadRequestBody(request);
                    Console.WriteLine($"   📦 Body recebido: {body}");
                    responseString = await CreateArduinoRecordFromBody(body);
                    response.StatusCode = 200;
                }
                else if (path == "/api/arduino/test-connection" && method == "GET")
                {
                    responseString = await TestConnection();
                    response.StatusCode = 200;
                }

                // Root - documentação
                else if (path == "/" || path == "")
                {
                    responseString = GetApiDocumentation();
                    contentType = "text/html; charset=utf-8";
                    response.StatusCode = 200;
                }

                // 404 Not Found
                else
                {
                    Console.WriteLine($"   ❌ Endpoint não encontrado: {method} {path}");
                    response.StatusCode = 404;
                    responseString = CreateJsonResponse(false, $"Endpoint não encontrado: {method} {path}", null);
                }

                // Enviar resposta
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentType = contentType;
                response.ContentLength64 = buffer.Length;
                response.ContentEncoding = Encoding.UTF8;

                try
                {
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    Console.WriteLine($"   ✅ Resposta enviada: HTTP {response.StatusCode}");
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 64 || ex.ErrorCode == 1229)
                {
                    Console.WriteLine($"   ⚠️ Cliente desconectou antes de receber a resposta");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ ERRO: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");

                try
                {
                    if (response.OutputStream.CanWrite)
                    {
                        response.StatusCode = 500;
                        string errorResponse = CreateJsonResponse(false, "Erro interno do servidor", ex.Message);
                        byte[] buffer = Encoding.UTF8.GetBytes(errorResponse);
                        response.ContentType = "application/json";
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                }
                catch (HttpListenerException hex) when (hex.ErrorCode == 64 || hex.ErrorCode == 1229)
                {
                    Console.WriteLine($"   ⚠️ Não foi possível enviar resposta de erro (cliente desconectado)");
                }
                catch { }
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch { }
            }
        }

        private static async Task<string> CreateArduinoRecord(string truckIdStr)
        {
            try
            {
                Console.WriteLine($"   🔍 Processando truckId: {truckIdStr}");

                if (!int.TryParse(truckIdStr, out int truckId) || truckId <= 0)
                {
                    Console.WriteLine($"   ❌ ID inválido: {truckIdStr}");
                    return CreateJsonResponse(false, "ID do caminhão deve ser um número maior que zero", null, 400);
                }

                if (!EnsureDatabaseInitialized())
                {
                    return CreateJsonResponse(false, "Erro ao inicializar banco de dados. Verifique o arquivo .env", null, 503);
                }

                Console.WriteLine("   🔌 Testando conexão com banco...");
                bool isConnected = await database.TestConnectionAsync();
                if (!isConnected)
                {
                    Console.WriteLine("   ❌ Falha na conexão com banco!");
                    return CreateJsonResponse(false, "Não foi possível conectar ao banco de dados", null, 503);
                }

                Console.WriteLine("   ✅ Conectado ao banco!");
                Console.WriteLine("   📊 Gerando dados simulados...");
                var truckData = dataSimulator.GetCurrentData();

                Console.WriteLine("   💾 Inserindo no banco (notificação será enviada automaticamente)...");

                // A notificação FCM será enviada automaticamente pelo callback após inserção
                await database.InsertTruckDataAsync(truckId, truckData);

                var data = new
                {
                    truckId = truckId,
                    velocidade = Math.Round(truckData.VehicleSpeed, 2),
                    rpm = (int)Math.Round(truckData.EngineRPM),
                    temperatura = Math.Round(truckData.CoolantTemp, 2),
                    nivelCombustivel = Math.Round(truckData.FuelLevel, 2),
                    pressaoOleo = Math.Round(truckData.OilPressure, 2),
                    bateria = Math.Round(truckData.BatteryVoltage, 2)
                };

                Console.WriteLine($"   ✅ Registro criado com sucesso!");

                return CreateJsonResponse(
                    true,
                    $"Registro Arduino criado com sucesso para o caminhão ID {truckId}. Notificação FCM enviada automaticamente.",
                    data,
                    200
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro: {ex.Message}");
                Console.WriteLine($"   Stack: {ex.StackTrace}");
                return CreateJsonResponse(false, "Erro ao criar registro Arduino", ex.Message, 500);
            }
        }

        private static async Task<int?> GetUserIdFromExternalApi(int truckId)
        {
            try
            {
                string apiUrl = $"https://api-postgresql-kr87.onrender.com/v1/api/motorista/{truckId}";
                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"   ⚠️ API externa retornou status {response.StatusCode}");
                    return null;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   📥 Resposta da API: {jsonResponse}");

                // Parse manual do JSON para extrair o ID do usuário
                int userId = ExtractUserIdFromJson(jsonResponse);
                Console.WriteLine($"   ✅ ID do usuário encontrado: {userId}");

                return userId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao buscar usuário na API externa: {ex.Message}");
                return null;
            }
        }

        private static int ExtractUserIdFromJson(string json)
        {
            // Procurar especificamente por "id_retorno" que é o campo correto
            int startIndex = json.IndexOf("\"id_retorno\"", StringComparison.OrdinalIgnoreCase);

            // Se não encontrar id_retorno, tentar outras variações
            if (startIndex == -1)
                startIndex = json.IndexOf("\"idRetorno\"", StringComparison.OrdinalIgnoreCase);

            if (startIndex == -1)
                startIndex = json.IndexOf("\"id\"", StringComparison.OrdinalIgnoreCase);

            if (startIndex == -1)
                startIndex = json.IndexOf("\"idUsuario\"", StringComparison.OrdinalIgnoreCase);

            if (startIndex == -1)
            {
                Console.WriteLine($"   ⚠️ JSON completo da API: {json}");
                throw new Exception("Campo 'id_retorno', 'id' ou 'idUsuario' não encontrado no JSON da API");
            }

            int colonIndex = json.IndexOf(":", startIndex);
            int endIndex = json.IndexOfAny(new[] { ',', '}' }, colonIndex);

            string value = json.Substring(colonIndex + 1, endIndex - colonIndex - 1).Trim();
            value = value.Trim('"', ' ');

            if (!int.TryParse(value, out int userId))
                throw new Exception($"Não foi possível converter '{value}' para número");

            Console.WriteLine($"   ✅ Campo encontrado e extraído: userId = {userId}");
            return userId;
        }

        private static async Task<bool> SendFcmNotification(int userId, string titulo, string corpo, TruckData truckData)
        {
            try
            {
                string apiUrl = "https://api-postgresql-kr87.onrender.com/v1/api/fcm/send";

                var notificationData = new
                {
                    idUsuario = userId,
                    titulo = titulo,
                    corpo = corpo,
                    data = new
                    {
                        velocidade = Math.Round(truckData.VehicleSpeed, 2).ToString(),
                        rpm = ((int)Math.Round(truckData.EngineRPM)).ToString(),
                        temperatura = Math.Round(truckData.CoolantTemp, 2).ToString()
                    }
                };

                string jsonPayload = SerializeObject(notificationData);
                Console.WriteLine($"   📤 Payload FCM: {jsonPayload}");

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"   ✅ Notificação FCM enviada com sucesso!");
                    return true;
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"   ❌ Falha ao enviar FCM: {response.StatusCode} - {errorResponse}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao enviar notificação FCM: {ex.Message}");
                return false;
            }
        }

        private static async Task<string> CreateArduinoRecordFromBody(string jsonBody)
        {
            try
            {
                string truckIdStr = ExtractTruckIdFromJson(jsonBody);
                return await CreateArduinoRecord(truckIdStr);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao processar JSON: {ex.Message}");
                return CreateJsonResponse(false, "Erro ao processar requisição", ex.Message, 400);
            }
        }

        private static async Task<string> TestConnection()
        {
            try
            {
                if (!EnsureDatabaseInitialized())
                {
                    return CreateJsonResponse(false, "Erro ao inicializar banco de dados. Verifique o arquivo .env", null, 503);
                }

                Console.WriteLine("   🔌 Testando conexão com banco de dados...");
                bool isConnected = await database.TestConnectionAsync();

                if (isConnected)
                {
                    Console.WriteLine("   ✅ Conexão estabelecida!");
                    return CreateJsonResponse(true, "Conexão com banco de dados estabelecida com sucesso", null);
                }
                else
                {
                    Console.WriteLine("   ❌ Falha na conexão!");
                    return CreateJsonResponse(false, "Falha ao conectar com o banco de dados", null, 503);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro: {ex.Message}");
                return CreateJsonResponse(false, "Erro ao testar conexão", ex.Message, 500);
            }
        }

        private static async Task<string> ReadRequestBody(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private static string CreateJsonResponse(bool success, string message, object data, int statusCode = 200)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            string dataJson = data != null ? $",\"data\":{SerializeObject(data)}" : "";
            string errorJson = !success && data is string error ? $",\"error\":\"{EscapeJson(error)}\"" : "";

            return $"{{\"success\":{success.ToString().ToLower()},\"message\":\"{EscapeJson(message)}\",\"timestamp\":\"{timestamp}\"{dataJson}{errorJson}}}";
        }

        private static string SerializeObject(object obj)
        {
            if (obj == null) return "null";

            var type = obj.GetType();
            if (type.IsPrimitive || type == typeof(string))
            {
                if (obj is string str)
                    return $"\"{EscapeJson(str)}\"";
                return obj.ToString();
            }

            var properties = type.GetProperties();
            var parts = new List<string>();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj);
                string valueStr;

                if (value == null)
                {
                    valueStr = "null";
                }
                else if (value is string s)
                {
                    valueStr = $"\"{EscapeJson(s)}\"";
                }
                else if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    var items = new List<string>();
                    foreach (var item in enumerable)
                    {
                        items.Add(SerializeObject(item));
                    }
                    valueStr = "[" + string.Join(",", items) + "]";
                }
                else
                {
                    valueStr = value.ToString();
                }

                parts.Add($"\"{prop.Name.ToLower()}\":{valueStr}");
            }

            return "{" + string.Join(",", parts) + "}";
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string ExtractTruckIdFromJson(string json)
        {
            int startIndex = json.IndexOf("\"truckId\"", StringComparison.OrdinalIgnoreCase);

            if (startIndex == -1)
                startIndex = json.IndexOf("\"TruckId\"", StringComparison.OrdinalIgnoreCase);

            if (startIndex == -1)
                throw new Exception("Campo 'truckId' não encontrado no JSON");

            int colonIndex = json.IndexOf(":", startIndex);
            int endIndex = json.IndexOfAny(new[] { ',', '}' }, colonIndex);

            string value = json.Substring(colonIndex + 1, endIndex - colonIndex - 1).Trim();
            return value.Trim('"', ' ');
        }

        private static string GetApiDocumentation()
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>OBD-II Simulator API</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Segoe UI', sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; padding: 20px; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; border-radius: 15px; box-shadow: 0 10px 40px rgba(0,0,0,0.2); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; }}
        .header h1 {{ font-size: 2.5em; margin-bottom: 10px; }}
        .content {{ padding: 30px; }}
        .status {{ background: #d4edda; color: #155724; padding: 15px; border-radius: 8px; margin-bottom: 30px; }}
        .test-panel {{ background: #f8f9fa; padding: 25px; border-radius: 10px; margin: 30px 0; }}
        .test-button {{ background: #667eea; color: white; padding: 15px 25px; border: none; border-radius: 8px; cursor: pointer; font-weight: bold; margin: 5px; }}
        .test-button:hover {{ background: #764ba2; }}
        .endpoint {{ background: white; padding: 25px; margin: 20px 0; border-radius: 10px; border-left: 5px solid #667eea; }}
        .method {{ color: white; padding: 6px 15px; border-radius: 20px; display: inline-block; margin-right: 10px; }}
        .post {{ background: #f5576c; }}
        .get {{ background: #00f2fe; }}
        code {{ background: #f8f9fa; padding: 8px 12px; border-radius: 5px; color: #667eea; }}
        pre {{ background: #282c34; color: #abb2bf; padding: 15px; border-radius: 8px; overflow-x: auto; margin: 10px 0; }}
        #resultArduino {{ margin-top: 20px; padding: 20px; border-radius: 8px; display: none; }}
        .success-result {{ background: #d4edda; color: #155724; }}
        .error-result {{ background: #f8d7da; color: #721c24; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🚛 OBD-II Simulator API</h1>
            <p>Sistema Arduino com Notificações FCM</p>
        </div>
        
        <div class='content'>
            <div class='status'>✅ <strong>API Online!</strong> Porta: {port}</div>

            <div class='test-panel'>
                <h2>🤖 Teste o Sistema</h2>
                <input type='number' id='truckId' placeholder='ID do Caminhão' value='1' style='width:100%;padding:12px;margin:10px 0;border:2px solid #ddd;border-radius:8px;'>
                <button class='test-button' onclick='createArduino()'>🚀 Criar Registro</button>
                <button class='test-button' onclick='testConnection()'>🔌 Testar Conexão</button>
                <div id='resultArduino'></div>
            </div>

            <h2 style='color:#667eea;margin:30px 0 20px;'>📚 Endpoint Principal</h2>
            
            <div class='endpoint'>
                <span class='method post'>POST</span>
                <code>/api/arduino/create/{{truckId}}</code>
                <p><strong>Descrição:</strong> Cria registro do Arduino e envia notificação FCM automática</p>
                <p><strong>Fluxo automático:</strong></p>
                <ul style='margin-left:20px;line-height:1.8;'>
                    <li>✅ Gera dados simulados do Arduino</li>
                    <li>✅ Salva no PostgreSQL local</li>
                    <li>✅ Busca ID do usuário (GET /v1/api/motorista/{{truckId}})</li>
                    <li>✅ Envia notificação FCM (POST /v1/api/fcm/send)</li>
                </ul>
                <p><strong>Exemplo cURL:</strong></p>
                <pre>curl -X POST http://localhost:{port}/api/arduino/create/1</pre>
                <p><strong>Resposta:</strong></p>
                <pre>{{
  ""success"": true,
  ""message"": ""Registro criado e notificação enviada"",
  ""data"": {{
    ""truckId"": 1,
    ""userId"": 5,
    ""notificacaoEnviada"": true,
    ""velocidade"": 85.5,
    ""rpm"": 1800,
    ""temperatura"": 92.3
  }}
}}</pre>
            </div>

            <div class='endpoint'>
                <span class='method get'>GET</span>
                <code>/api/arduino/test-connection</code>
                <p><strong>Descrição:</strong> Testa conexão com PostgreSQL</p>
                <pre>curl http://localhost:{port}/api/arduino/test-connection</pre>
            </div>

            <div style='background:#e7f3ff;padding:20px;border-radius:10px;margin-top:30px;'>
                <h3 style='color:#1976d2;'>📡 Integrações Externas</h3>
                <p><strong>API Consulta Usuário:</strong></p>
                <p>GET <code>https://api-postgresql-kr87.onrender.com/v1/api/motorista/{{truckId}}</code></p>
                <p style='margin-top:15px;'><strong>API Notificação FCM:</strong></p>
                <p>POST <code>https://api-postgresql-kr87.onrender.com/v1/api/fcm/send</code></p>
                <pre>{{
  ""idUsuario"": 5,
  ""titulo"": ""Dados do Arduino"",
  ""corpo"": ""RPM 1800, Vel 85km/h"",
  ""data"": {{
    ""velocidade"": ""85.5"",
    ""rpm"": ""1800"",
    ""temperatura"": ""92.3""
  }}
}}</pre>
            </div>
        </div>
    </div>

    <script>
        function showResult(success, data) {{
            const result = document.getElementById('resultArduino');
            result.style.display = 'block';
            result.className = success ? 'success-result' : 'error-result';
            result.innerHTML = '<pre>' + JSON.stringify(data, null, 2) + '</pre>';
        }}

        async function createArduino() {{
            const truckId = document.getElementById('truckId').value;
            try {{
                const response = await fetch(`/api/arduino/create/${{truckId}}`, {{ method: 'POST' }});
                const data = await response.json();
                showResult(data.success, data);
            }} catch (error) {{
                showResult(false, {{ error: error.message }});
            }}
        }}

        async function testConnection() {{
            try {{
                const response = await fetch('/api/arduino/test-connection');
                const data = await response.json();
                showResult(data.success, data);
            }} catch (error) {{
                showResult(false, {{ error: error.message }});
            }}
        }}
    </script>
</body>
</html>";
        }

        public static void Stop()
        {
            Console.WriteLine("\n🛑 Parando API Web...");
            isRunning = false;

            try
            {
                listener?.Stop();
                listener?.Close();
                Console.WriteLine("✅ API Web parada com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erro ao parar API: {ex.Message}");
            }
        }
    }
}