using System.Collections.Generic;
using System.Threading.Tasks;

namespace HmaxUnlocker.Engine.Communication
{
    public interface IAdbShell
    {
        Task<string> ExecuteAsync(string command);
        Task<string> ExecuteRootAsync(string command);
        Task<byte[]> ReadFileAsync(string remotePath);
        Task<bool> WriteFileAsync(string remotePath, byte[] data);
        Task<bool> WriteFileAsync(string remotePath, string textContent);
        Task<bool> PullFileAsync(string remotePath, string localPath);
        Task<bool> PushFileAsync(string localPath, string remotePath);
        Task<long> GetFileSizeAsync(string remotePath);
        Task<bool> FileExistsAsync(string remotePath);
        Task<bool> DirectoryExistsAsync(string remotePath);
        Task<List<string>> ListDirectoryAsync(string remotePath);
        Task<bool> IsDeviceConnectedAsync();
        Task<bool> HasRootAccessAsync();
        Task<string> RebootAsync(string mode = null);
    }
}
