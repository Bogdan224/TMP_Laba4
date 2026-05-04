using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ProcessController_Server
{
    public class Server
    {
        private bool keepProcessing = true;
        private TcpListener listener;
        public Action<TcpClient>? Action { get; set; }

        public Server(IPAddress ipAddress, int port)
        {
            listener = new TcpListener(ipAddress, port);
        }

        public void Start()
        {
            try
            {
                listener.Start();
                Console.WriteLine("Сервер запущен. Ожидание подключения...");
                while (keepProcessing)
                {
                    if (listener.Pending())
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        Task.Run(() => Process(client));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            finally
            {
                listener.Stop();
            }
        }

        private void Process(TcpClient client)
        {
            try
            {
                if (Action == null)
                    throw new Exception("Не выбрано действие для вычислений!");
                Action.Invoke(client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при передаче данных: {ex.Message}\n");
            }
            finally
            {
                client.Close();
                Console.WriteLine("Диспетчер отключился. Ожидание нового подключения...");
            }
        }
    }
}
