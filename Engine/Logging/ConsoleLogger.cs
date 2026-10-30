using System;

namespace HmaxUnlocker.Engine.Logging
{
    public class ConsoleLogger : ILogger
    {
        public void Info(string msg)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("[INFO]  " + DateTime.Now.ToString("HH:mm:ss") + " " + msg);
            Console.ResetColor();
        }

        public void Warn(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[WARN]  " + DateTime.Now.ToString("HH:mm:ss") + " " + msg);
            Console.ResetColor();
        }

        public void Error(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] " + DateTime.Now.ToString("HH:mm:ss") + " " + msg);
            Console.ResetColor();
        }

        public void Debug(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("[DEBUG] " + DateTime.Now.ToString("HH:mm:ss") + " " + msg);
            Console.ResetColor();
        }

        public void Log(string msg)
        {
            Info(msg);
        }
    }
}
