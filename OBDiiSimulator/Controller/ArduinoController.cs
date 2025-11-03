using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace OBDiiSimulator.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArduinoController : ControllerBase
    {
        private readonly Database _database;
        private readonly TruckDataSimulator _dataSimulator;

        public ArduinoController()
        {
            _database = new Database();
            _dataSimulator = new TruckDataSimulator(1);
        }

        /// <summary>
        /// Cria um novo registro Arduino para um caminhão específico
        /// </summary>
        /// <param name="truckId">ID do caminhão</param>
        /// <returns>HTTP Status indicando sucesso ou falha</returns>
        [HttpPost("create/{truckId}")]
        public async Task<IActionResult> CreateArduinoRecord(int truckId)
        {
            try
            {
                // Validação do ID do caminhão
                if (truckId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "ID do caminhão deve ser maior que zero",
                        timestamp = DateTime.Now
                    });
                }

                // Testa a conexão com o banco de dados
                bool isConnected = await _database.TestConnectionAsync();
                if (!isConnected)
                {
                    return StatusCode(503, new
                    {
                        success = false,
                        message = "Não foi possível conectar ao banco de dados",
                        timestamp = DateTime.Now
                    });
                }

                // Gera dados simulados do caminhão
                var truckData = _dataSimulator.GetCurrentData();

                // Insere os dados no banco de dados
                await _database.InsertTruckDataAsync(truckId, truckData);

                return Ok(new
                {
                    success = true,
                    message = $"Registro Arduino criado com sucesso para o caminhão ID {truckId}",
                    truckId = truckId,
                    timestamp = DateTime.Now,
                    data = new
                    {
                        velocidade = truckData.VehicleSpeed,
                        rpm = truckData.EngineRPM,
                        temperatura = truckData.CoolantTemp,
                        nivelCombustivel = truckData.FuelLevel
                    }
                });
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Dados do caminhão inválidos",
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erro de configuração do banco de dados",
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erro ao criar registro Arduino",
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Endpoint alternativo usando POST com body JSON
        /// </summary>
        /// <param name="request">Objeto com ID do caminhão</param>
        /// <returns>HTTP Status indicando sucesso ou falha</returns>
        [HttpPost("create")]
        public async Task<IActionResult> CreateArduinoRecordFromBody([FromBody] CreateArduinoRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Requisição inválida",
                    timestamp = DateTime.Now
                });
            }

            return await CreateArduinoRecord(request.TruckId);
        }

        /// <summary>
        /// Testa a conexão com o banco de dados
        /// </summary>
        /// <returns>Status da conexão</returns>
        [HttpGet("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                bool isConnected = await _database.TestConnectionAsync();

                if (isConnected)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Conexão com banco de dados estabelecida com sucesso",
                        timestamp = DateTime.Now
                    });
                }
                else
                {
                    return StatusCode(503, new
                    {
                        success = false,
                        message = "Falha ao conectar com o banco de dados",
                        timestamp = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erro ao testar conexão",
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }
    }

    /// <summary>
    /// Classe para receber dados do body da requisição
    /// </summary>
    public class CreateArduinoRequest
    {
        public int TruckId { get; set; }
    }
}