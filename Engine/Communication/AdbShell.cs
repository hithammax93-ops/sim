using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HmaxUnlocker.Engine.Communication
{
    /// <summary>
    /// تنفيذ حقيقي لقناة ADB:
    ///  - القراءة/الكتابة عبر adb pull/push (تعمل مع الملفات الكبيرة وملفات root)
    ///  - كل أمر root يُغلَّف بـ su 0 sh -c حتى تُنفَّذ التحويلات (>, |, &&) بصلاحيات root فعلياً
    ///  - عند الكتابة يُعاد تطبيق المالك/الصلاحيات الأصلية + restorecon حتى لا يرفض RIL الملف
    /// </summary>
    public class AdbShell : IAdbShell
    {
        private const int TransferTimeoutMs = 120000; // نقل الملفات الكبيرة (QCNv وغيره)

        private readonly string _adbPath;
        private readonly string _serial;
        private readonly int _timeoutMs;

        public AdbShell(string adbPath = null, string serial = null, int timeoutMs = 30000)
        {
            if (timeoutMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be greater than zero.");

            _adbPath = ResolveAdbPath(adbPath);
            _serial = string.IsNullOrWhiteSpace(serial) ? null : serial.Trim();
            _timeoutMs = timeoutMs;
        }

        // ابحث عن adb.exe بجانب البرنامج أولاً (bin/Debug) ثم في PATH
        private static string ResolveAdbPath(string adbPath)
        {
            if (!string.IsNullOrEmpty(adbPath))
                return adbPath;

            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "adb.exe");
            if (File.Exists(local))
                return local;

            return "adb";
        }

        private string BuildArgs(string args) =>
            string.IsNullOrEmpty(_serial) ? args : $"-s {_serial} {args}";

        // اقتباس مسار للـ shell على الجهاز مع معالجة علامات الاقتباس المفردة.
        private static string QuoteRemote(string remotePath)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
                throw new ArgumentException("Remote path cannot be empty.", nameof(remotePath));
            return "'" + remotePath.Replace("'", "'\\''") + "'";
        }

        public async Task<string> ExecuteAsync(string command)
        {
            var r = await RunProcessExAsync(_adbPath, BuildArgs($"shell \"{command}\""), _timeoutMs);
            return r.Output;
        }

        public async Task<string> ExecuteRootAsync(string command)
        {
            // sh -c داخل su حتى تُنفَّذ التحويلات والأنابيب بصلاحيات root لا بصلاحيات shell
            string escaped = command.Replace("'", "'\\''");
            var r = await RunProcessExAsync(_adbPath,
                BuildArgs($"shell \"su 0 sh -c '{escaped}'\""), _timeoutMs);
            return r.Output;
        }

        public async Task<byte[]> ReadFileAsync(string remotePath)
        {
            try
            {
                string staged = $"/data/local/tmp/.hmax_r_{Guid.NewGuid():N}";
                string localTemp = Path.Combine(Path.GetTempPath(), $"hmax_{Guid.NewGuid():N}.bin");

                try
                {
                    // انسخ بصلاحيات root ثم اجعله مقروءاً لـ adb ثم اسحبه فعلياً
                    await ExecuteRootAsync(
                        $"cat {QuoteRemote(remotePath)} > {staged} && chmod 644 {staged}");

                    var pull = await RunProcessExAsync(_adbPath,
                        BuildArgs($"pull {staged} \"{localTemp}\""), TransferTimeoutMs);

                    if (pull.ExitCode == 0 && File.Exists(localTemp))
                        return File.ReadAllBytes(localTemp);

                    return Array.Empty<byte>();
                }
                finally
                {
                    try { if (File.Exists(localTemp)) File.Delete(localTemp); } catch { }
                    try { await ExecuteRootAsync($"rm -f {staged}"); } catch { }
                }
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        public async Task<bool> WriteFileAsync(string remotePath, byte[] data)
        {
            if (data == null)
                return false;

            try
            {
                string localTemp = Path.Combine(Path.GetTempPath(), $"hmax_{Guid.NewGuid():N}.bin");
                File.WriteAllBytes(localTemp, data);
                try
                {
                    string staged = $"/data/local/tmp/.hmax_w_{Guid.NewGuid():N}";
                    var push = await RunProcessExAsync(_adbPath,
                        BuildArgs($"push \"{localTemp}\" {staged}"), TransferTimeoutMs);
                    if (push.ExitCode != 0)
                        return false;

                    if (!await CopyStagedToRemoteAsync(staged, remotePath))
                        return false;

                    // تحقق فعلي: الحجم على الجهاز يجب أن يطابق ما كتبناه
                    long remoteSize = await GetFileSizeAsync(remotePath);
                    return remoteSize == data.Length;
                }
                finally
                {
                    try { File.Delete(localTemp); } catch { }
                }
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> WriteFileAsync(string remotePath, string textContent)
        {
            return await WriteFileAsync(remotePath,
                textContent == null ? Array.Empty<byte>() : System.Text.Encoding.UTF8.GetBytes(textContent));
        }

        public async Task<bool> PullFileAsync(string remotePath, string localPath)
        {
            string staged = $"/data/local/tmp/.hmax_p_{Guid.NewGuid():N}";
            try
            {
                if (string.IsNullOrWhiteSpace(localPath))
                    return false;

                string localDirectory = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(localDirectory))
                    Directory.CreateDirectory(localDirectory);

                await ExecuteRootAsync(
                    $"cat {QuoteRemote(remotePath)} > {QuoteRemote(staged)} && chmod 644 {QuoteRemote(staged)}");

                var pull = await RunProcessExAsync(_adbPath,
                    BuildArgs($"pull {staged} \"{localPath}\""), TransferTimeoutMs);

                return pull.ExitCode == 0 && File.Exists(localPath);
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { await ExecuteRootAsync($"rm -f {QuoteRemote(staged)}"); } catch { }
            }
        }

        public async Task<bool> PushFileAsync(string localPath, string remotePath)
        {
            try
            {
                if (!File.Exists(localPath))
                    return false;

                string staged = $"/data/local/tmp/.hmax_s_{Guid.NewGuid():N}";
                var push = await RunProcessExAsync(_adbPath,
                    BuildArgs($"push \"{localPath}\" {staged}"), TransferTimeoutMs);
                if (push.ExitCode != 0)
                    return false;

                return await CopyStagedToRemoteAsync(staged, remotePath);
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        // ينسخ الملف المؤقت إلى وجهته النهائية بصلاحيات root مع إعادة تطبيق
        // المالك/الصلاحيات الأصلية وسياق SELinux حتى يبقى الملف صالحاً للنظام
        private async Task<bool> CopyStagedToRemoteAsync(string staged, string remotePath)
        {
            try
            {
                string quoted = QuoteRemote(remotePath);
                string quotedStaged = QuoteRemote(staged);

                // احتفظ بالمالك/الصلاحيات الحالية إن كان الملف موجوداً
                string owner = "radio:radio";
                string mode = "660";
                string stat = (await ExecuteRootAsync(
                    $"stat -c '%u:%g:%a' {quoted}")).Trim();
                var m = Regex.Match(stat, @"^\s*(\d+):(\d+):(\d+)\s*$");
                if (m.Success)
                {
                    owner = m.Groups[1].Value + ":" + m.Groups[2].Value;
                    mode = m.Groups[3].Value;
                }

                // لا نعتمد على غياب رسائل الخطأ: النجاح يتطلب علامة لا تظهر إلا بعد اكتمال كل الخطوات.
                // restorecon اختياري لأن بعض الأجهزة لا توفره، أما الكتابة والمالك والصلاحيات فإلزامية.
                string result = await ExecuteRootAsync(
                    $"cat {quotedStaged} > {quoted} && chown {owner} {quoted} && chmod {mode} {quoted} && " +
                    $"(restorecon {quoted} 2>/dev/null || true) && echo HMAX_WRITE_OK");

                return result.Contains("HMAX_WRITE_OK");
            }
            catch
            {
                return false;
            }
            finally
            {
                try { await ExecuteRootAsync($"rm -f {QuoteRemote(staged)}"); } catch { }
            }
        }

        public async Task<long> GetFileSizeAsync(string remotePath)
        {
            try
            {
                string result = await ExecuteRootAsync($"stat -c %s {QuoteRemote(remotePath)} 2>/dev/null");
                string firstLine = result.Split('\n')
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.Length > 0);
                if (firstLine != null && Regex.IsMatch(firstLine, @"^\d+$"))
                    return long.Parse(firstLine);
                return -1;
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch
            {
                return -1;
            }
        }

        public async Task<bool> FileExistsAsync(string remotePath)
        {
            try
            {
                string result = await ExecuteRootAsync($"test -f {QuoteRemote(remotePath)} && echo EXISTS");
                return result.Contains("EXISTS");
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DirectoryExistsAsync(string remotePath)
        {
            try
            {
                string result = await ExecuteRootAsync($"test -d {QuoteRemote(remotePath)} && echo EXISTS");
                return result.Contains("EXISTS");
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<string>> ListDirectoryAsync(string remotePath)
        {
            try
            {
                string result = await ExecuteRootAsync($"ls {QuoteRemote(remotePath)} 2>/dev/null");
                return result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task<bool> IsDeviceConnectedAsync()
        {
            try
            {
                string result = await RunProcessAsync(_adbPath, "devices");
                foreach (string rawLine in result.Split('\n'))
                {
                    string line = rawLine.Trim();
                    if (!line.EndsWith("\tdevice"))
                        continue;

                    if (string.IsNullOrEmpty(_serial))
                        return true;

                    if (line.StartsWith(_serial + "\t"))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> HasRootAccessAsync()
        {
            try
            {
                string r1 = await RunProcessAsync(_adbPath, BuildArgs("shell \"su 0 id\""));
                if (r1.Contains("uid=0"))
                    return true;

                // بعض إصدارات su تفضل الصيغة -c
                string r2 = await RunProcessAsync(_adbPath, BuildArgs("shell \"su -c id\""));
                return r2.Contains("uid=0");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// إعادة تشغيل الجهاز: بدون وسيطة = عادي، "recovery" = وضع الريكفري،
        /// "download" = وضع الداونلود (Odin) الخاص بسامسونج.
        /// </summary>
        public async Task<string> RebootAsync(string mode = null)
        {
            string verb = string.IsNullOrEmpty(mode) ? "reboot" : $"reboot {mode}";
            var r = await RunProcessExAsync(_adbPath, BuildArgs(verb), _timeoutMs);
            return r.Output;
        }

        private async Task<string> RunProcessAsync(string fileName, string arguments)
        {
            var r = await RunProcessExAsync(fileName, arguments, _timeoutMs);
            return r.Output;
        }

        private sealed class ProcessResult
        {
            public string Output;
            public int ExitCode;
        }

        private async Task<ProcessResult> RunProcessExAsync(string fileName, string arguments, int timeoutMs)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    throw new Exception($"ADB command failed to start: {fileName}\nError: {ex.Message}");
                }

                // اقرأ stdout و stderr معاً لتجنب الجمود عند امتلاء أي من المخزنين
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                var exitTask = Task.Run(new Action(process.WaitForExit));

                var completedTask = await Task.WhenAny(exitTask, Task.Delay(timeoutMs));
                if (completedTask != exitTask)
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException($"ADB command timed out after {timeoutMs}ms: {fileName} {arguments}");
                }

                string output = await outputTask;
                string error = await errorTask;

                return new ProcessResult
                {
                    Output = string.IsNullOrEmpty(error) ? output : output + "\n" + error,
                    ExitCode = process.HasExited ? process.ExitCode : -1
                };
            }
        }
    }
}
