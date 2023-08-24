﻿using System;
using System.Text;
using System.IO;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;

namespace CineGame.SDK {

    internal class CineGameLogger
    {
        static StreamWriter LogWriter;
        static string ENV_VAR_LOG_DIR = "LOG_DIR";
        static string DeviceId = SystemInfo.deviceUniqueIdentifier;

        public static string GameID;
        public static string LogName;
        public static string LogPath;

        static string BuildTime;
        static int NumLogErrors = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            if (Application.isEditor || LogWriter != null)
            {
                return;
            }

            LogName = "CineGame-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".log";

            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_VAR_LOG_DIR)))
            {
                LogPath = Application.persistentDataPath;
            }
            else
            {
                LogPath = Environment.GetEnvironmentVariable(ENV_VAR_LOG_DIR);
            }

            if (!Directory.Exists(LogPath))
            {
                Directory.CreateDirectory(LogPath);
            }

            FileStream stream = File.Open(LogPath + "/" + LogName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            LogWriter = new StreamWriter(stream, Encoding.UTF8);
            LogWriter.AutoFlush = true;
            Application.logMessageReceived -= HandleLogMessage;
            Application.logMessageReceived += HandleLogMessage;

            //Generate buildtime string from compiler-generated assembly version
            Version assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            DateTime startDate = new DateTime(2000, 1, 1, 0, 0, 0);
            TimeSpan span = new TimeSpan(assemblyVersion.Build, 0, 0, assemblyVersion.Revision * 2);
            DateTime buildDate = startDate.Add(span);
            BuildTime = buildDate.ToString("u");

            Debug.LogFormat("Engine: {0} Build version: {1} Build time: {2} Hostname: {3}", Application.unityVersion, assemblyVersion.ToString(), BuildTime, Environment.MachineName);

        }

        static void HandleLogMessage(string condition, string stackTrace, LogType logType)
        {
            LogWriter.WriteLine("{0} {1} {2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff"), (logType == LogType.Log) ? "Info" : logType.ToString(), condition);

            if (logType == LogType.Exception || logType == LogType.Assert || logType == LogType.Error)
            {
                float timeSinceStartup = Time.realtimeSinceStartup;
                string timeSinceStartupString = "[ " + string.Format("{0:00}", (int)timeSinceStartup / 60 % 60) + ":" + string.Format("{0:00}", (int)timeSinceStartup % 60) + " ] ";
                LogWriter.WriteLine(timeSinceStartupString + stackTrace);
            }
        }
    }
}
