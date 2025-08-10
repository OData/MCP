# Future CLI Plans for OData MCP Tools

## Interactive MCP Configuration Command (Future Enhancement)

### Overview
Create a new `configure` command that interactively asks users about their OData connection and generates the appropriate MCP configuration for their AI tool of choice (Claude Desktop, Claude Code, VS Code Copilot, Cursor, etc.).

### Command Structure
**Command**: `odata-mcp configure`

### Implementation Plan

#### 1. Create ConfigureCommand Class
**File**: `src/Microsoft.OData.Mcp.Tools/Commands/ConfigureCommand.cs`

Features:
- Interactive command-line prompts using McMaster.Extensions.CommandLineUtils
- Support for both interactive and non-interactive modes (with flags)
- Validation of inputs (URL, authentication, etc.)

#### 2. Interactive Prompts Flow

The command will ask:
1. **OData Service URL**: 
   - Prompt: "Enter your OData service URL:"
   - Validation: Must be a valid HTTP/HTTPS URL
   - Examples provided: Northwind, TripPin, custom

2. **Authentication Type**:
   - None
   - Bearer Token
   - API Key
   - Basic Auth
   - Client Certificate
   
3. **Authentication Credentials** (based on type):
   - Token/Key input (masked for security)
   - Username/Password for Basic Auth
   - Certificate path for Client Cert

4. **Target AI Tool**:
   - Claude Desktop
   - Claude Code (CLI)
   - VS Code with GitHub Copilot
   - Cursor IDE
   - All (generate configs for all supported tools)

5. **Advanced Options** (optional):
   - Verbose logging (yes/no)
   - Custom tool name/alias
   - Environment variables prefix

#### 3. Configuration Generation

Based on the target tool, generate:

**Claude Desktop** (`claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "odata-mcp": {
      "command": "npx",
      "args": [
        "-y",
        "odata-mcp",
        "start",
        "<service-url>"
      ],
      "env": {
        "ODATA_AUTH_TOKEN": "<token-if-needed>"
      }
    }
  }
}
```

**Claude Code** (`.mcp.json` in project root):
```json
{
  "mcpServers": {
    "odata-mcp": {
      "command": "odata-mcp",
      "args": ["start", "<service-url>"],
      "env": {
        "ODATA_AUTH_TOKEN": "<token-if-needed>"
      }
    }
  }
}
```

**VS Code/Cursor** (`.vscode/mcp.json`):
```json
{
  "mcp.servers": [
    {
      "name": "odata-mcp",
      "command": "odata-mcp",
      "args": ["start", "<service-url>"],
      "env": {
        "ODATA_AUTH_TOKEN": "<token-if-needed>"
      }
    }
  ]
}
```

#### 4. Output Options

After generating configuration:
1. **Display the configuration** in the console
2. **Save to file** options:
   - Auto-detect and save to appropriate location
   - Save to custom path
   - Copy to clipboard (if supported)
3. **Show installation instructions** for the target tool
4. **Test connection** option to verify the configuration works

#### 5. Non-Interactive Mode

Support command-line flags for automation:
```bash
odata-mcp configure \
  --url "https://services.odata.org/V4/Northwind/Northwind.svc" \
  --auth-type bearer \
  --auth-token "your-token" \
  --tool claude-desktop \
  --output ~/claude_config.json
```

#### 6. Files to Create/Modify

1. **Create**: `src/Microsoft.OData.Mcp.Tools/Commands/ConfigureCommand.cs`
   - Main command implementation
   - Interactive prompts logic
   - Configuration generation

2. **Create**: `src/Microsoft.OData.Mcp.Tools/Configuration/McpConfigGenerator.cs`
   - Configuration template generation
   - Tool-specific formatting
   - Path resolution for different platforms

3. **Create**: `src/Microsoft.OData.Mcp.Tools/Configuration/ToolConfigurations.cs`
   - Configuration templates for each AI tool
   - Platform-specific paths (Windows/macOS/Linux)

4. **Update**: `src/Microsoft.OData.Mcp.Tools/Commands/ODataMcpRootCommand.cs`
   - Add ConfigureCommand as a subcommand

#### 7. Features & Benefits

- **User-Friendly**: No manual JSON editing required
- **Multi-Tool Support**: Generate configs for any supported AI tool
- **Secure**: Masks sensitive inputs, supports environment variables
- **Validated**: Tests connection before saving
- **Cross-Platform**: Works on Windows, macOS, Linux
- **Discoverable**: Shows examples and common services

#### 8. Example Usage

```bash
# Interactive mode
$ odata-mcp configure

Welcome to OData MCP Configuration Setup!
==========================================

? Enter your OData service URL: https://services.odata.org/V4/Northwind/Northwind.svc
? Select authentication type: None
? Select your AI tool: Claude Desktop
? Enable verbose logging? No

✓ Configuration generated successfully!

Location: C:\Users\YourName\AppData\Roaming\Claude\claude_desktop_config.json

Next steps:
1. Restart Claude Desktop
2. Look for "odata-mcp" in available tools
3. Start querying your OData service!

? Test connection now? (Y/n) Y
✓ Connection successful! Found 26 entity sets.
```

### Technical Challenges

#### 1. JSON Merging
- **Challenge**: Need to parse existing configuration files and merge new servers without overwriting existing ones
- **Solution**: Use System.Text.Json to:
  - Load existing configuration
  - Check for duplicate server names
  - Merge configurations intelligently
  - Preserve user's existing settings

#### 2. Platform-Specific Paths
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Linux**: `~/.config/Claude/claude_desktop_config.json`

#### 3. Authentication Security
- **Environment Variables**: Store sensitive data in environment variables
- **Secure Input**: Use masked input for passwords/tokens
- **Config Encryption**: Consider supporting encrypted configuration storage

#### 4. Tool Detection
- Detect which AI tools are installed
- Validate tool installations
- Provide installation links if tools are missing

### Success Criteria
- Interactive prompts work smoothly
- Generated configurations are valid JSON
- Configurations work with target AI tools
- Non-interactive mode supports automation
- Clear instructions and error messages
- Existing configurations are preserved when merging

### Priority: FUTURE
This is a future enhancement that will make the tool more user-friendly but requires careful implementation to handle JSON merging, platform differences, and security considerations properly.