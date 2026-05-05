using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using ProcessController_Client;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TMP_Laba4_Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        int port = 8888;

        private TcpClient? client;

        private NetworkStream? stream;

        private StreamReader reader;
        private StreamWriter writer;

        private bool isConnected = false;

        public ObservableCollection<double> TemperatureValues { get; set; }
        public ObservableCollection<double> PressureValues { get; set; }

        public ISeries[] TemperatureSeries { get; set; }
        public ISeries[] PressureSeries { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            LoadPathsToComboBox();

            CreateSeries();

            DataContext = this;
        }

        private void PathFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PathFolders.SelectedItem == null) return;

            string selectedPath = PathFolders.SelectedItem.ToString();

            LoadFoldersFromPath(selectedPath);
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;

            if (string.IsNullOrEmpty(IPAddressTextBox.Text))
            {
                MessageBox.Show("Введите IP сервера!");
                return;
            }

            string ip = IPAddressTextBox.Text;
            
            try
            {
                client = new TcpClient(ip, port);
            }
            catch
            {
                MessageBox.Show("Сервер недоступен!");
                return;
            }
            TextBlockClient.Text += "Подключено к серверу!\n";

            stream = client.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream);
            isConnected = true;
        }
        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            DisconnectButton.IsEnabled = false;
            ConnectButton.IsEnabled = true;

            try
            {
                isConnected = false;

                Thread.Sleep(1000);

                stream?.Close();
                client?.Close();

                TextBlockClient.Text +=
                    "Отключено от сервера\n";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e) => this.Close();

        private async void TransmitToServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (writer == null)
                return;

            await writer.WriteLineAsync(PathFolders.Text);
            await writer.FlushAsync();
        }
        private async void LoadInfoButton_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                while (isConnected && client != null && client.Connected)
                {
                    string? response = reader.ReadLine();

                    if (response == null)
                        break;

                    if (isConnected == false)
                        break;

                    Dispatcher.BeginInvoke(() =>
                    {
                        if (isConnected == false)
                            return;

                        if (response.StartsWith("DATA:"))
                        {
                            string data = response.Replace("DATA:", "");

                            string[] parts = data.Split(',');

                            double temperature = double.Parse(parts[0]);
                            double pressure = double.Parse(parts[1]);

                            TextBlockClient.Text += $"T = {temperature}, P = {pressure}\n";

                            TemperatureValues.Add(temperature);

                            if (TemperatureValues.Count > 20)
                                TemperatureValues.RemoveAt(0);

                            PressureValues.Add(pressure);

                            if (PressureValues.Count > 20)
                                PressureValues.RemoveAt(0);
                        }
                        else if (response.StartsWith("FILE:"))
                        {
                            string fileName = response.Replace("FILE:", "");

                            TextBlockClient.Text += $"Файл: {fileName}\n";
                        }
                        else if (response == "END")
                        {
                            TextBlockClient.Text += "Передача файлов завершена\n";
                        }
                    });
                }
            });
        }

        private void LoadFoldersFromPath(string path)
        {
            FoldersList.Items.Clear();

            if (!Directory.Exists(path))
            {
                FoldersList.Items.Add($"Папка не найдена: {path}");
                return;
            }

            try
            {
                string[] folders = Directory.GetDirectories(path);

                if (folders.Length == 0)
                {
                    FoldersList.Items.Add("Нет папок в этой директории");
                }
                else
                {
                    foreach (string folder in folders)
                    {
                        string folderName = Path.GetFileName(folder);
                        FoldersList.Items.Add(folderName);
                    }
                }
            }
            catch (System.Exception ex)
            {
                FoldersList.Items.Add($"Ошибка: {ex.Message}");
            }
        }
        private void LoadPathsToComboBox()
        {
            PathFolders.Items.Add(@"C:\");
            PathFolders.Items.Add(@"C:\Users");
            PathFolders.Items.Add(@"C:\Program Files");
            PathFolders.Items.Add(@"C:\Windows");
            PathFolders.Items.Add(@"D:\");
        }
        private void CreateSeries()
        {
            TemperatureValues = new ObservableCollection<double>();
            PressureValues = new ObservableCollection<double>();

            TemperatureSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "Temperature",
                    Values = TemperatureValues
                }
            };

            PressureSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name = "Pressure",
                    Values = PressureValues
                }
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => this.DragMove();
        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e) => this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();

        private async void InstallationsWindowOpenButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            InstallationsWindow installationsWindow =
                new InstallationsWindow();

            installationsWindow.Show();

            await Task.Run(() =>
            {
                bool buttonsCreated = false;

                while (isConnected &&
                       client != null &&
                       client.Connected)
                {
                    string? response = reader.ReadLine();

                    if (response == null)
                        break;

                    Dispatcher.Invoke(() =>
                    {
                        if (response.StartsWith("COUNT:"))
                        {
                            int count = int.Parse(
                                response.Replace("COUNT:", ""));

                            for (int i = 0; i < count; i++)
                            {
                                Button button = new Button();

                                button.Content = $"Установка {i}";
                                button.Width = 120;
                                button.Height = 40;
                                button.Margin = new Thickness(5);

                                installationsWindow.ButtonsPanel
                                    .Children.Add(button);
                            }

                            buttonsCreated = true;
                        }
                        else if (response == "END")
                        {
                            return;
                        }
                        else
                        {
                            string[] parts = response.Split(',');

                            int index = int.Parse(parts[0]);
                            int status = int.Parse(parts[1]);

                            if (index <
                                installationsWindow.ButtonsPanel.Children.Count)
                            {
                                Button btn =
                                    (Button)installationsWindow
                                    .ButtonsPanel.Children[index];

                                btn.Content =
                                    $"Установка {index}\nСтатус: {status}";
                            }
                        }
                    });
                }
            });
        }
    }
}