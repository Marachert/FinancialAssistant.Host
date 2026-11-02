# Native Windows HTTPS Perimeter

FIN-207 implementation contract, not installed-host acceptance. The POC uses
direct Kestrel TLS on the existing public gateway, not IIS/ARR, Docker or WSL.
The runtime opens no public listener by default. FIN-207 remains In Progress
until FIN-278 installer integration and approved-host evidence are complete.
This work does not increase the completed-ticket count or make first-user testing
ready. See [the local backlog](../agent/LOCAL_BACKLOG.md) and
[the progress ledger](../agent/POC_PROGRESS.md).

## Boundary

`WindowsNativeGatewayProfile` adds the perimeter only in `WindowsNative`.
`FinancialAssistantHost` retains the canonical IPv4 loopback gateway listener
from `NativeServiceCatalog` (currently port 5100). The separately selected HTTPS
listener accepts TLS 1.2/1.3 only. Internal/admin/MCP service ports are not exposed;
there is no HTTP redirect listener, wildcard certificate or development-certificate
fallback. Existing JWT, exact public authentication allowlist, admin authorization,
rate limits and trusted downstream-hop validation remain authoritative.

The TLS boundary runs before gateway correlation/dispatch. It requires the exact
configured HTTP Host and port, rejects unrecognized SNI during the handshake, and
rejects `Forwarded`, `X-Forwarded-*`, `X-Original-*` and `X-ARR-*` headers. This is a
direct endpoint, not a trusted reverse-proxy deployment. Automatic forwarded-header
environment switches are rejected; do not clear trusted-proxy lists or add a
forwarded-header middleware to bypass the boundary.

On HTTPS, `/`, `/health`, `/health/*`, `/gateway`, `/gateway/*`, `/metrics` and
`/metrics/*` return 404. Diagnostics and readiness remain loopback-only. Other
business/admin routes still need their normal authorization; a valid TLS handshake
confers no user or admin identity. HTTP on the internal listener requires a
loopback peer. The runtime never changes financial state or provider settings.

## Protected Configuration Contract

`Gateway:NativeHttps` is strictly bound; unknown keys, malformed values and
conflicting service/technical ports fail without echoing input or inner secrets.
FIN-208 owns protected configuration delivery; this synthetic example is a schema
illustration, not production configuration or a valid certificate/identity:

```json
{
  "Gateway": {
    "NativeHttps": {
      "Enabled": true,
      "PublicOrigin": "https://poc.example.invalid:7443",
      "BindAddress": "0.0.0.0",
      "PublicExposureApproved": true,
      "CertificateSha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
      "ServiceSid": "S-1-5-80-1-2-3-4-5",
      "MaximumRequestBodyBytes": 12582912,
      "MaximumRequestHeadersBytes": 32768,
      "RequestHeadersTimeoutSeconds": 15,
      "MinimumCertificateDays": 7
    }
  }
}
```

`PublicOrigin` is a single HTTPS DNS/IPv4 origin, without credentials, path, query,
fragment or trailing DNS dot. The bind address is IPv4. Non-loopback binding needs
explicit exposure approval and a non-loopback origin. The approval flag is not
owner consent by itself: FIN-278 must obtain and record consent and verify actual
firewall/network state before enabling it. IPv6 is not supported in this baseline.
No DNS/certificate enrollment or paid service is invoked.

Body size is 64 KiB through 12 MiB, total headers 8-32 KiB, header timeout 5-30
seconds and minimum remaining certificate lifetime 1-90 days. Defaults are shown
above. Request-line limit is 8 KiB, keep-alive 60 seconds, handshake 10 seconds,
concurrent connections 128, and upgraded connections disabled. Successful public
requests carry seven-day HSTS, without preload or includeSubDomains.
Known-oversized HTTP/1 uploads return 413 with `Connection: close`, so clients do
not reuse a connection whose unread body cannot be drained. Body limits also
apply while reading chunked requests; valid uploads at the limit remain accepted.
The dispatcher and exception boundary preserve direct or wrapped local body-limit
exceptions as a sanitized `413 request_body_too_large`, not destination503 or
internal500. HTTP/2 never receives the HTTP/1-only Connection header. If a response
has already started, the connection is aborted rather than appending an error body.

## Certificate And Private Key

The production source reads an existing certificate from `LocalMachine/My` by
exact SHA-256 fingerprint. It does not import, enroll, purchase, issue, repair,
export or trust a certificate. It must run as the actual virtual account
`NT SERVICE\FinancialAssistant.gateway`; the configured canonical service SID
must match both that translated account and the current process identity.

The certificate must have one explicit non-CA basic-constraints extension,
digital-signature usage without CA signing, explicit server-auth EKU, an exact
DNS/IP SAN (no CN or wildcard fallback), a non-self-issued leaf, approved SHA-2
RSA/ECDSA signature and RSA >=2048 or ECDSA >=256 bits. Validity and the configured
renewal window are checked at startup. The machine trust chain uses offline
revocation checking, excludes the root from revocation and disables certificate
downloads. A missing chain, intermediate or usable cached revocation evidence is
a failure, not permission to disable trust/revocation checks or fetch/buy anything.
Approved-host qualification must verify these conditions with the actual issuer.

