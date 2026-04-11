# CodeSmith

An AI-powered coding interview practice tool. Users pick a programming language and difficulty, receive a tailored coding problem with starter code in a split-screen editor, and get guided assistance from an AI pair programmer powered by the Anthropic Claude API. The AI always has access to the current contents of the code editor, so it can reference and reason about the users's actual code.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) (for the React frontend)
- An [Anthropic API key](https://console.anthropic.com/)

## Solution Structure

| Project | Description |
|---------|-------------|
| `CodeSmith.Core` | Shared models, enums, interfaces, and custom exceptions |
| `CodeSmith.Infrastructure` | Anthropic SDK integration and in-memory session storage |
| `CodeSmith.Api` | ASP.NET Core Web API with rate limiting, CORS, and middleware |
| `CodeSmith.CLI` | Interactive console client for the API |
| `CodeSmith.Web` | React 19 frontend (Vite, TypeScript, Tailwind CSS v4, TanStack Query v5) |
| `CodeSmith.Tests` | xUnit + NSubstitute backend test suite |

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

### 4. Run the Web Frontend

In a separate terminal, install dependencies and start the Vite dev server:

```bash
cd CodeSmith.Web
npm install
npm run dev
```

The frontend starts on `https://localhost:5173` and automatically proxies `/api/*` requests to the backend at `https://localhost:7111`.

Open `https://localhost:5173` in your browser, pick a language and difficulty, and start coding with the AI pair programmer.

### 5. Run the CLI

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
  -d '{"difficulty": "Easy", "language": "CSharp"}'
```

Difficulty values: `Easy`, `Medium`, `Hard`.

Language values: `CSharp`, `Cpp`, `Go`, `Rust`, `Python`, `Java`, `TypeScript`.

### Chat within a session

```bash
curl -X POST https://localhost:7111/api/session/{sessionId}/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Can you give me a hint?", "editorContent": "public int Add(int a, int b) { return 0; }"}'
```

`editorContent` is optional. When provided, the AI uses it as context to reference the student's current code in the editor. When omitted, the AI only sees the original starter code.

## Running Tests

### All backend tests

```bash
dotnet test CodeSmith.slnx
```

Run with verbose output:

```bash
dotnet test CodeSmith.slnx --verbosity normal
```

### Frontend unit tests (Vitest)

```bash
cd CodeSmith.Web
npm test            # single run
npm run test:watch  # watch mode
```

### Playwright E2E tests

```bash
cd CodeSmith.Web
npx playwright test
```

### Browser E2E testing

Run both servers simultaneously in separate terminals, then open the app in your browser:

**Terminal 1 — API:**
```bash
dotnet run --project CodeSmith.Api --launch-profile https
```

**Terminal 2 — Frontend:**
```bash
cd CodeSmith.Web
npm run dev
```

Navigate to `https://localhost:5173`. Accept the self-signed certificate warning on first visit, then select a difficulty and start testing.

## Key Commands Reference

| Command | Purpose |
|---------|---------|
| `dotnet build CodeSmith.slnx` | Build the entire solution |
| `dotnet run --project CodeSmith.Api --launch-profile https` | Start the API server (HTTPS) |
| `dotnet run --project CodeSmith.CLI` | Start the CLI client |
| `cd CodeSmith.Web && npm run dev` | Start the Vite frontend dev server |
| `dotnet test CodeSmith.slnx` | Run all backend tests |
| `dotnet test --filter "FullyQualifiedName~Core"` | Run only Core tests |
| `cd CodeSmith.Web && npm test` | Run frontend unit tests (Vitest) |
| `dotnet clean CodeSmith.slnx` | Clean all build outputs |
| `dotnet restore CodeSmith.slnx` | Restore NuGet packages |

## Rate Limiting

The API enforces a fixed window rate limit of **60 requests per minute per IP**. Exceeding this returns HTTP `429 Too Many Requests`.

## Security Notes

- Never commit API keys. `appsettings.Development.json` and `appsettings.*.json` are gitignored.
- The API does not expose stack traces in error responses.
- Request logging does not capture request or response bodies.
