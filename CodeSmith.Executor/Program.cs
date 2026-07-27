// == CodeSmith Executor Host == //
// Multi-language code runner for Azure Container Apps custom Dynamic Sessions.
// Isolation is provided by Hyper-V around this container; we only spawn subprocesses.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// == Probes (session pool liveness / startup) == //
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }));

// == Execute == //
app.MapPost("/execute", async (ExecuteRequest request, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Language))
        return Results.BadRequest(new { error = "language is required" });
    if (request.Code is null)
        return Results.BadRequest(new { error = "code is required" });

    var runTimeoutMs = request.RunTimeoutMs is > 0 and <= 120_000 ? request.RunTimeoutMs.Value : 10_000;
    var compileTimeoutMs = request.CompileTimeoutMs is > 0 and <= 120_000 ? request.CompileTimeoutMs.Value : 10_000;

    try
    {
        var result = await LanguageRunner.ExecuteAsync(
            request.Language.Trim().ToLowerInvariant(),
            request.Code,
            runTimeoutMs,
            compileTimeoutMs,
            ct);
        return Results.Ok(result);
    }
    catch (UnsupportedLanguageException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new ExecuteResponse
            {
                Stdout = "",
                Stderr = $"Executor infrastructure error: {ex.Message}",
                ExitCode = -1,
                TimedOut = false
            },
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();

// == DTOs == //
internal sealed class ExecuteRequest
{
    [JsonPropertyName("language")] public string Language { get; set; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("runTimeoutMs")] public int? RunTimeoutMs { get; set; }
    [JsonPropertyName("compileTimeoutMs")] public int? CompileTimeoutMs { get; set; }
}

internal sealed class ExecuteResponse
{
    [JsonPropertyName("stdout")] public string Stdout { get; set; } = string.Empty;
    [JsonPropertyName("stderr")] public string Stderr { get; set; } = string.Empty;
    [JsonPropertyName("exitCode")] public int ExitCode { get; set; }
    [JsonPropertyName("timedOut")] public bool TimedOut { get; set; }
}

internal sealed class UnsupportedLanguageException(string message) : Exception(message);

// == Language Runner == //
internal static class LanguageRunner
{
    private const int MaxOutputLength = 10_000;

    public static async Task<ExecuteResponse> ExecuteAsync(
        string language,
        string code,
        int runTimeoutMs,
        int compileTimeoutMs,
        CancellationToken ct)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "codesmith-exec", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            return language switch
            {
                "python" => await RunInterpretedAsync(workDir, "main.py", code, "python3", ["main.py"], runTimeoutMs, ct),
                "typescript" => await RunTypeScriptAsync(workDir, code, runTimeoutMs, ct),
                "go" => await RunCompiledAsync(workDir, "main.go", code, "go", ["build", "-o", "main", "main.go"], "./main", [], compileTimeoutMs, runTimeoutMs, ct),
                "cpp" => await RunCompiledAsync(workDir, "main.cpp", code, "g++", ["-O0", "-std=c++17", "-o", "main", "main.cpp"], "./main", [], compileTimeoutMs, runTimeoutMs, ct),
                "rust" => await RunCompiledAsync(workDir, "main.rs", code, "rustc", ["-o", "main", "main.rs"], "./main", [], compileTimeoutMs, runTimeoutMs, ct),
                "java" => await RunJavaAsync(workDir, code, compileTimeoutMs, runTimeoutMs, ct),
                "csharp" => await RunCSharpAsync(workDir, code, compileTimeoutMs, runTimeoutMs, ct),
                _ => throw new UnsupportedLanguageException($"Unsupported language '{language}'.")
            };
        }
        finally
        {
            TryDelete(workDir);
        }
    }

    // == Interpreted (Python) == //
    private static async Task<ExecuteResponse> RunInterpretedAsync(
        string workDir, string fileName, string code, string command, string[] args, int timeoutMs, CancellationToken ct)
    {
        await File.WriteAllTextAsync(Path.Combine(workDir, fileName), code, ct);
        return await RunProcessAsync(command, args, workDir, timeoutMs, ct);
    }

    // == TypeScript via ts-node or node with transpile == //
    private static async Task<ExecuteResponse> RunTypeScriptAsync(string workDir, string code, int timeoutMs, CancellationToken ct)
    {
        await File.WriteAllTextAsync(Path.Combine(workDir, "main.ts"), code, ct);
        // Prefer tsx (fast); fall back to npx ts-node if tsx missing.
        var tsx = await RunProcessAsync("tsx", ["main.ts"], workDir, timeoutMs, ct, allowMissingExecutable: true);
        if (tsx.ExitCode != 127 && !tsx.Stderr.Contains("No such file", StringComparison.OrdinalIgnoreCase))
            return tsx;
        return await RunProcessAsync("npx", ["--yes", "ts-node", "main.ts"], workDir, timeoutMs, ct);
    }

    // == Compile-then-run == //
    private static async Task<ExecuteResponse> RunCompiledAsync(
        string workDir,
        string fileName,
        string code,
        string compileCmd,
        string[] compileArgs,
        string runCmd,
        string[] runArgs,
        int compileTimeoutMs,
        int runTimeoutMs,
        CancellationToken ct)
    {
        await File.WriteAllTextAsync(Path.Combine(workDir, fileName), code, ct);
        var compile = await RunProcessAsync(compileCmd, compileArgs, workDir, compileTimeoutMs, ct);
        if (compile.TimedOut || compile.ExitCode != 0)
            return compile;
        return await RunProcessAsync(runCmd, runArgs, workDir, runTimeoutMs, ct);
    }

    // == Java == //
    private static async Task<ExecuteResponse> RunJavaAsync(string workDir, string code, int compileTimeoutMs, int runTimeoutMs, CancellationToken ct)
    {
        await File.WriteAllTextAsync(Path.Combine(workDir, "Main.java"), code, ct);
        var compile = await RunProcessAsync("javac", ["Main.java"], workDir, compileTimeoutMs, ct);
        if (compile.TimedOut || compile.ExitCode != 0)
            return compile;
        return await RunProcessAsync("java", ["Main"], workDir, runTimeoutMs, ct);
    }

    // == C# via dotnet SDK == //
    private static async Task<ExecuteResponse> RunCSharpAsync(string workDir, string code, int compileTimeoutMs, int runTimeoutMs, CancellationToken ct)
    {
        // Ephemeral console project; student code becomes Program.cs body (top-level statements OK).
        var create = await RunProcessAsync(
            "dotnet",
            ["new", "console", "-n", "App", "-o", "App", "--force"],
            workDir,
            compileTimeoutMs,
            ct);
        if (create.TimedOut || create.ExitCode != 0)
            return create;

        var projectDir = Path.Combine(workDir, "App");
        await File.WriteAllTextAsync(Path.Combine(projectDir, "Program.cs"), code, ct);

        // Single `dotnet run` restores (if needed), compiles, and executes.
        var totalTimeout = compileTimeoutMs + runTimeoutMs;
        return await RunProcessAsync("dotnet", ["run", "-c", "Release"], projectDir, totalTimeout, ct);
    }

    // == Process spawn == //
    private static async Task<ExecuteResponse> RunProcessAsync(
        string fileName,
        string[] args,
        string workDir,
        int timeoutMs,
        CancellationToken ct,
        bool allowMissingExecutable = false)
    {
        // .NET on Unix resolves a relative FileName against THIS process's current directory
        // (the image WORKDIR, /app) rather than StartInfo.WorkingDirectory. A compiled artifact
        // invoked as "./main" would therefore be looked up next to the host binary and fail with
        // ENOENT. Anchor "./" paths to the run directory; bare names still resolve through PATH.
        var executable = fileName.StartsWith("./", StringComparison.Ordinal)
            ? Path.Combine(workDir, fileName[2..])
            : fileName;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var a in args)
            process.StartInfo.ArgumentList.Add(a);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            if (!process.Start())
            {
                return new ExecuteResponse
                {
                    Stderr = $"Failed to start '{fileName}'.",
                    ExitCode = -1
                };
            }
        }
        catch (Exception ex) when (allowMissingExecutable)
        {
            return new ExecuteResponse
            {
                Stderr = ex.Message,
                ExitCode = 127
            };
        }
        catch (Exception ex)
        {
            return new ExecuteResponse
            {
                Stderr = $"Failed to start '{fileName}': {ex.Message}",
                ExitCode = -1
            };
        }

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            return new ExecuteResponse
            {
                Stdout = Truncate(stdout.ToString()),
                Stderr = Truncate(string.IsNullOrEmpty(stderr.ToString())
                    ? $"Process killed: execution exceeded {timeoutMs / 1000} second timeout."
                    : stderr.ToString()),
                ExitCode = -1,
                TimedOut = true
            };
        }

        return new ExecuteResponse
        {
            Stdout = Truncate(stdout.ToString()),
            Stderr = Truncate(stderr.ToString()),
            ExitCode = process.ExitCode,
            TimedOut = false
        };
    }

    private static string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= MaxOutputLength) return value;
        return value[..MaxOutputLength] + "\n[output truncated]";
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // best-effort
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
