using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Process = System.Diagnostics.Process;

namespace CSharpier.VisualStudio
{
    public class CSharpierProcessPipeMultipleFiles : ICSharpierProcess
    {
        private readonly Logger logger;
        private readonly Process process;

        private volatile bool done;

        private readonly StringBuilder output = new StringBuilder();
        private readonly StringBuilder errorOutput = new StringBuilder();
        private readonly StreamWriter standardIn;

        public CSharpierProcessPipeMultipleFiles(string csharpierPath, Logger logger)
        {
            this.logger = logger;

            var processStartInfo = new ProcessStartInfo(csharpierPath, " --pipe-multiple-files")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            processStartInfo.EnvironmentVariables["DOTNET_NOLOGO"] = "1";
            this.process = new Process { StartInfo = processStartInfo };
            this.process.Start();
            this.standardIn = new StreamWriter(
                this.process.StandardInput.BaseStream,
                Encoding.UTF8
            );

            this.logger.Debug("Warm CSharpier with initial format");
            // warm by formatting a file twice, the 3rd time is when it gets really fast
            this.FormatFile("public class ClassName { }", "Test.cs");
            this.FormatFile("public class ClassName { }", "Test.cs");
        }

        public string FormatFile(string content, string filePath)
        {
            this.output.Clear();
            this.errorOutput.Clear();

            this.standardIn.Write(filePath);
            this.standardIn.Write('\u0003');
            this.standardIn.Write(content);
            this.standardIn.Write('\u0003');
            this.standardIn.Flush();

            this.done = false;

            var outputThread = new Thread(this.ReadOutput);
            var errorThread = new Thread(this.ReadError);
            outputThread.Start();
            errorThread.Start();

            while (!this.done)
            {
                Thread.Sleep(1);
            }

            outputThread.Abort();
            errorThread.Abort();

            var errorResult = this.errorOutput.ToString();
            var result = this.output.ToString();
            if (string.IsNullOrEmpty(errorResult))
            {
                if (string.IsNullOrEmpty(result))
                {
                    this.logger.Info("File is ignored by .csharpierignore");
                    return string.Empty;
                }
                else
                {
                    return this.output.ToString();
                }
            }

            this.logger.Info("Got error output: " + errorResult);
            return string.Empty;
        }

        private void ReadOutput(object state)
        {
            this.ReadFromProcess(this.process.StandardOutput, this.output);
        }

        private void ReadError(object state)
        {
            while (true)
            {
                this.logger.Debug("waiting");
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            this.ReadFromProcess(this.process.StandardError, this.errorOutput);
        }

        private void ReadFromProcess(StreamReader reader, StringBuilder stringBuilder)
        {
            try
            {
                var nextCharacter = reader.Read();
                while (nextCharacter != -1)
                {
                    if (nextCharacter == '\u0003')
                    {
                        return;
                    }

                    stringBuilder.Append((char)nextCharacter);
                    nextCharacter = reader.Read();
                }
            }
            catch (Exception e)
            {
                this.logger.Error(e);
            }
            finally
            {
                this.done = true;
            }
        }

        public void Dispose()
        {
            this.process.Kill();
        }
    }
}
