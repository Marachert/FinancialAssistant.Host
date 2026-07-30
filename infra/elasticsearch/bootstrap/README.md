# Local Elasticsearch Bootstrap

Related Jira: FIN-60.

This folder contains the first executable local Elasticsearch bootstrap sample.
It creates one Identity Service-owned accounts index using the canonical naming
contract from `docs/engineering/elasticsearch-index-naming.md`.

The sample is local development infrastructure. It does not contain credentials,
production configuration, user identities, or financial data.

## Created Resources

| Resource | Name |
| --- | --- |
| Versioned index template | `fa-local-identity-accounts-template-v1` |
| Template pattern | `fa-local-identity-accounts-v1-*` |
| Initial physical index | `fa-local-identity-accounts-v1-000001` |
| Stable read alias | `fa-local-identity-accounts-read` |
| Stable write alias | `fa-local-identity-accounts-write` |

The write alias is marked `is_write_index: true`. Application code must use the
stable aliases rather than the physical index name.

## Prerequisites

- PowerShell 7 (`pwsh`)
- Docker with the repository local infrastructure running
- Elasticsearch available at `http://localhost:9200`

Start the local stack from the repository root:

```powershell
Set-Location infra/docker-compose
docker compose up -d elasticsearch
docker compose ps elasticsearch
Set-Location ../..
```

## Run

From the repository root:

```powershell
pwsh -NoProfile -NonInteractive -File infra/elasticsearch/bootstrap/bootstrap.ps1
```

For a different local endpoint:

```powershell
pwsh -NoProfile -NonInteractive -File infra/elasticsearch/bootstrap/bootstrap.ps1 `
  -ElasticsearchUrl http://localhost:19200
```

The script:

1. validates the endpoint and committed template pattern;
2. creates or replaces the versioned composable index template;
3. creates the initial physical index with both aliases when it is absent;
4. reasserts both aliases atomically when the index already exists;
5. reads the template and aliases back and fails unless the expected state is
   verified.

Running the same command repeatedly is safe. It reuses the fixed
`v1-000001` physical index and does not create additional generations. The
script intentionally does not perform migration or rollover; those operations
must follow the write-safe atomic cutover rules in the naming guide.

## Verify

The script returns a `Result = verified` object only after its read-back checks
pass. The same state can be inspected manually:

```powershell
curl.exe http://localhost:9200/_index_template/fa-local-identity-accounts-template-v1
curl.exe http://localhost:9200/fa-local-identity-accounts-v1-000001/_mapping
curl.exe http://localhost:9200/_alias/fa-local-identity-accounts-read
curl.exe http://localhost:9200/_alias/fa-local-identity-accounts-write
```

Expected results:

- the template pattern is `fa-local-identity-accounts-v1-*`;
- mappings use `dynamic: strict`;
- the physical index is `fa-local-identity-accounts-v1-000001`;
- both aliases target that physical index;
- the write alias has `is_write_index: true`.

A second bootstrap run must finish with the same four resource names and no
additional physical index.

## Ownership And Safety

Identity Service exclusively owns this namespace. Other services must not query
or write these resources directly. This sample contains mappings only; it never
loads documents. Use synthetic data for any local manual checks, and never log or
commit Elasticsearch documents, credentials, tokens, real identities, receipt
content, OCR output, LLM prompts/responses, or financial records.
