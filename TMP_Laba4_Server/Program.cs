using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using ProcessController_Server;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
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
                            server.Action = (client, stream) =>
                            {
                                using StreamWriter writer = new StreamWriter(stream);
                                using StreamReader reader = new StreamReader(stream);

                                Task.Run(() => SendDirectoryContent(client, stream, writer, reader));
                                Task.Run(() => SendTemperatureAndPressure(client, stream, writer));

                                while (client.Connected)
                                {
                                    Thread.Sleep(100);
                                }
                            };
                            break;
                        case 2:
                            InitializeInstallationsList(out List<TechInstallation> installations);

                            server.Action = (client, stream) =>
                            {
                                using StreamWriter writer = new StreamWriter(stream);

                                Task.Run(() => SendInstallationsState(client, stream, installations, writer));

                                while (client.Connected)
                                {
                                    Thread.Sleep(100);
                                }
                            };
                            break;
                        default:
                            Console.Clear();
                            Console.WriteLine("Выберете число от 1 до 2");
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

        static void InitializeInstallationsList(out List<TechInstallation> installations)
        {
            var dirInfo = new DirectoryInfo(Directory.GetCurrentDirectory());

            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder()
                   .SetBasePath(dirInfo.Parent!.Parent!.Parent!.FullName)
                   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                   .AddEnvironmentVariables();

            var configuration = configurationBuilder.Build();
            var config = configuration.GetSection("Project").Get<Project>()!;

            installations = new();

            for (int i = 0; i < config.InstallationsCount; i++)
            {
                installations.Add(new TechInstallation());
            }
        }

        static void SendDirectoryContent(TcpClient client, NetworkStream stream, StreamWriter writer, StreamReader reader)
        {
            try
            {
                while (client.Connected)
                {
                    string path = reader.ReadLine();

                    if (!Directory.Exists(path))
                    {
                        if (!File.Exists(path))
                            throw new Exception($"Папка не найдена: {path}");
                    }

                    StringBuilder responseSB = new StringBuilder();
                    StringBuilder logSB = new StringBuilder();

                    if (Path.GetExtension(path) == string.Empty)
                    {
                        var fileSystem = Directory.GetFileSystemEntries(path);

                        logSB.Append($"Отправлено содержимое директории {Path.GetFileName(path)}");
                        foreach (string files in fileSystem)
                        {
                            string folderName = Path.GetFileName(files);
                            responseSB.Append("FILE:" + folderName + '\n');
                        }
                    }
                    else if (Path.GetExtension(path) == ".txt")
                    {
                        using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                        using var fileReader = new StreamReader(fileStream);

                        responseSB.Append("FILE:" + fileReader.ReadLine());
                        logSB.Append($"Отправлено содержимое файла {Path.GetFileName(path)}");
                    }
                    else
                        throw new Exception("Неподдерживаемый формат файла!");

                    writer.WriteLine(responseSB.ToString());
                    writer.WriteLine("END");
                    writer.Flush();
                }
            }
            catch (IOException ex) when (ex.Message.Contains("disconnected") || ex.Message.Contains("closed"))
            {
                Console.WriteLine("Клиент отключился во время отправки данных каталога");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в SendDirectoryContent: {ex.Message}");
            }

        }

        static void SendTemperatureAndPressure(TcpClient client, NetworkStream stream, StreamWriter writer)
        {
            try
            {
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
                Console.WriteLine($"Ошибка в SendTemperatureAndPressure: {ex.Message}");
            }
        }

        static void SendInstallationsState(TcpClient client, NetworkStream stream, IList<TechInstallation> installations, StreamWriter writer)
        {
            try
            {
                while (client.Connected)
                {
                    lock (installations)
                    {
                        foreach (TechInstallation installation in installations)
                        {
                            installation.Working();
                        }
                    }

                    StringBuilder responseSB = new StringBuilder();
                    StringBuilder logSB = new StringBuilder();
                    logSB.Append("Отправлено:\n");

                    lock (installations)
                    {
                        for (int i = 0; i < installations.Count; i++)
                        {
                            responseSB.Append(i + "," + (int)installations[i].InstallationStatus + "\n");
                            logSB.Append("номер установки - " + i + ", статус - " + installations[i].InstallationStatus + "\n");
                        }
                    }

                    writer.Write(responseSB.ToString());
                    writer.Flush();

                    Console.WriteLine(logSB.ToString());

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в SendInstallationsState: {ex.Message}");
            }
        }

        static void SendRepairedInstallation(TcpClient client)
        {

        }
    }
}
