using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ISRO_HACKATHON.Services
{
    public sealed class PythonService
    {
        private readonly string pythonExe;
        private readonly string detectorScript;

        public PythonService(string pythonExe, string detectorScript)
        {
            this.pythonExe = pythonExe;
            this.detectorScript = detectorScript;
        }

        public bool IsReady(out string reason)
        {
            if (!File.Exists(pythonExe))
            {
                reason =
                    "Python environment was not found.\r\n\r\n" +
                    $"Expected:\r\n{pythonExe}\r\n\r\n" +
                    "Create the virtual environment and install requirements.";
                return false;
            }

            if (!File.Exists(detectorScript))
            {
                reason = $"Python detector was not found:\r\n{detectorScript}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public async Task<string> RunDetectorAsync(string imagePath, string modelVersion)
        {
            string workingDirectory = Path.GetDirectoryName(detectorScript)
                                      ?? AppContext.BaseDirectory;

            string escapedImage = imagePath.Replace("\"", "\\\"");
            string escapedModel = modelVersion.Replace("\"", "\\\"");

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{detectorScript}\" --image \"{escapedImage}\" --model \"{escapedModel}\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using Process process = new Process { StartInfo = psi };

            process.Start();

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Python detector failed.\r\n\r\n" +
                    stderr +
                    (string.IsNullOrWhiteSpace(stdout) ? "" : "\r\n" + stdout));
            }

            return stdout.Trim();
        }
    }
}
