using DotNetEnv;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OBDiiSimulator
{
    internal static class Program
    {
        /// <summary>
        /// Ponto de entrada principal para o aplicativo.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Carrega variáveis de ambiente
            Env.Load();

            var db = new Database();
            Console.WriteLine($"ConnectionString carregada: {db.GetConnectionString()}");

            // MUDANÇA: Sempre inicia a API Web, não apenas quando tem --api
            Console.WriteLine("\n" + "=".PadRight(70, '='));
            Console.WriteLine("🚀 Iniciando OBD-II Simulator");
            Console.WriteLine("=".PadRight(70, '='));

            try
            {
                // Inicia a API Web em uma thread separada
                Console.WriteLine("\n📡 Iniciando API Web...");
                Task.Run(() => WebApiHost.Start(args));

                // Aguarda um momento para a API inicializar
                System.Threading.Thread.Sleep(2000);

                Console.WriteLine("\n✅ API Web iniciada com sucesso!");
                Console.WriteLine("   Acesse: http://localhost:5000");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n⚠️ Aviso: Não foi possível iniciar a API Web");
                Console.WriteLine($"   Erro: {ex.Message}");
                Console.WriteLine("   O programa continuará sem a API Web.");
                Console.WriteLine();
            }

            // Inicia a aplicação Windows Forms
            Console.WriteLine("🖥️ Iniciando interface gráfica...\n");
            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

            // Para a API quando o formulário é fechado
            Console.WriteLine("\n🛑 Encerrando aplicação...");
            WebApiHost.Stop();
        }
    }
}