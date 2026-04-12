// == Local Process Code Execution Service == //
using System.Diagnostics;
using System.Runtime.InteropServices;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Executes user-submitted code in a local host process with a configurable timeout.
/// Supports interpreted and compiled languages via language-specific execution strategies.
/// Unsafe for production use: code runs with the API process's permissions. Use
/// PistonCodeExecutionService for any non-development scenario.
/// </summary>
public class LocalProcessCodeExecutionService : ICodeExecutionService
{
    private const int TimeoutSeconds = 10;
    private const int MaxOutputLength = 10_000;

    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly string ExecutableExtension = IsWindows ? ".exe" : "";

    private readonly ILogger<LocalProcessCodeExecutionService> _logger;

    public LocalProcessCodeExecutionService(ILogger<LocalProcessCodeExecutionService> logger)
    {
        _logger = logger;
    }

    // == Execute User Code == //
    public async Task<CodeExecutionResult> ExecuteAsync(Language language, string code, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "codesmith", Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(tempDir);

            var strategy = GetExecutionStrategy(language);
            var filePath = Path.Combine(tempDir, strategy.FileName);
            await File.WriteAllTextAsync(filePath, code, ct);

            // Compile first if needed (C++, Rust, Java)
            if (strategy.CompileCommand is not null)
            {
                var compileResult = await RunProcessAsync(strategy.CompileCommand, strategy.CompileArgs!, tempDir, ct);

                if (compileResult.ExitCode != 0)
                {
                    return new CodeExecutionResult
                    {
                        Stdout = Truncate(compileResult.Stdout),
                        Stderr = Truncate(compileResult.Stderr),
                        ExitCode = compileResult.ExitCode,
                        TimedOut = compileResult.TimedOut
                    };
                }
            }

            // Run the code
            var runResult = await RunProcessAsync(strategy.RunCommand, strategy.RunArgs, tempDir, ct);

            return new CodeExecutionResult
            {
                Stdout = Truncate(runResult.Stdout),
                Stderr = Truncate(runResult.Stderr),
                ExitCode = runResult.ExitCode,
                TimedOut = runResult.TimedOut
            };
        }
        catch (CodeExecutionException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code execution infrastructure failure for {Language}", language);
            throw new CodeExecutionException($"Failed to execute {language} code: {ex.Message}", ex);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    // == Process Runner == //
    private async Task<ProcessResult> RunProcessAsync(string command, string arguments, string workingDirectory, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new CodeExecutionException($"Failed to start '{command}': {ex.Message}. Is the runtime installed?", ex);
        }

        // Read stdout/stderr concurrently before WaitForExitAsync to avoid deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

        bool timedOut = false;

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            timedOut = true;
            KillProcessTree(process);
        }

        // Collect whatever output we have
        string stdout, stderr;
        try
        {
            stdout = await stdoutTask;
            stderr = await stderrTask;
        }
        catch (OperationCanceledException)
        {
            stdout = "";
            stderr = timedOut ? "Process killed: execution exceeded 10 second timeout." : "";
        }

        return new ProcessResult
        {
            Stdout = stdout,
            Stderr = stderr,
            ExitCode = timedOut ? -1 : process.ExitCode,
            TimedOut = timedOut
        };
    }

    // == Language Execution Strategies == //
    private ExecutionStrategy GetExecutionStrategy(Language language) => language switch
    {
        Language.Python     => new ExecutionStrategy("main.py",    RunCommand: IsWindows ? "python" : "python3", RunArgs: "main.py"),
        Language.TypeScript => new ExecutionStrategy("main.ts",    RunCommand: "npx",    RunArgs: "tsx main.ts"),
        Language.Go         => new ExecutionStrategy("main.go",    RunCommand: "go",     RunArgs: "run main.go"),
        Language.CSharp     => new ExecutionStrategy("main.csx",   RunCommand: "dotnet-script", RunArgs: "main.csx"),
        Language.Cpp        => new ExecutionStrategy("main.cpp",   RunCommand: IsWindows ? "main.exe" : "./main", RunArgs: "",
                                   CompileCommand: "g++", CompileArgs: $"-o main{ExecutableExtension} main.cpp"),
        Language.Rust       => new ExecutionStrategy("main.rs",    RunCommand: IsWindows ? "main.exe" : "./main", RunArgs: "",
                                   CompileCommand: "rustc", CompileArgs: $"-o main{ExecutableExtension} main.rs"),
        Language.Java       => new ExecutionStrategy("Main.java",  RunCommand: "java",   RunArgs: "-cp . Main",
                                   CompileCommand: "javac", CompileArgs: "Main.java"),
        _ => throw new CodeExecutionException($"Unsupported language: {language}")
    };

    // == Helpers == //
    private static void KillProcessTree(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch { /* Process may have already exited */ }
    }

    private static string Truncate(string value)
    {
        if (value.Length <= MaxOutputLength) return value;
        return value[..MaxOutputLength] + "\n[output truncated]";
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temp directory: {Path}", path);
        }
    }

    // == Internal Types == //
    private record ExecutionStrategy(
        string FileName,
        string RunCommand,
        string RunArgs,
        string? CompileCommand = null,
        string? CompileArgs = null);

    private record ProcessResult
    {
        public string Stdout { get; init; } = "";
        public string Stderr { get; init; } = "";
        public int ExitCode { get; init; }
        public bool TimedOut { get; init; }
    }
}
