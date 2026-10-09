# Native Search And Observability Qualification

FIN-272, metadata and local inspection updated 2026-10-09. These are candidates in the
[component manifest](component-manifest.json), not approved installation inputs.
All six entries remain `blocked`. Five ZIP payloads and the Grafana MSI were
downloaded and inspected without running their executables; no component was installed.
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
| Dashboards | Grafana OSS 13.2.2 Windows amd64 MSI; protected local dashboards backed by Prometheus | MSI tables declare Grafana/LocalSystem auto-start and bundled NSSM; safe ownership/identity, actual behavior, signing, provisioning and AGPL/notices remain pending |

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
not installed footprint or a disk-space threshold. The five inspected archives
matched those digests. This is not trusted-signature verification, legal clearance
or a vulnerability assessment. The Grafana MSI also matched its pinned digest.

## Passive Payload Inspection

The repository's [offline inspector](../../tools/scripts/inspect-native-package.ps1)
checked pinned digests, bounded ZIP directories and Windows path safety on
2026-10-09. It does not execute binaries or establish CRC/decompression integrity,
installed footprint, service behavior or supported-host acceptance.

| Exact archive | Compressed bytes | Entries / files | Declared expanded bytes | Notice candidates |
| --- | --- | --- | --- | --- |
| Prometheus 3.14.0 Windows amd64 | 110,095,743 | 5 / 5 | 261,754,839 | LICENSE and NOTICE |
| Alertmanager 0.34.1 Windows amd64 | 40,131,065 | 5 / 5 | 80,778,691 | LICENSE and NOTICE |
| Jaeger 2.21.0 Windows amd64 | 64,683,817 | 3 / 2 | 140,133,888 | None |
| Jaeger tools 2.21.0 Windows amd64 | 41,148,088 | 4 / 3 | 80,453,120 | None |
| Elasticsearch 8.19.21 Windows x86_64 | 525,338,532 | 1,582 / 1,380 | 862,252,161 | 261 candidates, including root LICENSE.txt / NOTICE.txt |

Passive PE inspection identified `Amd64` for `prometheus.exe`, `promtool.exe`,
`alertmanager.exe`, `amtool.exe`, `jaeger.exe`, `jaeger-es-rollover.exe` and
`jaeger-esmapping-generator.exe`. Windows Authenticode reported `NotSigned` for
all seven; individual SHA-256 values are retained in the manifest. This result
does not invalidate the published archive digests, but neither a digest nor a
detached archive signature automatically approves Windows application-control
trust. Never disable Smart App Control or other host security to install them.

Jaeger's archives lack bundled LICENSE/NOTICE files. Obtain and retain the
applicable exact-release terms and dependency notices before redistribution;
the source license alone is not a complete binary notice inventory. The bundled
`example-hotrod.exe` is a demonstration, not a production dependency. Exclude it
from the proposed payload. Also exclude `jaeger-es-index-cleaner.exe` from the
proposed payload unless a later owned-prefix retention design explicitly
qualifies it; the installer must never automatically run a broad deletion tool.
The sample Prometheus/Alertmanager YAML files are not deployment configuration.

The exact Jaeger [release SBOM](https://github.com/jaegertracing/jaeger/releases/download/v2.21.0/jaeger-SBOM.spdx.json)
was downloaded separately and its release SHA-256 verified (17,909,102 bytes,
SPDX-2.3, 10,193 packages and 141 file records). It contains matching SHA-256
records for the selected `jaeger.exe`, rollover and mapping-generator binaries,
using build-path names without the `.exe` suffix. All three records have
`NOASSERTION` file licenses. Release-wide package declarations include Apache-2.0,
BSD-3-Clause, ISC, MIT and NOASSERTION; they are evidence to reconcile with the
selected binaries, not a finished attribution bundle. The exact-tag
[Jaeger license](https://github.com/jaegertracing/jaeger/blob/v2.21.0/LICENSE)
is Apache-2.0. No large SBOM or downloaded binary is committed to this repository.

Elasticsearch's bundled `jdk/release` identifies Oracle OpenJDK 26.0.2,
runtime `26.0.2+10-55`, Windows x86_64. Its `java.exe` is Amd64, version 26.0.2.0,
and Authenticode reported `Valid` for Oracle America, Inc. The vendor's
`elasticsearch-service-x64.exe` and service manager are version 1.3.1.0 with
`Valid` Apache Software Foundation signatures; the manager is I386, whereas the
service executable is Amd64. These selected-file observations do not qualify
every JDK DLL, Elasticsearch module, native prerequisite or target OS. Preserve
root and module/JDK notices and revalidate trust at release/install time.

The Grafana MSI is 491,490,816 compressed bytes and Authenticode reported
`NotSigned`. Its matching vendor hash is not a substitute for application-control
trust. A passive [Windows Installer database read](https://learn.microsoft.com/en-us/windows/win32/msi/installer-opendatabase)
in mode 0 identified GrafanaOSS 13.2.2.0, manufacturer Grafana Labs, 13,359 file
rows, 1,380,406,797 declared file bytes and 129 notice-name candidates. The
`ServiceInstall` table declares service `Grafana`, type 16, start type 2 and
account `LocalSystem`; `ServiceControl` has event 163. This is declared metadata,
not a service installation or actual lifecycle test. The File table includes
`grafana.exe`, built-in datasource executables and `nssm.exe` (331,264 bytes);
the wrapper's version/signature/notices are not yet qualified. Do not install
these defaults blindly: reconcile existing `Grafana` service ownership, avoid
starting an unconfigured listener, establish a least-privilege identity and
verify data-preserving repair/uninstall before approving an unattended path.
MSI ProductCode/UpgradeCode and exact observations are retained in the manifest.
Declared file bytes do not include data growth, staging, filesystem overhead or
backup capacity. No MSI action was executed.

## Service And Network Boundary

[Grafana's Windows guide](https://grafana.com/docs/grafana/latest/setup-grafana/installation/windows/)
distinguishes the installer from standalone binaries and suggests NSSM for the
latter. The exact MSI's read-only tables now provide the evidence above; they do
not establish observed SCM behavior or make this wrapper globally qualified.
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