Read-only `CERT_KEY_PROV_INFO` metadata is checked before opening a private key.
Only the Microsoft Software Key Storage Provider or the Microsoft Enhanced RSA
and AES Cryptographic Provider, with machine-key metadata and no custom provider
parameters, is allowed. Remote, hardware and other providers are rejected before
activation. The opened key must be local, non-exportable, non-removable and not
UI-protected. The canonical ProgramData key path must have no reparse-point
ancestors, a protected DACL owned by SYSTEM or Administrators, and no other allow
grants except SYSTEM/Administrators and read-only access for the intended gateway
service SID. The source changes neither ACLs nor certificate stores.

FIN-278/208 must provision the approved certificate, chain/revocation material,
non-exportable key and least-privilege ACLs through their separately reviewed
consent/ownership flow. No real certificates, keys, passwords or store exports
belong in Git, CLI arguments, support bundles or evidence.

## Renewal And Readiness

Renewal is an explicit validated replacement plus a controlled gateway restart,
not automatic enrollment or hot reload. Before switching, validate the new
certificate, private key, service access, hostname, chain and renewal window;
update the protected SHA-256 reference transactionally and retain the prior valid
reference for an installation-owned recovery decision. The installer must verify
the restarted listener with real client trust. Failed validation must not open an
unapproved listener or silently select another certificate.

The cached certificate is checked against current time for each handshake and
public request. An expired certificate is never selected; an already connected
request fails closed after expiry. The loopback `native_https` readiness check
becomes unhealthy when the renewal window is reached, even before expiry. Missing
or disabled HTTPS is also unhealthy in the native profile, although the loopback
diagnostic process can start. Existing downstream readiness checks still apply.
Restarts, active-session behavior and recovery remain approved-host tests.

## Installer Handoff

`NativeHttpsPlan.Firewall` is a non-executing plan: one owned TCP rule,
`FinancialAssistant.Gateway.Https`, selected public port, Domain/Private profiles,
LocalSubnet clients only. It does not call Windows Firewall. FIN-278 must inspect
existing exposure/conflicts, bind the rule to the owned executable/service and
appropriate interface, obtain consent, apply and independently verify effective
policy, and roll back only owned changes. Do not open internal ports, management
endpoints, Public profile or unrestricted remote access. Validate foreign rules
and reject unexpected exposure rather than taking ownership of them.

The installer must also integrate protected configuration, actual SCM identity,
certificate import/renewal/binding/ACL verification, port collisions and bounded
recovery. A valid plan or current-user console test is not installed acceptance.

## Free Verification And Pending Evidence

The hosting tests cover immutable/strict configuration, explicit exposure,
reserved ports, SID shape, request bounds, provider-metadata allow/reject rules,
and synthetic Windows ACL descriptors plus a current-user file rejection.
Descriptor-positive tests do not certify a real machine service key. Windows-only
ACL cases execute only on Windows; non-Windows CI cannot establish that evidence.

The gateway tests use actual Kestrel TCP/TLS on owned IPv4 loopback ports,
synthetic issued certificates and process-local custom-root trust. They verify
TLS requests, HSTS, forged forwarding metadata, independent HTTP Host rejection,
private diagnostics, body limits, untrusted root/unknown SNI failures and restart
renewal to a different valid leaf. Client trust checks are not bypassed. Synthetic
proxy coverage uses the production dispatcher and exception middleware against an
owned loopback receiver: an oversized chunked upload must return413 without a
completed downstream write, then an exact-limit valid upload must succeed.
Synthetic client revocation is disabled only in this fixture; production uses offline
revocation. Schannel needs a temporary persisted fixture key: an in-memory PFX
default import removes that key when disposed, never changes a certificate/trust
store, and clears exported bytes. No fixture is a release certificate.

```powershell
dotnet test backend/shared/hosting/FinancialAssistant.Shared.Hosting.Tests/FinancialAssistant.Shared.Hosting.Tests.csproj --configuration Release
dotnet test backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway.Tests/FinancialAssistant.PublicApiGateway.Tests.csproj --configuration Release
```

Required later evidence on approved Windows 11 / Server 2022 / Server 2025 hosts:
actual service SID/store/chain/revocation/key ACLs; missing/expired/wrong-host/
wrong-provider/extra-grant negatives; real firewall and internal-port isolation;
JWT/admin/access-negative requests through the installed HTTPS gateway; renewal,
restart, recovery and old-reference rollback; and FIN-278 integration. No services,
vendor packages, firewall rules or OS trust changes are performed on the owner's
working PC. Paid providers, certificate purchases and paid test hosts stay disabled.

Platform references: [Kestrel endpoints](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-8.0),
[hostname validation](https://learn.microsoft.com/dotnet/api/system.security.cryptography.x509certificates.x509certificate2.matcheshostname),
[certificate provider metadata](https://learn.microsoft.com/en-us/windows/win32/api/wincrypt/nf-wincrypt-certgetcertificatecontextproperty).
