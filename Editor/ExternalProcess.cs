using System;
using System.ComponentModel;
using System.Text;
using UnityEngine;

namespace CineGame.HostEditor
{
    internal static class ExternalProcess
    {
        public delegate bool ProgressDelegate(string sMessage, float percent);

        /// <summary>
        /// Run external process from filename and arguments, return true if exit with 0, false otherwise.
        /// If progressCallback is defined, you can read each line of the StandardOutput.
        /// </summary>
        public static bool Run(bool createWindow, string filename, string arguments = null, string workingDirectory = null, ProgressDelegate progressCallback = null)
        {
            try
            {
                var p = new System.Diagnostics.Process();
                p.StartInfo.FileName = filename;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = !createWindow;
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    p.StartInfo.WorkingDirectory = workingDirectory;
                }
                if (p.Start())
                {
                    var reader = p.StandardOutput;
                    var sb = new StringBuilder();
                    while (!reader.EndOfStream)
                    {
                        var outputLine = reader.ReadLine().Trim();
                        sb.AppendLine(outputLine);
                        if (progressCallback != null)
                        {
                            if (progressCallback(outputLine, 0f))
                            {
                                p.Kill();
                                break;
                            }
                        }
                    }
                    reader = p.StandardError;
                    if (!reader.EndOfStream)
                    {
                        Debug.LogErrorFormat("SystemProcess {0} >> {1}", filename, reader.ReadToEnd());
                    }
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        Debug.LogErrorFormat("SystemProcess {0} exitcode={1}", filename, p.ExitCode);
                    }
                    return p.ExitCode == 0;
                }
            }
            catch (Win32Exception win32e)
            {
                Debug.LogErrorFormat("SystemProcess: Win32Exception while running {0}: {1}", filename, win32e);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("SystemProcess: Exception while running {0}: {1}", filename, e);
            }
            return false;
        }


        /// <summary>
        /// Run external process from filename and arguments, return true if exit with 0, false otherwise.
        /// If progressCallback is defined, you can read each line of the StandardOutput.
        /// </summary>
        public static bool Run(string filename, string arguments = null, string workingDirectory = null, ProgressDelegate progressCallback = null)
        {
            return Run(false, filename, arguments, workingDirectory, progressCallback);
        }
    }
}
