// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.OData.Mcp.Core.Routing
{
    /// <summary>
    /// High-performance route parser using ReadOnlySpan for zero-allocation parsing.
    /// </summary>
    /// <remarks>
    /// This parser handles MCP routes in the format: /{odataRoute}/mcp/{command}
    /// where {odataRoute} can be empty, and {command} is optional.
    /// </remarks>
    public ref struct SpanRouteParser
    {

        #region Constants

        internal const string McpSegment = "mcp";
        internal const char PathSeparator = '/';

        #endregion

        #region Fields

        internal readonly ReadOnlySpan<char> _path;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanRouteParser"/> struct.
        /// </summary>
        /// <param name="path">The path to parse.</param>
        public SpanRouteParser(ReadOnlySpan<char> path)
        {
            _path = path;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to parse an MCP route from the path.
        /// </summary>
        /// <param name="odataRoute">The OData route prefix, if found.</param>
        /// <param name="mcpCommand">The MCP command, if specified.</param>
        /// <returns>True if this is an MCP route; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryParseMcpRoute(out ReadOnlySpan<char> odataRoute, out ReadOnlySpan<char> mcpCommand)
        {
            odataRoute = default;
            mcpCommand = default;

            if (_path.IsEmpty)
            {
                return false;
            }

            // Trim leading slash if present
            var path = _path[0] == PathSeparator ? _path.Slice(1) : _path;

            // Find the "mcp" segment
            var mcpIndex = FindMcpSegment(path);
            if (mcpIndex < 0)
            {
                return false;
            }

            // Extract OData route (everything before /mcp)
            if (mcpIndex > 0)
            {
                // Remove trailing slash from OData route
                odataRoute = path.Slice(0, mcpIndex - 1);
            }
            else
            {
                // Empty OData route (root level MCP)
                odataRoute = ReadOnlySpan<char>.Empty;
            }

            // Extract MCP command (everything after /mcp/)
            var commandStart = mcpIndex + McpSegment.Length;
            if (commandStart < path.Length && path[commandStart] == PathSeparator)
            {
                commandStart++; // Skip the slash after "mcp"
                if (commandStart < path.Length)
                {
                    mcpCommand = path.Slice(commandStart);
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if the path is an MCP route without parsing details.
        /// </summary>
        /// <returns>True if this is an MCP route; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMcpRoute()
        {
            if (_path.IsEmpty)
            {
                return false;
            }

            var path = _path[0] == PathSeparator ? _path.Slice(1) : _path;
            return FindMcpSegment(path) >= 0;
        }

        /// <summary>
        /// Extracts just the OData route portion from an MCP path.
        /// </summary>
        /// <param name="odataRoute">The OData route prefix.</param>
        /// <returns>True if an OData route was found; otherwise, false.</returns>
        public bool TryGetODataRoute(out ReadOnlySpan<char> odataRoute)
        {
            odataRoute = default;

            if (_path.IsEmpty)
            {
                return false;
            }

            var path = _path[0] == PathSeparator ? _path.Slice(1) : _path;
            var mcpIndex = FindMcpSegment(path);

            if (mcpIndex < 0)
            {
                // Not an MCP route, but might still be an OData route
                return false;
            }

            if (mcpIndex > 0)
            {
                odataRoute = path.Slice(0, mcpIndex - 1);
                return true;
            }

            // Root level MCP
            odataRoute = ReadOnlySpan<char>.Empty;
            return true;
        }

        /// <summary>
        /// Gets the MCP command from the path.
        /// </summary>
        /// <param name="command">The MCP command.</param>
        /// <returns>True if a command was found; otherwise, false.</returns>
        public bool TryGetMcpCommand(out McpCommand command)
        {
            command = McpCommand.Unknown;

            if (!TryParseMcpRoute(out _, out var mcpCommand))
            {
                return false;
            }

            if (mcpCommand.IsEmpty)
            {
                command = McpCommand.Info; // Default command
                return true;
            }

            // Parse known commands
            if (mcpCommand.Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                command = McpCommand.Info;
                return true;
            }

            if (mcpCommand.Equals("tools", StringComparison.OrdinalIgnoreCase))
            {
                command = McpCommand.Tools;
                return true;
            }

            if (mcpCommand.StartsWith("tools/execute", StringComparison.OrdinalIgnoreCase))
            {
                command = McpCommand.ToolsExecute;
                return true;
            }

            // Check for specific tool info request: tools/{toolName}
            if (mcpCommand.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) &&
                mcpCommand.Length > 6)
            {
                command = McpCommand.ToolInfo;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts the tool name from a tool info request path.
        /// </summary>
        /// <param name="toolName">The tool name.</param>
        /// <returns>True if a tool name was found; otherwise, false.</returns>
        public bool TryGetToolName(out ReadOnlySpan<char> toolName)
        {
            toolName = default;

            if (!TryParseMcpRoute(out _, out var mcpCommand))
            {
                return false;
            }

            if (mcpCommand.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) &&
                mcpCommand.Length > 6)
            {
                toolName = mcpCommand.Slice(6);
                return !toolName.Contains(PathSeparator);
            }

            return false;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Finds the index of the "mcp" segment in the path.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <returns>The index of the "mcp" segment, or -1 if not found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FindMcpSegment(ReadOnlySpan<char> path)
        {
            var remaining = path;
            var offset = 0;

            while (!remaining.IsEmpty)
            {
                var nextSlash = remaining.IndexOf(PathSeparator);
                var segment = nextSlash >= 0 ? remaining.Slice(0, nextSlash) : remaining;

                if (segment.Equals(McpSegment, StringComparison.OrdinalIgnoreCase))
                {
                    return offset;
                }

                if (nextSlash < 0)
                {
                    break;
                }

                offset += nextSlash + 1;
                remaining = remaining.Slice(nextSlash + 1);
            }

            return -1;
        }

        #endregion

    }
}
