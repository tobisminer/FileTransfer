using CommunityToolkit.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.IO.MemoryMappedFiles;

namespace FileTransfer;

public partial class MainPage : ContentPage
{
    public ObservableCollection<Utils.Files> FilesList { get; } = new();
    public ObservableCollection<Utils.Log> Logs { get; } = new();

    private List<FileResult> _selectedFiles = new();

    public MainPage()
    {
        InitializeComponent();
        LoadDefault();
    }
    private void LoadDefault()
    {
        IpAddress.Text = Preferences.Default.Get("IPAddress", "");
        SetTheme(true);
    }
    private void ThemeBtn_OnClicked(object sender, EventArgs e)
    {
        SetTheme();
    }
    private void SetTheme(bool startUp = false)
    {
        var isDarkMode = Preferences.Default.Get("DarkMode", true);
        if (!startUp)
        {
            isDarkMode = !isDarkMode;
        }
        if (isDarkMode)
        {
            ThemeBtn.Source = "moon.png";
            Preferences.Default.Set("DarkMode", true);
            Application.Current.UserAppTheme = AppTheme.Light;
        }
        else
        {
            ThemeBtn.Source = "sun.png";
            Preferences.Default.Set("DarkMode", false);
            Application.Current.UserAppTheme = AppTheme.Dark;
        }
    }
    #region Client

    private async void SelectFilesBtn_Click(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickMultipleAsync();
            var fileResults = result.ToList();
            _selectedFiles = fileResults.ToList();
            FilesList.Clear();
            foreach (var file in _selectedFiles)
            {
                var stream = await file.OpenReadAsync();
                FilesList.Add(new Utils.Files { FileName = file.FileName, FileSize = Utils.SizeSuffix(stream.Length) });
                await stream.DisposeAsync();
            }

            FileListView.ItemsSource = FilesList;
        }
        catch (Exception exception)
        {
            Utils.HandleException(exception);
        }
    }

    private async void SendBtn_Click(object sender, EventArgs e)
    {
        var ip = IpAddress.Text;
        const int port = 23000;

        if (!Utils.ValidateIPv4(ip))
        {
            Utils.MakeToast("IP address is invalid");
            return;
        }
        Preferences.Default.Set("IPAddress", ip);
        foreach (var file in _selectedFiles.Where(file => file != null))
        {
            var stream = await file.OpenReadAsync();
            await SendFile(stream, file.FileName, ip, port);
        }
    }
  
    private async Task SendFile(Stream stream, string fileName, string IPadress, int Port)
    {
        try
        {
            const int bufferSize = 1024;

            var bufferCount = Convert.ToInt32(Math.Ceiling(stream.Length / (double)bufferSize));

            var tcpClient = new TcpClient(IPadress, Port)
            {
                SendTimeout = 60000,
                ReceiveTimeout = 60000
            };
            var client = tcpClient.Client;
            var headerStr = "Content-length:" + stream.Length + "\r\nFilename:" + fileName + "\r\n";
            var header = new byte[bufferSize];
            Array.Copy(Encoding.UTF8.GetBytes(headerStr), header, Encoding.UTF8.GetBytes(headerStr).Length);

            await client.SendAsync(header);
            for (var i = 0; i < bufferCount; i++)
            {
                var buffer = new byte[bufferSize];
                var size = await stream.ReadAsync(buffer.AsMemory(0, bufferSize));

                await client.SendAsync(buffer);
            }

            client.Close();
            stream.Close();

            Utils.MakeToast("File successfully send!");
        }
        catch (Exception e)
        {
            Utils.HandleException(e);
        }
    }

    #endregion

    #region Server
    private TcpListener _listener;
    private string defaultDirectory;

    private bool _isServerRunning;
    private void ServerCreateBtn_Click(object sender, EventArgs e)
    {
        MainLayout.Children.ToList().ForEach(x =>
        {
            var test = (View)x;
            test.IsVisible = test.ClassId switch
            {
                "Client" => !test.IsVisible,
                "Server" => !test.IsVisible,
                _ => test.IsVisible
            };

        });
        InvalidateMeasure();
        if (Header.Text == "File Client")
        {
            Header.Text = "File Server";
            SwitchBtn.Text = "Switch to Client";
            CreateServer();
        }

        else
        {
            Header.Text = "File Client";
            SwitchBtn.Text = "Switch to Server";
            StopServer();
        }
    }
    public async void CreateServer()
    {
        var ipAddress = Utils.GetIPAdress();
        
        ServerIpAddress.Text = ipAddress;
        _isServerRunning = true;
        _listener = new TcpListener(IPAddress.Parse(ipAddress), 23000);
        _listener.Start();
        var watch = new Stopwatch();
        while (_isServerRunning)
            try
            {
                var socket = await _listener.AcceptSocketAsync();
                watch.Restart();
                CreateNewLog($"Client connected! With IP {socket.RemoteEndPoint}");

                const int bufferSize = 1024;
                var header = new byte[bufferSize];
                socket.Receive(header);
                var headerStr = Encoding.UTF8.GetString(header);
                var split = headerStr.Split(new[] { "\r\n" }, StringSplitOptions.None);
                var headers = split.Where(
                    s => s.Contains(':')).ToDictionary(
                    s => s[..s.IndexOf(":", StringComparison.Ordinal)],
                    s => s[(s.IndexOf(":", StringComparison.Ordinal) + 1)..]);

                var fileSize = Convert.ToInt32(headers["Content-length"]);
                var filename = headers["Filename"];
                CreateNewLog($"File name: {filename}");
                CreateNewLog($"File size: {Utils.SizeSuffix(fileSize, 3)}");
                var memoryStream = new MemoryStream(); //TODO: Fix big memory allocation*/
                while (fileSize > 0)
                {
                    var buffer = new byte[bufferSize];
                    var size = await socket.ReceiveAsync(buffer, SocketFlags.None);
                    await memoryStream.WriteAsync(buffer.AsMemory(0, size));
                    fileSize -= size;
                }
                string resultPath;
                if (defaultDirectory is null or "")
                {
                    var result = await FileSaver.SaveAsync(filename, memoryStream, Utils.CancellationToken);
                    resultPath = result.FilePath;
                }
                else
                {
                    var targetFile = Path.Combine(defaultDirectory, filename);
                    await using var fileStream = new FileStream(targetFile, FileMode.Create);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await memoryStream.CopyToAsync(fileStream);

                    resultPath = targetFile;
                }
                
                await memoryStream.DisposeAsync();
                memoryStream.Close();
                socket.Close();
                socket.Dispose();
                watch.Stop();
                CreateNewLog($"File transferred in {watch.ElapsedMilliseconds} ms");
                CreateNewLog($"File saved to {resultPath}");
                CreateNewLog("---------File transfer done!---------");
            }
            catch (Exception e)
            {
                _listener.Stop();
                Utils.HandleException(e);
            }
    }
    private async void DirectoryBtn_OnClicked(object sender, EventArgs e)
    {
        var folder = await FolderPicker.Default.PickAsync(Utils.CancellationToken);
        if (folder.Folder == null) return;
        var path = folder.Folder.Path;
        DefaultDirectory.Text = path; 
        defaultDirectory = path;
    }

    public void StopServer()
    {
        _listener.Stop();
        _listener.Server.Close();
        _isServerRunning = false;
    }

    #endregion

    public void CreateNewLog(string message)
    {
        Logs.Add(new Utils.Log { Message = message });
        ServerLogView.ItemsSource = Logs;
    }


   
}