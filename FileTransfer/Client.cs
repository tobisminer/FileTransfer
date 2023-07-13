using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileTransfer
{
    internal class Client
    {
        private readonly MainPage Page;
        public Client(MainPage page)
        {
            Page = page;
        }
        public async Task SendFile(Stream stream, string fileName, string IPadress, int Port)
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
                var sizeSent = 0;
                for (var i = 0; i < bufferCount; i++)
                {
                    var buffer = new byte[bufferSize];
                    var size = await stream.ReadAsync(buffer.AsMemory(0, bufferSize));
                    sizeSent += size;
                    var progress = ((double)sizeSent / (double)stream.Length);
                    Utils.UpdateProgress(Page, progress);
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

    }
}
