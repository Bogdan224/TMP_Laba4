using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TMP_Laba4_Server
{
    public class Program
    {
        private static Random random = new Random();
        private static bool isRunning = true;

        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Any, 8888);

            try
            {
                server.Start();
                Console.WriteLine("Сервер запущен. Ожидание подключения...");

                while (isRunning)
                {
                    if (server.Pending())
                    {
                        TcpClient client = server.AcceptTcpClient();
                        Task.Run(() => HandleClient(client));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
            finally
            {
                server.Stop();
            }
        }

        static void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();

                while (client.Connected)
                {
                    double temperature = GenerateTemperature();
                    double pressure = GeneratePressure();

                    // Форматирование строки: температура,давление
                    string data = $"{temperature},{pressure}\n";
                    byte[] buffer = Encoding.UTF8.GetBytes(data);

                    // Отправка данных
                    stream.Write(buffer, 0, buffer.Length);
                    
                    Console.WriteLine($"Отправлено: T={temperature:F2}°C, P={pressure:F4} атм");

                    Thread.Sleep(1000);
                }
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

        static double GenerateTemperature()
        {
            return random.Next(101);
        }

        static double GeneratePressure()
        {
            return random.Next(7);
        }
    }
}
