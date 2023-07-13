using CommunityToolkit.Maui.Storage;
using System.Collections.ObjectModel;

namespace FileTransfer;

public partial class MainPage : ContentPage
{
    public ObservableCollection<Utils.Files> FilesList { get; } = new();
    public ObservableCollection<Utils.Log> Logs { get; } = new();

    private List<FileResult> _selectedFiles = new();

    private Server server;
    private Client client;

    public MainPage()
    {
        InitializeComponent();
        LoadDefault();
    }
    private void LoadDefault()
    {
        IpAddress.Text = Preferences.Default.Get("IPAddress", "");
        SetTheme(true);

        server = new Server(this);
        client = new Client(this);
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
            _selectedFiles = result.ToList();
            foreach (var file in _selectedFiles.Where(file => file == null))
            {
                _selectedFiles.Remove(file);
            }
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

        foreach (var file in _selectedFiles)
        {
            var stream = await file.OpenReadAsync();
            await client.SendFile(stream, file.FileName, ip, port);
        }
    }


    #endregion

    #region Server



    private void SwitchBtn_Click(object sender, EventArgs e)
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
        if (Header.Text == "File Sender")
        {
            Header.Text = "File Receiver";
            SwitchBtn.Text = "Switch to Sender";
            server.CreateServer();
        }

        else
        {
            Header.Text = "File Sender";
            SwitchBtn.Text = "Switch to Receiver";
            server.StopServer();
        }
    }

    private async void DirectoryBtn_OnClicked(object sender, EventArgs e)
    {
        var folder = await FolderPicker.Default.PickAsync(Utils.CancellationToken);
        if (folder.Folder == null) return;
        var path = folder.Folder.Path;
        DefaultDirectory.Text = path;
        server.defaultDirectory = path;
    }



    #endregion





}