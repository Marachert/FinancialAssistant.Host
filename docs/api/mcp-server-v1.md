# MCP Server v1

## Transport

The internal endpoint is stateless Streamable HTTP at `POST /mcp`, implemented
with `ModelContextProtocol.AspNetCore` 2.2.0. Legacy SSE is not enabled. The host
allowlist is loopback-only by default and deployment must explicitly add a known
internal host.

## Authentication

Every MCP request requires `X-Mcp-Authentication` and at least one allowlisted
`X-Mcp-Roles` value: `admin`, `operator`, `developer`, or `qa`. Secrets come from
the environment. `X-Correlation-Id` is optional, bounded to 128 safe characters,
and is never authorization.

| Tool | Admin | Operator | Developer | QA |
| --- | ---: | ---: | ---: | ---: |
| `system_health` | Yes | Yes | Yes | Yes |
| `ai_cost_summary` | Yes | Yes | No | No |
| `parsing_quality` | Yes | Yes | No | Yes |
| `prompt_eval_summary` | Yes | No | Yes | Yes |
| `jira_issue_draft` | Yes | No | Yes | No |
| `architecture_lookup` | Yes | Yes | Yes | Yes |

Unauthorized tools are excluded from MCP tool listings and rejected when called.
All tools are read-only. The Jira tool returns a draft requiring human/agent
submission and cannot mutate Atlassian.

## Data policy

Operational tools call allowlisted Monitoring and Audit APIs only. There is no
arbitrary Elasticsearch/database query, URL fetch, shell, filesystem, or raw
production-data tool. Responses contain aggregate operational counters, safe
status identifiers, or governed documentation references. Every request and
tool outcome is audited without raw personal or financial content.
