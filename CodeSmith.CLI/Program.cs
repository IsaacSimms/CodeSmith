// == CodeSmith CLI Entry Point == //
using CodeSmith.CLI.Services;
using CodeSmith.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// == Host Configuration == //
var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);

var baseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured in appsettings.json.");

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
});

using var host = builder.Build();
var apiClient = host.Services.GetRequiredService<ApiClient>();

// == Cancellation Support == //
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await RunAsync(apiClient, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nSession cancelled. Goodbye!");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nError: {ex.Message}");
    Console.ResetColor();
    Environment.ExitCode = 1;
}

// == Main Application Loop == //
static async Task RunAsync(ApiClient apiClient, CancellationToken ct)
{
    Console.WriteLine("========================================");
    Console.WriteLine("       CodeSmith - Coding Tutor         ");
    Console.WriteLine("========================================");
    Console.WriteLine();

    // == Difficulty Selection == //
    Console.WriteLine("Select difficulty:");
    Console.WriteLine("  1. Easy");
    Console.WriteLine("  2. Medium");
    Console.WriteLine("  3. Hard");
    Console.Write("\nYour choice (1-3): ");

    var choice = Console.ReadLine()?.Trim();
    var difficulty = choice switch
    {
        "1" => Difficulty.Easy,
        "2" => Difficulty.Medium,
        "3" => Difficulty.Hard,
        _   => throw new InvalidOperationException("Invalid choice. Please enter 1, 2, or 3.")
    };

    Console.WriteLine($"\nGenerating {difficulty} problem...\n");

    var session = await apiClient.CreateSessionAsync(difficulty, ct);

    // == Display Problem == //
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("== Problem Description ==");
    Console.ResetColor();
    Console.WriteLine(session.ProblemDescription);
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("== Starter Code ==");
    Console.ResetColor();
    Console.WriteLine(session.StarterCode);
    Console.WriteLine();

    // == Chat Loop == //
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("== Chat ==");
    Console.WriteLine("Type your questions below. Type 'exit' to quit.");
    Console.ResetColor();
    Console.WriteLine();

    while (!ct.IsCancellationRequested)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("You: ");
        Console.ResetColor();

        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
            continue;

        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("\nGoodbye!");
            break;
        }

        var response = await apiClient.SendChatAsync(session.SessionId, input, ct);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("Tutor: ");
        Console.ResetColor();
        Console.WriteLine(response);
        Console.WriteLine();
    }
}
