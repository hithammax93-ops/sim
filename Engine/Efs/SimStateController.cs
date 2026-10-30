using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HmaxUnlocker.Engine.Communication;
using HmaxUnlocker.Engine.Logging;
using HmaxUnlocker.Engine.Models;

namespace HmaxUnlocker.Engine.Efs
{
    public class SimStateController
    {
        private readonly IAdbShell _adb;
        private readonly ILogger _logger;

        public SimStateController(IAdbShell adb, ILogger logger)
        {
            _adb = adb;
            _logger = logger;
        }

        public async Task<string> GetRawSimStateAsync()
        {
            string state = await _adb.ExecuteAsync("getprop gsm.sim.state");
            return string.IsNullOrWhiteSpace(state) ? "UNKNOWN" : state.Trim().ToUpperInvariant();
        }

        public async Task<LockStatus> EvaluateLockStatusFromSimAsync()
        {
            string simState = await GetRawSimStateAsync();
            _logger?.Debug($"[SimState] Current gsm.sim.state: {simState}");

            if (simState.Contains("NETWORK_LOCKED"))
            {
                return LockStatus.FactoryLocked;
            }

            if (simState.Contains("LOADED") || simState.Contains("READY"))
            {
                // جاهزية الشريحة لا تثبت فتح قفل الشبكة؛ لا نستخدم خاصية أنشأها البرنامج
                // نفسه كدليل على نجاح الفتح.
                _logger?.Debug("[SimState] SIM is ready, but network unlock cannot be inferred from this state.");
            }

            return LockStatus.Unknown;
        }

        /// <summary>
        /// يقرأ أسماء خدمات RIL المسجلة في init و حالتها الحالية من getprop.
        /// يفهم الصيغتين: [init.svc.X]: [running] و init.svc.X: running.
        /// يعيد أسماء الخدمات الجارية فقط (restart لخدمة متوقفة يغيّر سلوك الجهاز).
        /// </summary>
        private async Task<List<KeyValuePair<string, string>>> GetRilServicesWithStateAsync()
        {
            var found = new List<KeyValuePair<string, string>>();
            string props = await _adb.ExecuteAsync("getprop");

            foreach (string rawLine in props.Split('\n'))
            {
                int idx = rawLine.IndexOf("init.svc.", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    continue;

                string tail = rawLine.Substring(idx); // "init.svc.ril-daemon]: [running]" أو "init.svc.X: running"

                string propName;
                string state;
                int bracketEnd = tail.IndexOf(']');
                if (bracketEnd > 0)
                {
                    propName = tail.Substring(0, bracketEnd);
                    int valStart = tail.IndexOf('[', bracketEnd);
                    state = valStart >= 0
                        ? tail.Substring(valStart + 1).TrimEnd(']', ' ', '\r').Trim()
                        : "";
                }
                else
                {
                    int colon = tail.IndexOf(':');
                    if (colon < 0)
                        continue;
                    propName = tail.Substring(0, colon).Trim();
                    state = tail.Substring(colon + 1).Trim().Trim('\r');
                }

                string serviceName = propName.Substring("init.svc.".Length);
                if (serviceName.Length == 0)
                    continue;

                if (serviceName.IndexOf("ril", StringComparison.OrdinalIgnoreCase) >= 0
                    && !found.Any(s => s.Key == serviceName))
                {
                    found.Add(new KeyValuePair<string, string>(serviceName, state.ToLowerInvariant()));
                }
            }

            return found;
        }

        /// <summary>
        /// يعيد تشغيل خدمات RIL الجارية فعلياً: stop ← التحقق أن الحالة صارت stopped
        /// ← start ← التحقق أن الحالة عادت running. مع بديل ctl.stop/ctl.start
        /// إن لم توجد أدوات stop/start. كل خطوة تُسجَّل بصدق.
        /// </summary>
        public async Task<bool> RestartRilAsync()
        {
            _logger?.Info("[RIL] Detecting RIL services (init.svc.*)...");
            var services = await GetRilServicesWithStateAsync();
            var runningServices = services.Where(s => s.Value == "running").ToList();

            if (runningServices.Count == 0)
            {
                _logger?.Warn("[RIL] No running RIL service was identified; restart was not attempted.");
                return false;
            }

            bool allVerified = true;
            foreach (var svc in runningServices)
            {
                string name = svc.Key;
                _logger?.Info($"[RIL] Restarting service: {name}");

                await _adb.ExecuteRootAsync(
                    $"stop {name} 2>/dev/null || setprop ctl.stop {name}");
                await Task.Delay(1000);

                string afterStop = (await _adb.ExecuteAsync($"getprop init.svc.{name}")).Trim();
                bool stopped = afterStop.Equals("stopped", StringComparison.OrdinalIgnoreCase);
                if (!stopped) allVerified = false;
                _logger?.Info($"[RIL]   {name} after stop: '{afterStop}' {(stopped ? "(stopped OK)" : "(stop NOT confirmed)")}");

                // حاول إعادة التشغيل حتى عند تعذر تأكيد التوقف لتجنب ترك الخدمة متوقفة.
                await _adb.ExecuteRootAsync(
                    $"start {name} 2>/dev/null || setprop ctl.start {name}");
                await Task.Delay(500);

                string afterStart = (await _adb.ExecuteAsync($"getprop init.svc.{name}")).Trim();
                bool running = afterStart.Equals("running", StringComparison.OrdinalIgnoreCase);
                if (!running) allVerified = false;
                _logger?.Info($"[RIL]   {name} after start: '{afterStart}' {(running ? "(running OK)" : "(NOT running — check logcat radio)")}");
            }

            return allVerified;
        }

        public async Task<bool> RestartRilAndVerifyLoadedAsync(int timeoutSeconds = 15)
        {
            if (timeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));

            if (!await RestartRilAsync())
                return false;

            int elapsed = 0;
            while (elapsed < timeoutSeconds)
            {
                await Task.Delay(3000);
                elapsed += 3;

                string currentState = await GetRawSimStateAsync();
                _logger?.Info($"[SimState] Checking state ({elapsed}s): {currentState}");

                if (currentState.Contains("LOADED"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
