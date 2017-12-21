#load "platform.cake"

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

/// <summary>
///  Class encompassing the optional settings for running processes.
/// </summary>
public class RunOptions
{
    /// <summary>
    ///  The working directory of the process.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    ///  Container logging the StandardOutput content.
    /// </summary>
    public IList<string> Output { get; }

    /// <summary>
    ///  Wait for process to become idle before terminating it.
    /// </summary>
    public bool WaitForIdle { get; }

    public IDictionary<string, string> Environment { get; }

    public RunOptions(string workingDirectory = null, IList<string> output = null, bool waitForIdle = false, IDictionary<string, string> environment = null)
    {
        this.WorkingDirectory = workingDirectory;
        this.Output = output;
        this.WaitForIdle = waitForIdle;
        this.Environment = environment;
    }
}

/// <summary>
///  Wrapper for the exit code and state.
///  Used to query the result of an execution with method calls.
/// </summary>
public struct ExitStatus
{
    public int Code { get; }
    private bool _wasIdle;

    /// <summary>
    ///  Default constructor when the execution finished.
    /// </summary>
    /// <param name="code">The exit code</param>
    public ExitStatus(int code)
    {
        this.Code = code;
        this._wasIdle = false;
    }

    /// <summary>
    ///  Default constructor when the execution potentially timed out.
    /// </summary>
    /// <param name="code">The exit code</param>
    /// <param name="wasIdle">True if the execution timed out</param>
    public ExitStatus(int code, bool wasIdle)
    {
        this.Code = code;
        this._wasIdle = wasIdle;
    }

    /// <summary>
    ///  Flag signalling that the execution timed out.
    /// </summary>
    public bool WasIdle { get { return _wasIdle; } }

    /// <summary>
    ///  Implicit conversion from ExitStatus to the exit code.
    /// </summary>
    /// <param name="exitStatus">The exit status</param>
    /// <returns>The exit code</returns>
    public static implicit operator int(ExitStatus exitStatus)
    {
        return exitStatus.Code;
    }

    /// <summary>
    ///  Trigger Exception for non-zero exit code.
    /// </summary>
    /// <param name="errorMessage">The message to use in the Exception</param>
    /// <returns>The exit status for further queries</returns>
    public ExitStatus ExceptionOnError(string errorMessage)
    {
        if (this.Code != 0)
        {
            throw new Exception(errorMessage);
        }

        return this;
    }
}

/// <summary>
///  Run the given executable with the given arguments.
/// </summary>
/// <param name="command">Executable to run</param>
/// <param name="arguments">Arguments</param>
/// <returns>The exit status for further queries</returns>
ExitStatus Run(string command, string arguments)
{
    return Run(command, arguments, new RunOptions());
}

/// <summary>
///  Run the given executable with the given arguments.
/// </summary>
/// <param name="command">Executable to run</param>
/// <param name="arguments">Arguments</param>
/// <param name="workingDirectory">Working directory</param>
/// <returns>The exit status for further queries</returns>
ExitStatus Run(string command, string arguments, string workingDirectory)
{
    return Run(command, arguments, new RunOptions(workingDirectory));
}

/// <summary>
///  Run the given command with the given arguments.
/// </summary>
/// <param name="exec">Command to run</param>
/// <param name="arguments">Arguments</param>
/// <param name="runOptions">Optional settings</param>
/// <returns>The exit status for further queries</returns>
ExitStatus Run(string command, string arguments, RunOptions runOptions)
{
    var workingDirectory = runOptions.WorkingDirectory ?? System.IO.Directory.GetCurrentDirectory();

    Context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, "Run:");
    Context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, "  Command: {0}", command);
    Context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, "  Arguments: {0}", arguments);
    Context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, "  CWD: {0}", workingDirectory);

    var startInfo = new ProcessStartInfo(command, arguments)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = runOptions.Output != null || runOptions.WaitForIdle
    };

    if (runOptions.Environment != null)
    {
        foreach (var item in runOptions.Environment)
        {
            startInfo.EnvironmentVariables.Add(item.Key, item.Value);
        }
    }

    var process = System.Diagnostics.Process.Start(startInfo);
    var lastDateTime = DateTime.Now;

    if (runOptions.Output != null || runOptions.WaitForIdle)
    {
        process.OutputDataReceived += (s, e) =>
        {
            if (runOptions.WaitForIdle)
            {
                lastDateTime = DateTime.Now;
            }

            if (e.Data != null)
            {
                if (runOptions.Output != null)
                {
                    runOptions.Output.Add(e.Data);
                }
                else if (runOptions.WaitForIdle)
                {
                    Console.WriteLine(e.Data);
                }
            }
        };

        process.BeginOutputReadLine();
    }

    if (!runOptions.WaitForIdle)
    {
        process.WaitForExit();
        return new ExitStatus(process.ExitCode);
    }
    else
    {
        while (true)
        {
            var exited = process.HasExited || process.WaitForExit(10000);
            if (exited)
            {
                return new ExitStatus(process.ExitCode);
            }

            var currentDateTime = DateTime.Now;
            var timeSpan = currentDateTime - lastDateTime;

            if (timeSpan.TotalMilliseconds >= 10000)
            {
                break;
            }
        }

        KillProcessTree(process);
        return new ExitStatus(0, true);
    }
}

string RunAndCaptureOutput(string command, string arguments, string workingDirectory = null)
{
    var output = new List<string>();
    Run(command, arguments, new RunOptions(workingDirectory, output))
        .ExceptionOnError($"Failed to run '{command}' with arguments, '{arguments}'.");

    var builder = new StringBuilder();
    foreach (var line in output)
    {
        builder.AppendLine(line);
    }

    return builder.ToString().Trim();
}

/// <summary>
///  Kill the given process and all its child processes.
/// </summary>
/// <param name="process">Root process</param>
private void KillProcessTree(Process process)
{
    // Child processes are not killed on Windows by default
    // Use TASKKILL to kill the process hierarchy rooted in the process
    if (Platform.Current.IsWindows)
    {
        StartProcess($"TASKKILL",
            new ProcessSettings
            {
                Arguments = $"/PID {process.Id} /T /F",
            });
    }
    else
    {
        foreach (var pid in GetUnixChildProcessIds(process.Id))
        {
            Run("kill", pid.ToString());
        }

        Run("kill", process.Id.ToString());
    }
}

int[] GetUnixChildProcessIds(int processId)
{
    var output = RunAndCaptureOutput("ps", "-A -o ppid,pid");
    var lines = output.Split(new[] { System.Environment.NewLine }, System.StringSplitOptions.RemoveEmptyEntries);
    var childPIDs = new List<int>();

    foreach (var line in lines)
    {
        var pairs = line.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

        int ppid;
        if (int.TryParse(pairs[0].Trim(), out ppid) && ppid == processId)
        {
            childPIDs.Add(int.Parse(pairs[1].Trim()));
        }

    }

    return childPIDs.ToArray();
}
