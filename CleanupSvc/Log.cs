using System;
using System.Diagnostics;

namespace CleanupSvc
{
    class EventLogger
    {
        private static readonly int PID = Process.GetCurrentProcess().Id;

        public const string eventLog = "Application";
        public const string eventSource = "CleanupSvc";

        public static void Error(string message, Exception e = null)
        {
            EventLog.WriteEntry(eventSource, string.Format("{0}\n{1}", message, e), EventLogEntryType.Error, PID);
        }

        public static void Warning(string message, Exception e = null)
        {
            EventLog.WriteEntry(eventSource, string.Format("{0}\n{1}", message, e), EventLogEntryType.Warning, PID);
        }

        public static void Info(string message)
        {
            EventLog.WriteEntry(eventSource, message, EventLogEntryType.Information, PID);
        }
    }

    class ByteSuffixes
    {
        public static string GetString(long i)
        {
            if (i < 1024) return i.ToString() + " bytes";
            double j = i / 1024.0;
            if (j < 1024) return j.ToString("0.##") + "KB";
            j /= 1024;
            if (j < 1024) return j.ToString("0.##") + "MB";
            j /= 1024;
            if (j < 1024) return j.ToString("0.##") + "GB";
            j /= 1024;
            if (j < 1024) return j.ToString("0.##") + "TB";
            j /= 1024;
            if (j < 1024) return j.ToString("0.##") + "PB";
            j /= 1024;
            if (j < 1024) return j.ToString("0.##") + "EB";
            return i.ToString() + " freaking bytes";
        }
    }
}
