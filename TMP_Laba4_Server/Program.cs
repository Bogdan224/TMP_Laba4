using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using ProcessController_Server;
using System.Collections.Concurrent;
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

        private static ConcurrentQueue<int> repairQueue = new ConcurrentQueue<int>();
        private static SemaphoreSlim repairSemaphore = new SemaphoreSlim(Environment.ProcessorCount);
        private static CancellationTokenSource cts = new CancellationTokenSource();

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

                            StartRepairWorkers(installations);

                            server.Action = (client, stream) =>
                            {
                                using StreamWriter writer = new StreamWriter(stream);
                                using StreamReader reader = new StreamReader(stream);

                                Task.Run(() => SendInstallationsState(client, stream, installations, writer));
                                Task.Run(() => HandleRepairRequests(client, stream, installations, writer, reader));

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
                    string[] drives = Directory.GetLogicalDrives();

                    foreach (string drive in drives)
                    {
                        writer.WriteLine($"DRIVE:{drive}");
                    }

                    string? path = reader.ReadLine();

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
                            responseSB.Append("FILE:" + folderName + ',');
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
                writer.WriteLine($"COUNT:{installations.Count}");
                writer.Flush();

                while (client.Connected)
                {

                    foreach (TechInstallation installation in installations)
                    {
                        if(installation.InstallationStatus != TechInstallation.Status.Repair)
                            installation.Working();
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

                    Thread.Sleep(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в SendInstallationsState: {ex.Message}");
            }
        }

        static void StartRepairWorkers(IList<TechInstallation> installations)
        {
            int workerCount = Environment.ProcessorCount * 2; // Количество параллельных обработчиков
            for (int i = 0; i < workerCount; i++)
            {
                Task.Run(() => ProcessRepairQueue(installations, cts.Token));
            }
            Console.WriteLine($"Запущено {workerCount} обработчиков ремонта");
        }

        // Воркер, обрабатывающий очередь запросов на починку
        static async Task ProcessRepairQueue(IList<TechInstallation> installations, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (repairQueue.TryDequeue(out int index))
                {
                    await RepairInstallationAsync(installations, index);
                }
                else
                {
                    await Task.Delay(100, token); // Ожидание новых запросов
                }
            }
        }

        // Асинхронный ремонт установки (не блокирует другие операции)
        static async Task RepairInstallationAsync(IList<TechInstallation> installations, int index)
        {
            await repairSemaphore.WaitAsync();
            try
            {
                lock (installations)
                {
                    if (installations[index].InstallationStatus != TechInstallation.Status.Crash)
                    {
                        Console.WriteLine($"Установка {index} не сломана, ремонт не требуется");
                        return;
                    }

                    Console.WriteLine($"Начало ремонта установки {index}");
                    installations[index].InstallationStatus = TechInstallation.Status.Repair;
                }

                await WaitForRepairCompletion(installations, index);

                lock (installations)
                {
                    if (installations[index].InstallationStatus == TechInstallation.Status.Repair)
                    {
                        installations[index].InstallationStatus = TechInstallation.Status.Success;
                        Console.WriteLine($"Ремонт установки {index} завершен успешно");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при ремонте установки {index}: {ex.Message}");
                lock (installations)
                {
                    installations[index].InstallationStatus = TechInstallation.Status.Crash;
                }
            }
            finally
            {
                repairSemaphore.Release();
            }
        }

        static async Task WaitForRepairCompletion(IList<TechInstallation> installations, int index)
        {
            while (true)
            {
                await Task.Delay(2000);

                lock (installations)
                {
                    if (installations[index].InstallationStatus != TechInstallation.Status.Repair)
                    {
                        return;
                    }

                    var workingMethod = typeof(TechInstallation).GetMethod("Working");
                    if (workingMethod != null)
                    {
                        workingMethod.Invoke(installations[index], null);
                    }
                }
            }
        }

        static void HandleRepairRequests(TcpClient client, NetworkStream stream, IList<TechInstallation> installations, StreamWriter writer, StreamReader reader)
        {
            try
            {
                while (client.Connected)
                {
                    string? request = reader.ReadLine();

                    if (request == null) break;

                    if (int.TryParse(request, out int index))
                    {
                        lock (installations)
                        {
                            if (index < 0 || index >= installations.Count)
                            {
                                writer.WriteLine($"Ошибка: неверный индекс {index}");
                                writer.Flush();
                                continue;
                            }

                            if (installations[index].InstallationStatus != TechInstallation.Status.Crash)
                            {
                                writer.WriteLine($"Ошибка: установка {index} не сломана (текущий статус: {installations[index].InstallationStatus})");
                                writer.Flush();
                                continue;
                            }
                        }

                        // Добавляем запрос в очередь для параллельной обработки
                        repairQueue.Enqueue(index);

                        Console.WriteLine($"Установка {index} добавлена в очередь на ремонт");
                    }
                    else
                    {
                        writer.WriteLine("Ошибка: неверный формат");
                        writer.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HandleRepairRequests: {ex.Message}");
            }
        }
    }
}
