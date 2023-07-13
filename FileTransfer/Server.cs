using CommunityToolkit.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FileTransfer
{
    internal class Server
    {
        public TcpListener _listener;
        public string defaultDirectory;
        public bool _isServerRunning;
        private readonly MainPage Page;
        public Server(MainPage page)
        {
            this.Page = page;
        }
        
        public async void CreateServer()
        {
            var ipAddress = Utils.GetIPAdress();

            Page.ServerIpAddress.Text = ipAddress;
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
                    Page.ProgressFile.Progress = 0;
                    const int bufferSize = 1024;
                    var header = new byte[bufferSize];
                    await socket.ReceiveAsync(header);
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

                    var lastProgress = 0D;
                    var fileSizeCopy = fileSize;

                    while (fileSizeCopy > 0)
                    {
                        var buffer = new byte[bufferSize];
                        var size = await socket.ReceiveAsync(buffer, SocketFlags.None);
                        await memoryStream.WriteAsync(buffer.AsMemory(0, size));
                        fileSizeCopy -= size;
                        //calculate progress
                        var progress = (memoryStream.Length + 0.0) / fileSize;
                        Utils.UpdateProgress(Page, progress);
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
                        resultPath = targetFile;
                        await using var fileStream = new FileStream(targetFile, FileMode.Create);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        await memoryStream.CopyToAsync(fileStream);
                    }

                    await memoryStream.DisposeAsync();
                    memoryStream.Close();
                    socket.Dispose();
                    watch.Stop();
                    Page.ProgressFile.Progress = 0;
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

        public void StopServer()
        {
            _listener.Stop();
            _listener.Server.Close();
            _isServerRunning = false;
            Page.ProgressFile.Progress = 0;
        }
        public void CreateNewLog(string message)
        {
            Page.Logs.Add(new Utils.Log { Message = message });
            Page.ServerLogView.ItemsSource = Page.Logs;
        }
    }
}
