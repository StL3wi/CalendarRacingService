using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

public class DebugConsole
{
    private static Process _debugProcess;
    private static StreamWriter _debugWriter;
    private static bool _isInitialized = false;
    private static readonly object _lockObject = new object();

    public void Initialize(bool debugEnabled)
    {
        

        try
        {
            // Start a new CMD process
            _debugProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/k title Debug Console",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = false
                }
            };

            _debugProcess.Start();
            _debugWriter = _debugProcess.StandardInput;

            // Write a header to the debug console
            WriteDebug("=== DEBUG CONSOLE INITIALIZED ===");
            WriteDebug($"Application started at: {DateTime.Now}");
            WriteDebug("===============================");

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize debug console: {ex.Message}");
        }
    }

    public void WriteDebug(string message)
    {
        if (!_isInitialized || _debugWriter == null)
            return;

        try
        {
            lock (_lockObject)
            {
                string formattedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                _debugWriter.WriteLine(formattedMessage);
                _debugWriter.Flush(); // Ensure it's written immediately
            }
        }
        catch (Exception)
        {
            // Silently fail if writing to debug console fails
        }
    }

    public void Shutdown()
    {
        if (!_isInitialized)
            return;

        try
        {
            WriteDebug("=== DEBUG CONSOLE SHUTTING DOWN ===");

            // Give it a moment to display the last message
            Thread.Sleep(500);

            _debugWriter?.Close();

            // Don't force close the window - let the user close it manually
            // if they want to review the logs

            _isInitialized = false;
        }
        catch (Exception)
        {
            // Silently fail if shutdown fails
        }
    }
}