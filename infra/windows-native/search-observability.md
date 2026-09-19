# Native Search And Observability Qualification

FIN-272, metadata checked 2026-09-18. These are candidates in the
[component manifest](component-manifest.json), not approved installation inputs.
All six entries remain `blocked`; no payload was downloaded or installed.
The [observability contract](../../docs/architecture/backend-observability-strategy.md)
and [storage policy](../../docs/architecture/storage-policy.md) remain authoritative.

## Candidate Paths

| Capability | Native candidate and intended path | Qualification still required |
| --- | --- | --- |
| Retained search and operational projections | Elasticsearch 8.19.21 Windows x64 ZIP; use its bundled JDK and vendor service launcher | Bundled JDK/launcher versions, signatures/notices, OS matrix, exact retained-contract API/index compatibility |
| Metrics | Prometheus 3.14.0 Windows amd64 ZIP; scrape allowlisted application metrics into owned local TSDB | Native service wrapper, scrape/exporter implementation, bounded retention and series cardinality, restart tests |
| Alert delivery | Alertmanager 0.34.1 Windows amd64 ZIP; Prometheus rules to local protected receiver | Service wrapper, authenticated local receiver adapter, grouping/retry/silence persistence; no external notifications enabled |
| Traces | Jaeger 2.21.0 Windows amd64 ZIP; application OTLP to Jaeger to dedicated Elasticsearch trace indices | Service wrapper, explicit config and health extension, privacy filtering/sampling, retention and authenticated queries |
| Trace index maintenance | Jaeger tools 2.21.0 Windows amd64 ZIP; one-shot owned-prefix rollover initialization and ILM policy | Exact tools in archive, least-privilege bootstrap role and idempotence; never broad index deletion |
| Dashboards | Grafana OSS 13.2.2 Windows amd64 MSI; protected local dashboards backed by Prometheus | MSI properties/service behavior, signing, provisioning, least privilege and AGPL/notices review |

This is a proposed topology, not deployed behavior. Application metrics exporters,
trace exporters and the local alert receiver still belong to FIN-277. The existing
Monitoring API is not assumed to accept Alertmanager webhooks. Persistent audit
events remain in their owning durable store, separate from disposable diagnostics.
Local bounded JSON logs remain valid; adding these candidates does not silently
introduce Loki, Tempo, Kibana, Alloy, a cloud account or a second telemetry store.

[Jaeger 2.21 documents Elasticsearch 7.x/8.x compatibility](https://www.jaegertracing.io/docs/2.21/storage/elasticsearch/).
Therefore 8.19.21 is the candidate; do not silently substitute Elasticsearch 9.x.
Use explicit persistent storage, not Jaeger's in-memory demonstration defaults.
Its documented ILM path needs initialization tooling, so the separate tools ZIP
is inventoried too. Exact mappings and read-model contracts still need FIN-205.

## Sources And Integrity

The JSON stores complete digests and exact payload URLs. Evidence sources:

- [Elasticsearch Windows guide](https://www.elastic.co/guide/en/elasticsearch/reference/8.19/zip-windows.html)
  and the exact ZIP's vendor-published SHA-512 sidecar.
- [Prometheus v3.14.0 release metadata](https://api.github.com/repos/prometheus/prometheus/releases/tags/v3.14.0).
- [Alertmanager v0.34.1 release metadata](https://api.github.com/repos/prometheus/alertmanager/releases/tags/v0.34.1).
- [Jaeger v2.21.0 release metadata](https://api.github.com/repos/jaegertracing/jaeger/releases/tags/v2.21.0),
  with distinct ZIP digests for Jaeger and tools; detached signatures are published.
- [Grafana OSS 13.2.2 Windows download](https://grafana.com/grafana/download/13.2.2?edition=oss&platform=windows),
  build 34846740809 MSI. Do not substitute Enterprise or a different build.

All GitHub candidates above were non-prerelease at retrieval. Published asset
sizes for Prometheus, Alertmanager, Jaeger and tools are compressed download bytes,
not installed footprint or a disk-space threshold. Source hashes are not locally
verified payload hashes, trusted signatures or a vulnerability assessment.

## Service And Network Boundary

[Grafana's Windows guide](https://grafana.com/docs/grafana/latest/setup-grafana/installation/windows/)
distinguishes the installer from standalone binaries and suggests NSSM for the
latter. Do not infer the selected MSI's SCM behavior from those standalone steps.
[WinSW](https://github.com/winsw/winsw) is another wrapper candidate, not yet a
dependency: latest stable metadata returned 2.12.0 with no asset digest. Neither
an old stable label nor a newer prerelease establishes a qualified support path.
No wrapper, compiler or SDK is installed by this research.

Reserve loopback ports 9200/9300 for Elasticsearch, 9090 for Prometheus, 9093 for
single-node Alertmanager, 3000 for Grafana and 4317/4318/16686/13133 for the selected
Jaeger OTLP/query/health configuration. These are proposed assignments, not a
claim that all defaults are loopback. Explicitly disable unused receivers,
debug listeners and Alertmanager clustering; otherwise inventory additional ports.
Grafana and trace queries require operator authentication. No default credentials,
anonymous UI, external exporter, plugin download or trial activation is permitted.

## Support And Acceptance Matrix

| Target | Evidence available | Release status |
| --- | --- | --- |
| Windows 11 x64 | Native x64 artifacts exist; exact edition/build and each vendor's compatibility still need confirmation | Not tested; blocked |
| Windows Server 2022 Desktop Experience x64 | Same artifacts; service identities, filesystem/ACL and reboot behavior unverified | Not tested; blocked |
| Windows Server 2025 Desktop Experience x64 | Same artifacts; do not inherit acceptance from Server 2022 | Not tested; blocked |

[Elastic's support matrix](https://www.elastic.co/support/matrix) describes vendor
subscription eligibility, not a purchased support entitlement or our host tests.
[Elastic License 2.0](https://www.elastic.co/licensing/elastic-license) and Grafana
OSS AGPL terms require distribution/notices review for the actual payloads.
No legal clearance or commercial entitlement is inferred from free downloads.

Before qualification, record exact vendor support boundaries, all bundled
licenses, wrapper trust, install/detect/repair/remove operations, exit/reboot
behavior and measured resource needs. On authorized disposable hosts verify
synthetic scrape/query, alert delivery, trace persistence, bounded retention,
service/host restart and data-preserving maintenance. Unknown results remain
blocked; local schema validation cannot convert them into acceptance.
