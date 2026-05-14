using System.Diagnostics;
using System.Runtime.InteropServices;
using BLEProximity.Models;

namespace BLEProximity.Services;

/// <summary>
/// Executes configured commands when the proximity monitor triggers.
/// Supports placeholder substitution, process timeout enforcement, and error handling.
/// </summary>
public class CommandExecutor : ICommandExecutor
{
    private const int MaxExecutablePathLength = 260;
    private const int MaxArgumentsLength = 8191;
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    private readonly IToastNotifier? _toastNotifier;

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    public CommandExecutor(IToastNotifier? toastNotifier = null)
    {
        _toastNotifier = toastNotifier;
    }

    /// <summary>
    /// Substitutes placeholders in the arguments string with values from the device context.
    /// </summary>
    public static string SubstitutePlaceholders(string arguments, DeviceContext context)
    {
        if (string.IsNullOrEmpty(arguments))
            return arguments;

        return arguments
            .Replace("{mac}", context.MacAddress, StringComparison.OrdinalIgnoreCase)
            .Replace("{name}", context.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{rssi}", context.SmoothedRssi.ToString("F1"), StringComparison.OrdinalIgnoreCase)
            .Replace("{timestamp}", context.Timestamp.ToString("o"), StringComparison.OrdinalIgnoreCase);
    }

    public async Task ExecuteAsync(CommandConfig config, DeviceContext context)
    {
        Console.WriteLine($"[CommandExecutor] ExecuteAsync called with: {config.ExecutablePath} {config.Arguments}");
        
        var executablePath = config.ExecutablePath;
        var arguments = config.Arguments;

        // Apply defaults if not configured
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Console.WriteLine("[CommandExecutor] No executable configured, using default LockWorkStation");
            executablePath = "rundll32.exe";
            arguments = "user32.dll,LockWorkStation";
        }

        // Handle LockWorkstation directly via Win32 API (most reliable)
        if (executablePath.Equals("rundll32.exe", StringComparison.OrdinalIgnoreCase)
            && arguments.Contains("LockWorkStation", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[CommandExecutor] Locking workstation via Win32 API");
            LockWorkStation();
            Console.WriteLine("[CommandExecutor] LockWorkStation API call completed");
            return;
        }

        // Enforce length limits
        if (executablePath.Length > MaxExecutablePathLength)
            executablePath = executablePath[..MaxExecutablePathLength];
        if (arguments.Length > MaxArgumentsLength)
            arguments = arguments[..MaxArgumentsLength];

        // Substitute placeholders
        var substitutedArguments = SubstitutePlaceholders(arguments, context);
        System.Diagnostics.Debug.WriteLine($"[CommandExecutor] Final command: {executablePath} {substitutedArguments}");

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = substitutedArguments,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process? process = null;
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CommandExecutor] Starting process: {executablePath}");
            process = Process.Start(startInfo);

            if (process == null)
            {
                System.Diagnostics.Debug.WriteLine($"[CommandExecutor] Failed to start process: {executablePath}");
                Debug.WriteLine($"[CommandExecutor] Failed to start process: {executablePath}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[CommandExecutor] Process started successfully, PID: {process.Id}");

            // UseShellExecute=true may not support WaitForExitAsync for all processes
            // Use a simple timeout wait
            try
            {
                var exited = process.WaitForExit((int)ProcessTimeout.TotalMilliseconds);
                if (!exited)
                {
                    System.Diagnostics.Debug.WriteLine($"[CommandExecutor] Process '{executablePath}' exceeded timeout. Killing.");
                    Debug.WriteLine($"[CommandExecutor] Process '{executablePath}' exceeded timeout. Killing.");
                    try { process.Kill(entireProcessTree: true); } catch { }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CommandExecutor] Process completed with exit code: {process.ExitCode}");
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited or no handle available (common with UseShellExecute)
                System.Diagnostics.Debug.WriteLine("[CommandExecutor] Process handle not available (UseShellExecute), assuming success");
            }

            await Task.CompletedTask; // Keep method async-compatible
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                    || ex is InvalidOperationException
                                    || ex is ObjectDisposedException)
        {
            System.Diagnostics.Debug.WriteLine($"[CommandExecutor] Command execution failed: {ex}");
            Debug.WriteLine($"[CommandExecutor] Command execution failed: {ex.Message}");
            _toastNotifier?.ShowCountdownToast("Command Failed", $"Could not execute: {executablePath} - {ex.Message}", 0);
        }
        finally
        {
            process?.Dispose();
        }
    }
}
