# CodeSmith

An AI-powered coding tutor that generates C# programming problems and provides guided assistance through the Anthropic Claude API.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An [Anthropic API key](https://console.anthropic.com/)

## Solution Structure

| Project | Description |
|---------|-------------|
| `CodeSmith.Core` | Shared models, enums, interfaces, and custom exceptions |
| `CodeSmith.Infrastructure` | Anthropic SDK integration and in-memory session storage |
| `CodeSmith.Api` | ASP.NET Core Web API with rate limiting, CORS, and middleware |
| `CodeSmith.CLI` | Interactive console client for the API |
| `CodeSmith.Tests` | xUnit test suite |

## Setup

### 1. Clone and build

```bash
git clone <repo-url>
cd CodeSmith
dotnet build CodeSmith.slnx
```

### 2. Configure the API key

Create or edit `CodeSmith.Api/appsettings.Development.json` (this file is gitignored):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Anthropic": {
    "ApiKey": "sk-ant-your-key-here"
  }
}
```

Alternatively, set the key via environment variable:

```bash
export Anthropic__ApiKey="sk-ant-your-key-here"
```

### 3. Run the API

The CLI expects the API on HTTPS (`https://localhost:7111`). Launch with the `https` profile:

```bash
dotnet run --project CodeSmith.Api --launch-profile https
```

The API starts on:
- HTTPS: `https://localhost:7111`
- HTTP: `http://localhost:5175`

Swagger UI is available in Development mode at `https://localhost:7111/swagger`.

### 4. Run the CLI

In a separate terminal (while the API is running):

```bash
dotnet run --project CodeSmith.CLI
```

Follow the prompts to select a difficulty, view the problem, and chat with the tutor. Type `exit` or press `Ctrl+C` to quit.

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/session` | Create a new problem session |
| `POST` | `/api/session/{sessionId}/chat` | Send a chat message in a session |

### Create a session

```bash
curl -X POST https://localhost:7111/api/session \
  -H "Content-Type: application/json" \
  -d '{"difficulty": "Easy"}'
```

Difficulty values: `Easy`, `Medium`, `Hard`.

### Chat within a session

```bash
curl -X POST https://localhost:7111/api/session/{sessionId}/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Can you give me a hint?"}'
```

## Running Tests

```bash
dotnet test CodeSmith.slnx
```

Run with verbose output:

```bash
dotnet test CodeSmith.slnx --verbosity normal
```

## Key Commands Reference

| Command | Purpose |
|---------|---------|
| `dotnet build CodeSmith.slnx` | Build the entire solution |
| `dotnet run --project CodeSmith.Api --launch-profile https` | Start the API server (HTTPS) |
| `dotnet run --project CodeSmith.CLI` | Start the CLI client |
| `dotnet test CodeSmith.slnx` | Run all tests |
| `dotnet test --filter "FullyQualifiedName~Core"` | Run only Core tests |
| `dotnet clean CodeSmith.slnx` | Clean all build outputs |
| `dotnet restore CodeSmith.slnx` | Restore NuGet packages |

## Rate Limiting

The API enforces a fixed window rate limit of **60 requests per minute per IP**. Exceeding this returns HTTP `429 Too Many Requests`.

## Security Notes

- Never commit API keys. `appsettings.Development.json` and `appsettings.*.json` are gitignored.
- The API does not expose stack traces in error responses.
- Request logging does not capture request or response bodies.
