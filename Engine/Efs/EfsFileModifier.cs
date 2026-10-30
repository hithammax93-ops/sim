using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HmaxUnlocker.Engine.Efs
{
    /// <summary>
    /// تعديل حقيقي لملفات الخصائص على الجهاز (telephony.prop / factory.prop):
    /// يحافظ على الأسطر الأصلية ويستبدل/يضيف المفاتيح المطلوبة فقط،
    /// ويكتب بدون BOM لأن BOM يفسد قراءة ملفات الخصائص في أندرويد.
    /// </summary>
    public class EfsFileModifier
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// يدمج الخصائص الجديدة في محتوى ملف خصائص موجود دون إتلاف الأسطر الأصلية.
        /// المفتاح الموجود مسبقاً يُستبدل (تُحذف نسخته القديمة ويُضاف سطره بالقيمة الجديدة).
        /// </summary>
        public string UpsertProps(string originalContent, IDictionary<string, string> props, string markerComment)
        {
            var output = new List<string>();
            var newKeys = new HashSet<string>(props.Keys, StringComparer.Ordinal);

            string normalized = (originalContent ?? "").Replace("\r", "").Replace("\0", "");
            foreach (string rawLine in normalized.Split('\n'))
            {
                string line = rawLine.TrimEnd();
                if (line.Length == 0)
                    continue;

                string trimmed = line.Trim();
                if (trimmed.StartsWith("#"))
                {
                    output.Add(line);
                    continue;
                }

                int eq = trimmed.IndexOf('=');
                if (eq <= 0)
                {
                    output.Add(line);
                    continue;
                }

                string key = trimmed.Substring(0, eq).Trim();
                if (newKeys.Contains(key))
                    continue; // سيُضاف لاحقاً بالقيمة الجديدة

                output.Add(line);
            }

            if (!string.IsNullOrEmpty(markerComment))
                output.Add(markerComment);

            foreach (var kv in props)
                output.Add(kv.Key + "=" + kv.Value);

            return string.Join("\n", output) + "\n";
        }

        /// <summary>محتوى ملف mps_code.dat: كود CSC فقط بدون أسطر إضافية.</summary>
        public string BuildMpsCodeContent(string targetCsc)
        {
            return targetCsc;
        }

        /// <summary>خصائص CSC التي تُكتب في factory.prop (تُقرأ عند الإقلاع).</summary>
        public IDictionary<string, string> BuildCscProps(string targetCsc, string mccMncWhitelist)
        {
            var props = new Dictionary<string, string>
            {
                ["persist.omc.salescode"] = targetCsc,
                ["persist.sys.csc"] = targetCsc,
                ["persist.vendor.csc"] = targetCsc,
                ["ro.csc.sales_code"] = targetCsc,
            };

            if (!string.IsNullOrWhiteSpace(mccMncWhitelist))
                props["persist.sys.hmax.netlist"] = mccMncWhitelist;

            return props;
        }

        /// <summary>خصائص الشبكة التي تُكتب في telephony.prop.</summary>
        public IDictionary<string, string> BuildTelephonyProps()
        {
            return new Dictionary<string, string>
            {
                ["persist.radio.def_network"] = "26,26",
                ["persist.radio.multisim.config"] = "dsds",
                ["persist.vendor.radio.pref_network_mode"] = "26",
                ["ro.telephony.default_network"] = "26,26",
            };
        }

        public void WriteLocalFile(string filePath, string content)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, content ?? "", Utf8NoBom);
        }

        /// <summary>يقرر ما إذا كان المحتوى الجديد غيّر شيئاً فعلاً مقارنة بالأصلي.</summary>
        public static bool ContentChanged(string original, string modified)
        {
            return !string.Equals((original ?? "").Trim(), (modified ?? "").Trim(), StringComparison.Ordinal);
        }
    }
}
