\# Purpose



The purpose of this codebase is to let AI can run advanced queries against OData services, by providing self-documenting MCP tools that are dynamically-generated against one or more OData services.



\## Execution



This is accomplished two ways:



1. By delivering a console application that leverages the C# MCP SDK (https://modelcontextprotocol.github.io/csharp-sdk) to dynamically generate MCP Tools for \*any\* OData API, including ones that are behind authentication.
2. By delivering an AspNetCore package over NuGet that takes the application it's installed in and magically reads the OData models registered and provides MCP HTTP endpoints for every EdmModel registered.



\## Architecture


* Microsoft.OData.Mcp.Core: Centralized library for all common functionality, including CSDL parsing, route management, and dynamic MCP tool generation.
* Microsoft.OData.Mcp.Tools: The implementation of Option 1 as a `dotnet tool` packaged as an McpServer that will be locally installed and run though either `dotnet tool install` or `dnx`.
* Microsoft.OData.Mcp.AspNetCore: The implementation of Option 2 as a magical set of Extension Methods that let you control how the MCP services are configured and respond to requests



\## Design Considerations



* This should be the most well-designed system the OData team has ever shipped.

* This should feel like 🪄 \*MAGIC\*.
* It should not drag the ENTIRE OData stack with it just to parse $metadata CSDL or forward calls to an OData endpoint.
* It should never replicate functionality that is in the base MCP SDK.
* It should not be more complicated than it ACTUALLY has to be. There is magic in code being stupid simple.



\## Performance Considerations



* This should be the fastest .NET library the OData team has ever shipped.
* It should use `Span` whenever we have to parse text.
* It should use `\[Regex] when it needs to use regular expressions.
* It should define different static `JsonSerializationOptions` in a `McpConstants` class so the same instance can be re-used everywhere.
* It should use other string constants wherever possible to minimize allocations.
* It should aggressively cache things like the parsed $metadata so operations are lightning fast.
* There should one or more `\*.Performance` projects to benchmark everything.





\## Testing Considerations



* This should be the most extensively tested system the OData team has ever shipped.
* Our North Star should be testing against the official OData deployed services: Northwind for read-only services, and TripPin for read-write services.
* It should use Breakdance.Assemblies to standardize how DI containers are constructed and used, as well as to guaranteed the public API surface doesn't break.
* It should use Breakdance.AspNetCore to create the TestServer and simplify how real integration tests are crafted to run real requests through an in-memory pipeline.
* It should ALWAYS test with real code and NEVER mock anything ever.
* Every method should have it's visibility set to `internal` or greater so classes can be extensively unit tested at every level.
* It should strive for > 96% code coverage. The closer to 100%, the better.



\## Implementation Considerations



* Never erase completed ToDo items from any AI-generated task lists.
* Double-check if something is the right thing to do before deleting something.
