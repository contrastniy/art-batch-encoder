using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ArtBatchEncoder
{
    internal static class OpenImageIoProbe
    {
        public static string Probe(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                throw new FileNotFoundException("oiiotool.exe was not found.", executablePath);

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = executablePath;
            startInfo.Arguments = "--version";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                    throw new InvalidOperationException("Could not start oiiotool.exe.");

                var standardOutput = process.StandardOutput.ReadToEnd();
                var standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
                    throw new InvalidOperationException("oiiotool.exe returned exit code " + process.ExitCode + ".\r\n" + details.Trim());
                }

                var combined = (standardOutput + Environment.NewLine + standardError).Trim();
                if (combined.Length == 0)
                    return "OpenImageIO detected";

                var lineEnd = combined.IndexOfAny(new[] { '\r', '\n' });
                return lineEnd >= 0 ? combined.Substring(0, lineEnd).Trim() : combined;
            }
        }
    }
}
