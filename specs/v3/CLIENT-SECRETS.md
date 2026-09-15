# Outbound client secrets

**Status:** Deferred  
**Revised:** 2026-09-15  
**Companion:** [AUTHENTICATION.md](./AUTHENTICATION.md)

---

## What is wrong

Outbound OAuth today binds a confidential-client secret (and an identity-assertion id token) from two process-wide names:

- `ODATA_MCP_CLIENT_SECRET`
- `ODATA_MCP_ID_TOKEN`

`odata-mcp add` refuses to write those into `.mcp.json` so the secret is not on disk next to the MCP host config. `start` reads the environment when `--client-secret` is unset.

That does not work for more than one MCP server on a machine. Claude Desktop, Cursor, and every other host launch each server as a child of the **same** process. They inherit the same environment. One name cannot hold Graph’s secret and SAP’s secret at once. Per-server `env` in MCP config would fix that and is exactly what AUTH-12 forbade.

Environment variables are also not a secret store. An MCP host, a tool that can shell out, and `ps` on `--client-secret` are three different leak channels; picking the first does not make the secret safe.

## What we will do

Replace the global env-var binder with a **per-service** secret source that a .NET operator already has: user secrets for local/dev, and later the OS store Latchkey already uses for tokens (or an equivalent). The lookup key is the OData service root plus client id, not a single process-wide name.

Until that feature lands, `FromEnvironment` and the two `ODATA_MCP_*` names stay in the product so existing one-daemon operators do not break.
