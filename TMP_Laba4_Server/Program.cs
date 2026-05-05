using ProcessController_Server;
using System.IO;
using System.Linq;
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
            Server server = new Server(IPAddress.Loopback, 8888);

            while (true)
            {
                Console.WriteLine("Выберете действия для сервера (1-2):");
                Console.WriteLine("1) Передача структуры отправленного каталога и передача температуры и давления");
                Console.WriteLine("2) Передача состояния технологических установок");

                if (int.TryParse(Console.ReadLine(), out int choice))
                {
                    switch (choice)
                    {
                        case 1:
                            server.Action = (client) =>
                            {
                                var cts = new CancellationTokenSource();

                                Task.Run(() => SendDataTask1(client));
                                Task.Run(() => SendDataTask2(client));

                                // Чтобы метод не завершился сразу
                                while (client.Connected)
                                {
                                    Thread.Sleep(100);
                                }

                                cts.Cancel();
                            };
                            break;
                        case 2:
                            //server.Actions = SendDataTask2;
                            break;
                        default:
                            Console.Clear();
                            Console.WriteLine("Выберете число от 1 до 3");
                            continue;
                    }
                    break;
                }

                Console.Clear();
                Console.WriteLine("Неверный формат! Попробуйте еще раз!");
                continue;
            }

            Console.Clear();
            server.Start();
        }

        static void SendDataTask1(TcpClient client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();
                using var writer = new StreamWriter(stream);
                using var reader = new StreamReader(stream);

                string? path = reader.ReadLine();

                if (string.IsNullOrEmpty(path))
                    return;

                if (!Directory.Exists(path))
                    throw new Exception($"Папка не найдена: {path}");

                var fileSystem = Directory.GetFileSystemEntries(path);

                StringBuilder builder = new StringBuilder();

                foreach (string files in fileSystem)
                {
                    string folderName = Path.GetFileName(files);
                    builder.Append("FILE:" + folderName + '\n');
                }

                writer.Write(builder.ToString());
                writer.WriteLine("END");
                writer.Flush();
            }
            catch (IOException ex) when (ex.Message.Contains("disconnected") || ex.Message.Contains("closed"))
            {
                Console.WriteLine("Клиент отключился во время отправки данных каталога");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в SendDataTask1: {ex.Message}");
            }

        }

        static void SendDataTask2(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                using var writer = new StreamWriter(stream);

                while (client.Connected)
                {
                    double temperature = random.Next(101);
                    double pressure = random.Next(7);

                    string data = $"DATA:{temperature},{pressure}\n";

                    writer.Write(data);
                    writer.Flush();

                    Console.WriteLine($"Отправлено: T={temperature}°C, P={pressure} атм");

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в SendDataTask2: {ex.Message}");
            }
        }
    }
}
