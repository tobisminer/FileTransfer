using CommunityToolkit.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FileTransfer;

public partial class MainPage : ContentPage
{
    public class Files
    {
        public string FileName { get; set; }
        public string FileSize { get; set; }
    }

    public class Log
    {
        public string Message { get; set; }
    }

    public ObservableCollection<Files> FilesList { get; } = new();
    public ObservableCollection<Log> Logs { get; } = new();

    private List<FileResult> _selectedFiles = new();

    public MainPage()
    {
        InitializeComponent();

        IpAddress.Text = Preferences.Default.Get("IPAddress", "");
        Port.Text = Preferences.Default.Get("Port", 0).ToString();
    }

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
                FilesList.Add(new Files { FileName = file.FileName, FileSize = Utils.SizeSuffix(stream.Length) });
                stream.Close();
                await stream.DisposeAsync();
            }

            FileListView.ItemsSource = FilesList;
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }

    private async void SendBtn_Click(object sender, EventArgs e)
    {
        var ip = IpAddress.Text;

        if (!Utils.ValidateIPv4(ip))
        {
            Utils.MakeToast("IP address is invalid");
            return;
        }

        if (!int.TryParse(Port.Text, out var port) || port is < 0 or > 65535)
        {
            Utils.MakeToast("Port is invalid number");
            return;
        }

        Preferences.Default.Set("IPAddress", ip);
        Preferences.Default.Set("Port", port);
        foreach (var file in _selectedFiles.Where(file => file != null))
        {
            var stream = await file.OpenReadAsync();
            SendFile(stream, file.FileName, ip, port);
        }
    }

    private async void SendFile(Stream stream, string fileName, string IPadress, int Port)
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
            HandleException(e);
        }
    }

    public void HandleException(Exception e)
    {
        Utils.MakeToast(e.Message);
    }
    
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

    private TcpListener _listener;
    private bool _isServerRunning;
    public async void CreateServer()
    {
        string ipAddress = null;
#if WINDOWS
        ipAddress = Utils.GetLocalIpAddress().ToString();
#endif
#if ANDROID
        ipAddress = Utils.GetLocalIpAddressForAndroid();
#endif

        ServerIpAddress.Text = ipAddress;
        ServerPort.Text = "23000";
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
                var bufferCount = Convert.ToInt32(Math.Ceiling(fileSize / (double)bufferSize));
                var filename = headers["Filename"];
                CreateNewLog($"File name: {filename}");
                CreateNewLog($"File size: {Utils.SizeSuffix(fileSize, 3)} bytes");

                var fs = new MemoryStream();
                while (fileSize > 0)
                {
                    var buffer = new byte[bufferSize];
                    var size = await socket.ReceiveAsync(buffer, SocketFlags.None);
                    await fs.WriteAsync(buffer, 0, size);
                    fileSize -= size;
                }


                var result = await FileSaver.SaveAsync(filename, fs, Utils.CancellationToken);
                fs.Close();
                socket.Close();
                watch.Stop();
                CreateNewLog($"File transferred in {watch.ElapsedMilliseconds} ms");
                CreateNewLog($"File saved to {result.FilePath}");
                CreateNewLog("---------File transfer done!---------");
            }
            catch (Exception e)
            {
                _listener.Stop();
                HandleException(e);
            }
    }

    public void StopServer()
    {
        _listener.Stop();
        _listener.Server.Close();
        _isServerRunning = false;
    }
    public void CreateNewLog(string message)
    {
        Logs.Add(new Log { Message = message });
        ServerLogView.ItemsSource = Logs;
    }
}