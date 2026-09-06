const healthStates = new Set(['healthy', 'degraded', 'unavailable', 'not_configured']);
const categories = new Set([null, 'timeout', 'transport', 'http_status', 'invalid_response', 'not_configured', 'provider_unavailable', 'invalid_result', 'policy_rejected']);
const safeKey = value => typeof value === 'string' && /^[a-z][a-z0-9-]{0,63}$/.test(value);
const count = value => Number.isSafeInteger(value) && value >= 0;
const date = value => typeof value === 'string' && Number.isFinite(Date.parse(value));
const requireShape = condition => {
  if (!condition) throw new Error('invalid_response');
};
export function validateDashboard(data) {
  requireShape(data && data.dataClassification === 'aggregate-operational-only' && date(data.generatedAtUtc) && healthStates.has(data.overallStatus) && Array.isArray(data.services) && data.services.length <= 100);
  for (const service of data.services) requireShape(safeKey(service.service) && healthStates.has(service.status) && count(service.latencyMilliseconds) && date(service.checkedAtUtc) && categories.has(service.errorCategory));
  const {
    readiness,
    rabbitMq,
    elasticsearch,
    metrics
  } = data;
  requireShape(readiness && ['componentCount', 'healthyCount', 'degradedCount', 'unavailableCount', 'notConfiguredCount'].every(key => count(readiness[key])));
  requireShape(rabbitMq && healthStates.has(rabbitMq.status) && count(rabbitMq.queueDepth) && count(rabbitMq.consumerCount));
  requireShape(elasticsearch && healthStates.has(elasticsearch.status) && ['green', 'yellow', 'red', 'unknown'].includes(elasticsearch.clusterStatus) && count(elasticsearch.nodeCount) && count(elasticsearch.activeShardCount));
  requireShape(metrics?.aiUsage && ['requestCount', 'successfulRequestCount', 'inputTokenCount', 'outputTokenCount', 'estimatedCostMicros'].every(key => count(metrics.aiUsage[key])));
  requireShape(metrics?.parsingQuality && ['processedCount', 'successfulCount', 'reviewRequiredCount', 'failedCount'].every(key => count(metrics.parsingQuality[key])) && typeof metrics.parsingQuality.successPercent === 'number' && metrics.parsingQuality.successPercent >= 0 && metrics.parsingQuality.successPercent <= 100);
  return data;
}
export function validateJobs(data) {
  requireShape(data && data.dataClassification === 'bounded-operational-metadata-only' && date(data.generatedAtUtc) && Array.isArray(data.jobs) && data.jobs.length <= 200 && data.capacity === 200 && data.retentionHours === 24);
  for (const job of data.jobs) requireShape(typeof job.operationId === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(job.operationId) && safeKey(job.service) && ['ai', 'ocr', 'notification'].includes(job.kind) && ['queued', 'running', 'succeeded', 'failed'].includes(job.state) && count(job.revision) && date(job.observedAtUtc) && categories.has(job.errorCategory));
  return data;
}
export function createMonitoringClient(fetcher = fetch) {
  let session = null;
  const client = {
    clientInstanceId: crypto.randomUUID(),
    platform: 'web',
    appVersion: '0.1.0'
  };
  async function request(path, options = {}) {
    const response = await fetcher('/gateway' + path, {
      cache: 'no-store',
      credentials: 'omit',
      redirect: 'error',
      signal: AbortSignal.timeout(15000),
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...(session ? {
          Authorization: 'Bearer ' + session.accessToken
        } : {})
      }
    });
    if (!response.ok) {
      if ([401, 403].includes(response.status)) session = null;
      throw new Error(response.status === 401 ? 'sign_in_required' : response.status === 403 ? 'admin_required' : response.status === 429 ? 'rate_limited' : 'service_unavailable');
    }
    return response.status === 204 ? null : response.json();
  }
  return {
    async signIn(email, password) {
      session = null;
      const result = await request('/auth/v1/sign-in', {
        method: 'POST',
        body: JSON.stringify({
          email,
          password,
          client
        })
      });
      requireShape(result?.tokenType?.toLowerCase() === 'bearer' && typeof result.accessToken === 'string' && result.accessToken.length >= 32 && result.accessToken.length <= 4096 && !/[\r\n]/.test(result.accessToken) && typeof result.refreshToken === 'string' && date(result.accessTokenExpiresAtUtc));
      session = {
        accessToken: result.accessToken,
        refreshToken: result.refreshToken,
        expires: Date.parse(result.accessTokenExpiresAtUtc)
      };
    },
    async load() {
      if (!session || session.expires <= Date.now()) {
        session = null;
        throw new Error('sign_in_required');
      }
      const dashboard = validateDashboard(await request('/admin/monitoring'));
      const jobs = validateJobs(await request('/admin/monitoring/jobs'));
      return {
        dashboard,
        jobs
      };
    },
    async signOut() {
      const previous = session;
      session = null;
      if (previous) {
        try {
          await fetcher('/gateway/auth/v1/logout', {
            method: 'POST',
            credentials: 'omit',
            cache: 'no-store',
            redirect: 'error',
            signal: AbortSignal.timeout(10000),
            headers: {
              'Content-Type': 'application/json',
              Authorization: 'Bearer ' + previous.accessToken
            },
            body: JSON.stringify({
              refreshToken: previous.refreshToken,
              client
            })
          });
        } catch {/* Local session is cleared even when revocation is unavailable. */}
      }
    }
  };
}
