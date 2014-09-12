using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using AmiBroker.Data;

namespace Traderdata.Client.ABConnector.Util
{
    internal enum MessageType : int { Error = 0, Warning = 1, Info = 2, Trace = 3 }

    [DebuggerStepThrough]
    internal static class LogAndMessage
    {
        private static bool verboseLog;
        private static SortedList<DateTime, string> queuedMessages;
        private static DateTime lastQueued;

        static LogAndMessage()
        {
            queuedMessages = new SortedList<DateTime, string>();
            lastQueued = DateTime.Now;
        }

        internal static bool VerboseLog
        {
            get
            {
                return verboseLog;
            }
            set
            {
                verboseLog = value;
            }
        }

        internal static void Log(MessageType type, string message)
        {
            const string logSource = "Traderdata - ABDataConnector";

            if (IsLoggable(type))
            {
                switch (type)
                {
                    case MessageType.Error:
                        DataSourceBase.DotNetLog(logSource, "Error", message);
                        break;

                    case MessageType.Warning:
                        DataSourceBase.DotNetLog(logSource, "Warning", message);
                        break;

                    case MessageType.Info:
                        DataSourceBase.DotNetLog(logSource, "Info", message);
                        break;

                    case MessageType.Trace:

                        DataSourceBase.DotNetLog(logSource, "Trace", message);
                        break;
                }
            }
        }

        internal static void LogAndAdd(MessageType type, string message)
        {
            if (IsLoggable(type))
            {
                QueueMessage(type, message);
                Log(type, message);
            }
        }

        internal static string GetMessages()
        {
            if (queuedMessages.Count == 0)
                return string.Empty;

            // remove all old messages
            DateTime holdTime = DateTime.Now.AddSeconds(-5);
            while (queuedMessages.Count > 0 && queuedMessages.Keys[0] < holdTime)
                queuedMessages.RemoveAt(0);

            // build displayed plugin messages
            StringBuilder msg = new StringBuilder(1000);
            foreach (KeyValuePair<DateTime, string> kvp in queuedMessages)
            {
                msg.Append(kvp.Value);
                msg.Append(Environment.NewLine);
            }

            return msg.ToString();
        }

        private static void QueueMessage(MessageType type, string message)
        {
            SetLastAddTime();

            switch (type)
            {
                case MessageType.Error:
                    queuedMessages.Add(lastQueued, "ERROR! " + message);
                    break;

                case MessageType.Warning:
                    queuedMessages.Add(lastQueued, "Warning! " + message);
                    break;

                case MessageType.Info:
                    queuedMessages.Add(lastQueued, message);
                    break;

                case MessageType.Trace:
                    queuedMessages.Add(lastQueued, message);
                    break;
            }
        }

        private static bool IsLoggable(MessageType type)
        {
            return type == MessageType.Error
                || type == MessageType.Warning
                || type == MessageType.Info
                || type == MessageType.Trace && verboseLog;
        }

        private static void SetLastAddTime()
        {
            // to make at least 1 second between messages
            if (lastQueued.AddSeconds(1) > DateTime.Now)
                lastQueued = lastQueued.AddSeconds(1);
            else
                lastQueued = DateTime.Now;
        }
    }
}
