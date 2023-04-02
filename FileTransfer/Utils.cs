using CommunityToolkit.Maui.Alerts;

namespace FileClient
{
    internal class Utils
    {
        public static CancellationTokenSource cancellationTokenSource = new();

        private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB" };
        public static string SizeSuffix(long value, int decimalPlaces = 0)
        {
            if (value < 0)
            {
                throw new ArgumentException("Bytes should not be negative", "value");
            }
            var mag = (int)Math.Max(0, Math.Log(value, 1024));
            var adjustedSize = Math.Round(value / Math.Pow(1024, mag), decimalPlaces);
            return $"{adjustedSize} {SizeSuffixes[mag]}";
        }

        public static async void MakeToast(string message)
        {
            var toast = Toast.Make(message);
            await toast.Show(cancellationTokenSource.Token);
        }


    }
}
