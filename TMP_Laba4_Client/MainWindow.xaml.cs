using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace TMP_Laba4_Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            LoadPathsToComboBox();
        }

        private void PathFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PathFolders.SelectedItem == null) return;

            string selectedPath = PathFolders.SelectedItem.ToString();

            LoadFoldersFromPath(selectedPath);
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
            catch (UnauthorizedAccessException)
            {
                FoldersList.Items.Add("Нет доступа к этой папке");
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => this.DragMove();

        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e) => this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}