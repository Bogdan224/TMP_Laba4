using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using OpenTK.Input;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
        private bool canEnable = true;

        public bool IsConnected
        {
            get => isConnected;

            set
            {
                isConnected = value;
                Notify?.Invoke(isConnected);
            }
        }

        public ObservableCollection<double> TemperatureValues { get; set; }
        public ObservableCollection<double> PressureValues { get; set; }

        public delegate void AccountHandler(bool isConnected);
        public event AccountHandler? Notify;

        public ISeries[] TemperatureSeries { get; set; }
        public ISeries[] PressureSeries { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            CreateSeries();

            DataContext = this;
            Notify += OnConnected;

            serverButton.IsEnabled = false;
            clientButton.IsEnabled = false;
        }

        public void OnConnected(bool isConnected)
        {
            if (isConnected)
            {
                serverButton.IsEnabled = true;
                clientButton.IsEnabled = true;
            }
            else
            {
                serverButton.IsEnabled = false;
                clientButton.IsEnabled = false;
            }
        }

        private void PathFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PathFolders.SelectedItem == null)
                return;

            string? selectedPath = PathFolders.SelectedItem.ToString();

            if (selectedPath == null)
                return;
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
                ConnectButton.IsEnabled = true;
                DisconnectButton.IsEnabled = false;
                return;
            }
            TextBlockClient.Text += "Подключено к серверу!\n";

            stream = client.GetStream();
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream);
            IsConnected = true;
        }
        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            DisconnectButton.IsEnabled = false;
            ConnectButton.IsEnabled = true;

            try
            {
                IsConnected = false;

                stream?.Close();
                client?.Close();

                Thread.Sleep(1000);

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
            if (writer == null || isConnected == false)
                return;

            await writer.WriteLineAsync(PathFolders.Text);
            await writer.FlushAsync();
        }

        private bool _isLoading = false;

        private async void LoadInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
                return;

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        while (client != null && client.Connected)
                        {
                            string? response = reader.ReadLine();

                            if (response == null)
                                break;

                            if (response.StartsWith("DRIVE:"))
                            {
                                string drive = response.Replace("DRIVE:", "");
                                Dispatcher.Invoke(() => PathFolders.Items.Add(drive));
                            }
                            else if (response.StartsWith("DATA:"))
                            {
                                string data = response.Replace("DATA:", "");
                                string[] parts = data.Split(',');
                                double temperature = double.Parse(parts[0]);
                                double pressure = double.Parse(parts[1]);

                                Dispatcher.Invoke(() =>
                                {
                                    TextBlockClient.Text += $"T = {temperature}, P = {pressure}\n";
                                    TemperatureValues.Add(temperature);
                                    PressureValues.Add(pressure);

                                    if (TemperatureValues.Count > 20) TemperatureValues.RemoveAt(0);
                                    if (PressureValues.Count > 20) PressureValues.RemoveAt(0);
                                });
                            }
                            else if (response.StartsWith("FILE:"))
                            {
                                string fileName = response.Replace("FILE:", "");
                                fileName = fileName.Replace(',', '\n');

                                Dispatcher.Invoke(() =>
                                {
                                    FileTextBlock.Text = "";
                                    TextBlockClient.Text += $"Файл: {fileName}\n";
                                    FileTextBlock.Text += fileName;
                                });
                            }
                            else if (response == "END")
                            {
                                Dispatcher.Invoke(() => TextBlockClient.Text += "Передача файлов завершена\n");
                            }
                        }
                    }
                    catch (IOException)
                    {
                        Dispatcher.Invoke(() => TextBlockClient.Text += "Соединение закрыто\n");
                    }
                    catch (ObjectDisposedException) { }
                });
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async void InstallationButton_Click(object sender, RoutedEventArgs e)
        {
            InstallationButton.IsEnabled = false;
            GraphsButton.IsEnabled = true;

            GraphsStackPanel.Visibility = Visibility.Collapsed;
            ButtonsPanel.Visibility = Visibility.Visible;

            Dispatcher.Invoke(() =>
            {
                ButtonsPanel.Children.Clear();
            });

            bool buttonsCreated = false;

            await Task.Run(() =>
            {
                try
                {
                    while (isConnected && client != null && client.Connected)
                    {
                        if (isConnected == false)
                            break;

                        string? response = reader.ReadLine();

                        if (response == null)
                            break;

                        Dispatcher.Invoke(() =>
                        {
                            if (isConnected == false)
                                return;

                            if (response.StartsWith("COUNT:"))
                            {
                                if (buttonsCreated)
                                    return;

                                int count = int.Parse(response.Replace("COUNT:", ""));


                                for (int i = 0; i < count; i++)
                                {
                                    Button button = new Button();

                                    button.Width = 150;
                                    button.Height = 70;
                                    button.Margin = new Thickness(5);

                                    button.Click += Button_Click;

                                    ButtonsPanel.Children.Add(button);
                                }

                                buttonsCreated = true;
                            }
                            else if (response.StartsWith("REPAIRED:"))
                            {
                                string status = response.Replace("REPAIRED:", "");

                                if (!int.TryParse(status, out int index))
                                    throw new Exception("Не удалось обработать ответ сервера!");

                                Button button = (Button)ButtonsPanel.Children[index];

                                string statusText = "Работает";
                                button.Focusable = false;
                                button.IsHitTestVisible = false;
                                button.Background = Brushes.Green;

                                button.Content = $"Установка {index}\n{statusText}";
                            }
                            else
                            {
                                string[] parts = response.Split(',');

                                int index = int.Parse(parts[0]);
                                int status = int.Parse(parts[1]);

                                if (index >= ButtonsPanel.Children.Count)
                                    return;

                                Button button = (Button)ButtonsPanel.Children[index];

                                button.Tag = index;

                                string statusText = "";

                                switch (status)
                                {
                                    case 0:
                                        statusText = "Работает";
                                        button.Focusable = false;
                                        button.IsHitTestVisible = false;
                                        button.Background = Brushes.Green;
                                        break;

                                    case 1:
                                        statusText = "Авария";
                                        button.Background = Brushes.Red;
                                        button.Focusable = true;
                                        button.IsHitTestVisible = true;
                                        break;

                                    case 2:
                                        statusText = "Ремонт";
                                        button.Background = Brushes.Gray;
                                        button.Focusable = false;
                                        button.IsHitTestVisible = false;
                                        break;
                                }

                                button.Content =
                                    $"Установка {index}\n{statusText}";
                            }
                        });
                    }
                }
                catch (IOException)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TextBlockClient.Text += "Соединение закрыто\n";
                    });
                }
                catch (ObjectDisposedException)
                {
                }
            });
        }

        private void GraphsButton_Click(object sender, RoutedEventArgs e)
        {
            GraphsStackPanel.Visibility = Visibility.Visible;
            ButtonsPanel.Visibility = Visibility.Collapsed;

            GraphsButton.IsEnabled = false;
            InstallationButton.IsEnabled = true;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            int index = (int)button.Tag;

            string statusText = "Ремонт";
            button.Background = Brushes.Gray;
            button.Focusable = false;
            button.IsHitTestVisible = false;
            button.Content = $"Установка {index}\n{statusText}";

            TextBlockClient.Text += $"Установка {index} на {statusText}е\n";

            await writer.WriteLineAsync(index.ToString());
            await writer.FlushAsync();
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
    }
}