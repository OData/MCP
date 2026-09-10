# Microsoft OData for AI — Local MCP

A small app on your machine that lets your AI talk to any OData API you can reach — your CRM, your ERP, a public sample, Microsoft Graph.

It runs on **Windows, macOS, and Linux**. You install it, point it at a service, and start asking questions in your assistant. You do not need to know OData.

## What you can do

Once it is connected, ask things like:

- Tell me about customer ALFKI.
- When was the last time that account placed an order?
- Find the customers who have not created a new project in 45 days.
- Give me ten non-obvious insights about my account.
- Who are we still waiting on in Germany?

Your assistant looks the records up, filters them, follows related data, and can create or update a record when you ask it to. You stay in the chat. You do not export a spreadsheet first.

## Before OData for AI

- AI will connect to the remote service without instructions or guardrails, and start flopping around trying to use it. 
    - What THEY pay for: wasted connections, wasted bandwidth, wasted compute.
    - What YOU pay for: wasted tokens, wasted usage, wasted time.

### Nobody wins.

| Service Name | $metadata size | Tokens to read |
| ------------ | -------------- | -------------- |
| Northwind    | 19,406 bytes   | 4,802          |
| TripPin      | 7,297 bytes    | 1,859          |

## After OData for AI

- Your connection starts up, logs in, processes the remote service description, and builds a compact picture of the service.
- It provides your AI with specific tools and instructions to help maximize success while minimizing wasted tokens & bandwidth.
    - What THEY get: happier customers and smaller bills.
    - What YOU get: faster answer and smaller bills.

### EVERYBODY WINS!

| Service Name | Compact payload size | % size reduction | Tokens to read | % token reduction |
| ------------ | -------------------- | ---------------- | -------------- | ----------------- |
| Northwind    | 2,419 bytes          | 87.5%            | 654            | 86.4%             |
| TripPin      | 320 bytes            | 95.6%            | 91             | 95.1%             | 

## Get started

You need [.NET 8](https://dotnet.microsoft.com/download) or later on Windows, macOS, or Linux, and an assistant that can use MCP (Claude Code, Claude Desktop, Cursor, and others).

**1. Install**

```bash
dotnet tool install -g Microsoft.OData.Mcp.Tools
```

**2. Check the service**

```bash
dotnet odata-mcp try https://services.odata.org/TripPinRESTierService
```

You want a line that says `Verdict: ready`. If the service needs a sign-in, `try` tells you what to add and does not sign in behind your back.

**3. Add it to your assistant**

```bash
dotnet odata-mcp add
```

The wizard asks a few questions and prints the registration command, so a token never lands in your shell history.

Claude Desktop (edit the config it already uses for MCP servers):

```json
{
  "mcpServers": {
    "trippin": {
      "command": "dotnet",
      "args": ["odata-mcp", "start", "https://services.odata.org/TripPinRESTierService"]
    }
  }
}
```

**4. Ask**

Open a new chat and try: *Tell me about the people in this service. Who has trips coming up?*

That is the happy path. Most people stop here.

## If the service needs a sign-in

`try` prints the flags. Common cases:

| Situation | Add this |
|---|---|
| The service is public | nothing |
| You already have a bearer token | `--auth-token "<token>"` |
| The service uses OAuth (including Microsoft Graph) | `--client-id <your-app-id>` — you get a URL and a code the first time; after that it remembers |
| Unattended, no person to click | `--grant client_credentials --client-secret <secret>` |
| API key or HTTP Basic | `--api-key` / `--basic-user` as `try` suggests |

Example (Microsoft Graph):

```bash
dotnet odata-mcp start https://graph.microsoft.com/v1.0 \
  --client-id "{your-app-registration-id}" \
  --scopes "https://graph.microsoft.com/.default"
```

Sign-in prompts show as `Sign in at <url>` and `Code: <code>`. Tokens are stored on your machine and refreshed for you.

If you **own** the API, one line in that app (`AddProtectedResourceMetadata`) lets Local MCP discover how to sign in with no extra flags. That is documented with [Remote MCP](../Microsoft.OData.Mcp.AspNetCore/readme.md).

## Commands you will actually use

| Command | What it does |
|---|---|
| `dotnet odata-mcp try <url>` | Probe the service. Exit 0 means ready. |
| `dotnet odata-mcp add` | Wizard that registers the server with Claude Code. |
| `dotnet odata-mcp start <url>` | Run it. Your assistant starts this for you after registration. |

Pass the service root (the URL you would open in a browser). If you paste `/$metadata` on the end, it is trimmed for you.

## When something is off

Run `try` first. The verdict line is the answer.

| Verdict | What to do |
|---|---|
| `ready` | `dotnet odata-mcp start <url>` or `dotnet odata-mcp add` |
| `data requires sign-in` / `metadata requires sign-in` | Add the flags `try` printed, run `try` again |
| `unreachable` | Check the URL. Use the service root, not a single collection |
| `unreadable` | The URL answered, but not as OData this tool understands (login page, or an older OData version) |

`dotnet odata-mcp start … --verbose` prints each call your assistant makes, on stderr only.

## License

MIT.
