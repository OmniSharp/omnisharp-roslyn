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
    ///  Desired maximum time-out for the process
    /// </summary>
    public int TimeOut { get; }

    public IDictionary<string, string> Environment { get; }

    public RunOptions(string workingDirectory = null, IList<string> output = null, int timeOut = 0)
    {
        this.WorkingDirectory = workingDirectory;
        this.Output = output;
        this.TimeOut = timeOut;
    }
}

/// <summary>
///  Wrapper for the exit code and state.
///  Used to query the result of an execution with method calls.
/// </summary>
public struct ExitStatus
{
    public int Code { get; }
    private bool _timeOut;

    /// <summary>
    ///  Default constructor when the execution finished.
    /// </summary>
    /// <param name="code">The exit code</param>
    public ExitStatus(int code)
    {
        this.Code = code;
        this._timeOut = false;
    }

    /// <summary>
    ///  Default constructor when the execution potentially timed out.
    /// </summary>
    /// <param name="code">The exit code</param>
    /// <param name="timeOut">True if the execution timed out</param>
    public ExitStatus(int code, bool timeOut)
    {
        this.Code = code;
        this._timeOut = timeOut;
    }

    /// <summary>
    ///  Flag signalling that the execution timed out.
    /// </summary>
    public bool DidTimeOut { get { return _timeOut; } }

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

    var process = System.Diagnostics.Process.Start(
            new ProcessStartInfo(command, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = runOptions.Output != null
            });

    if (runOptions.Output != null)
    {
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                runOptions.Output.Add(e.Data);
            }
        };

        process.BeginOutputReadLine();
    }

    if (runOptions.TimeOut == 0)
    {
        process.WaitForExit();
        return new ExitStatus(process.ExitCode);
    }
    else
    {
        bool finished = process.WaitForExit(runOptions.TimeOut);
        if (finished)
        {
            return new ExitStatus(process.ExitCode);
        }
        else
        {
            KillProcessTree(process);
            return new ExitStatus(0, true);
        }
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
///  Run tool with the given arguments
/// </summary>
/// <param name="command">Executable to run</param>
/// <param name="arguments">Arguments</param>
/// <param name="runOptions">Optional settings</param>
/// <returns>The exit status for further queries</returns>
ExitStatus RunTool(string command, string arguments, string workingDirectory, string logFileName = null)
{
    var output = new List<string>();
    var exitStatus = Run(command, arguments, new RunOptions(workingDirectory, output));

    var log = string.Join(System.Environment.NewLine, output);

    if (exitStatus.Code == 0)
    {
        Context.Log.Write(Verbosity.Diagnostic, LogLevel.Debug, "{0}", log);
    }
    else
    {
        Context.Log.Write(Verbosity.Normal, LogLevel.Error, "{0}", log);
    }

    if (logFileName != null)
    {
        System.IO.File.WriteAllText(logFileName, log);
    }

    return exitStatus;
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
        process.Kill();
    }
}
