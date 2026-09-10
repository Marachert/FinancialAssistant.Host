import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { createMonitoringClient } from '../api/client.mjs';
const api = createMonitoringClient();
const messages = {
  sign_in_required: 'Sign in to continue.',
  admin_required: 'Administrator access is required.',
  rate_limited: 'Too many requests. Try again shortly.',
  invalid_response: 'Operational data could not be verified.',
  service_unavailable: 'Monitoring is temporarily unavailable.'
};
const number = value => new Intl.NumberFormat('en-US').format(value);
const time = value => new Date(value).toLocaleString('en-GB', {
  hour12: false
});
const labels = {
  not_configured: 'Not configured',
  healthy: 'Healthy',
  degraded: 'Degraded',
  unavailable: 'Unavailable',
  queued: 'Queued',
  running: 'Running',
  succeeded: 'Succeeded',
  failed: 'Failed'
};
const tabs = [['overview', 'Activity', 'Overview'], ['services', 'Server', 'Services'], ['jobs', 'ListChecks', 'Processing jobs'], ['usage', 'ChartNoAxesCombined', 'AI & OCR'], ['support', 'LifeBuoy', 'Support']];
function Icon({
  name
}) {
  return <img className="icon" src={'/icons/' + name + '.svg'} alt="" width="20" height="20" />;
}
function Status({
  value
}) {
  return <span className={'status ' + value}><i />{labels[value] || 'Unknown'}</span>;
}
function Metric({
  title,
  value,
  note
}) {
  return <div className="metric"><span>{title}</span><strong>{value}</strong><small>{note}</small></div>;
}
function Services({
  dashboard,
  compact = false
}) {
  const services = dashboard.services;
  return <section className="section"><div className="section-heading"><h2>Service readiness</h2><span>{services.length} services</span></div>
    {services.length === 0 ? <p className="empty">No service observations available.</p> : <div className="table-scroll"><table>
      <thead><tr><th>Service</th><th>Status</th><th>Latency</th>{!compact && <><th>Last checked</th><th>Category</th></>}</tr></thead>
      <tbody>{services.map(s => <tr key={s.service}><td className="service-name"><Icon name="Server" />{s.service}</td>
        <td><Status value={s.status} /></td><td className="numeric">{number(s.latencyMilliseconds)} ms</td>
        {!compact && <><td>{time(s.checkedAtUtc)}</td><td>{s.errorCategory || 'None'}</td></>}</tr>)}</tbody>
    </table></div>}</section>;
}
function Jobs({
  jobs,
  compact = false
}) {
  const [filter, setFilter] = useState('all');
  const [query, setQuery] = useState('');
  const rows = jobs.jobs.filter(job => (filter === 'all' || job.state === filter) && (job.operationId + ' ' + job.service).toLowerCase().includes(query.toLowerCase()));
  return <section className="section"><div className="section-heading"><h2>{compact ? 'Recent processing' : 'Processing jobs'}</h2>
    <span>Last {jobs.retentionHours}h / max {jobs.capacity}</span></div>
    <div className="filters"><div className="segments" role="group" aria-label="Job status">
      {['all', 'failed', 'running', 'queued', 'succeeded'].map(state => <button key={state} aria-pressed={filter === state} onClick={() => setFilter(state)}>{state === 'all' ? 'All jobs' : labels[state]}</button>)}</div>
      {!compact && <label className="search"><Icon name="Search" /><input aria-label="Search operation ID or service" placeholder="Operation ID or service" value={query} onChange={e => setQuery(e.target.value)} maxLength={64} /></label>}</div>
    {rows.length === 0 ? <div className="empty"><Icon name="ListChecks" /><p>{jobs.jobs.length ? 'No matching jobs.' : 'No job observations received.'}</p></div> : <div className="table-scroll"><table><thead><tr><th>Operation</th><th>Service / kind</th><th>Status</th><th>Observed</th><th>Failure category</th></tr></thead>
        <tbody>{rows.slice(0, compact ? 5 : 200).map(job => <tr key={job.service + job.operationId}>
          <td><code title={job.operationId}>{job.operationId.slice(0, 8)}</code><small className="row-note">Revision {job.revision}</small></td>
          <td>{job.service}<small className="row-note">{job.kind.toUpperCase()}</small></td><td><Status value={job.state} /></td>
          <td>{time(job.observedAtUtc)}</td><td>{job.errorCategory || 'None'}</td></tr>)}</tbody></table></div>}
  </section>;
}
function Usage({
  dashboard
}) {
  const ai = dashboard.metrics.aiUsage;
  const ocr = dashboard.metrics.parsingQuality;
  return <><section className="section"><div className="section-heading"><h2>AI usage</h2><span>Process-local counters</span></div>
    <div className="metrics"><Metric title="Requests" value={number(ai.requestCount)} note={number(ai.successfulRequestCount) + ' successful'} />
      <Metric title="Input tokens" value={number(ai.inputTokenCount)} note="Reported by providers" />
      <Metric title="Output tokens" value={number(ai.outputTokenCount)} note="Reported by providers" />
      <Metric title="Estimated cost" value={number(ai.estimatedCostMicros)} note="Provider cost micro-units" /></div></section>
    <section className="section"><div className="section-heading"><h2>OCR & parsing quality</h2><span>Process-local counters</span></div>
      <div className="metrics"><Metric title="Processed" value={number(ocr.processedCount)} note="Received observations" />
        <Metric title="Successful" value={number(ocr.successfulCount)} note={ocr.successPercent + '% success'} />
        <Metric title="Review required" value={number(ocr.reviewRequiredCount)} note="Awaiting human confirmation" />
        <Metric title="Failed" value={number(ocr.failedCount)} note="Reported processing failures" /></div>
      <label className="quality">Parsing success<progress value={ocr.successPercent} max="100" />{ocr.successPercent}%</label>
    </section></>;
}
function Dependencies({
  dashboard
}) {
  const r = dashboard.rabbitMq;
  const e = dashboard.elasticsearch;
  return <section className="section"><div className="section-heading"><h2>Infrastructure</h2></div><div className="dependencies">
    <div><h3>RabbitMQ</h3><Status value={r.status} /><dl><div><dt>Queue depth</dt><dd>{number(r.queueDepth)}</dd></div>
      <div><dt>Consumers</dt><dd>{number(r.consumerCount)}</dd></div></dl></div>
    <div><h3>Elasticsearch</h3><Status value={e.status} /><dl><div><dt>Cluster</dt><dd>{e.clusterStatus}</dd></div>
      <div><dt>Nodes / active shards</dt><dd>{e.nodeCount} / {e.activeShardCount}</dd></div></dl></div>
  </div></section>;
}
function App() {
  const [view, setView] = useState('overview');
  const [data, setData] = useState(null);
  const [authenticated, setAuthenticated] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [autoRefresh, setAutoRefresh] = useState(false);
  const generation = useRef(0);
  const busyRef = useRef(false);
  const heading = useRef(null);
  async function load() {
    if (busyRef.current) return;
    const current = generation.current;
    busyRef.current = true;
    setBusy(true);
    setError('');
    try {
      const result = await api.load();
      if (current !== generation.current) return;
      setData(result);
      setAuthenticated(true);
    } catch (e) {
      if (current !== generation.current) return;
      setData(null);
      setError(messages[e.message] || messages.service_unavailable);
      if (['sign_in_required', 'admin_required'].includes(e.message)) {
        setAuthenticated(false);
        setAutoRefresh(false);
      }
    } finally {
      if (current === generation.current) {
        setBusy(false);
        busyRef.current = false;
      }
    }
  }
  async function signIn(event) {
    event.preventDefault();
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    setError('');
    const form = event.currentTarget;
    const fields = new FormData(form);
    const current = generation.current;
    try {
      await api.signIn(fields.get('email'), fields.get('password'));
      form.reset();
      if (current !== generation.current) return;
      busyRef.current = false;
      await load();
    } catch (e) {
      form.reset();
      setError(messages[e.message] || messages.service_unavailable);
    } finally {
      if (current === generation.current) {
        setBusy(false);
        busyRef.current = false;
      }
    }
  }
  async function signOut() {
    generation.current++;
    setData(null);
    setAuthenticated(false);
    setAutoRefresh(false);
    setError('');
    setBusy(false);
    busyRef.current = false;
    await api.signOut();
  }
  useEffect(() => {
    if (!authenticated || !autoRefresh) return;
    const timer = setInterval(() => {
      if (document.visibilityState === 'visible') load();
    }, 30000);
    return () => clearInterval(timer);
  }, [authenticated, autoRefresh]);
  useEffect(() => {
    heading.current?.focus();
  }, [view]);
  const title = tabs.find(tab => tab[0] === view)[2];
  return <div className="app"><aside className="sidebar"><div className="brand"><Icon name="Activity" /><strong>Financial Assistant</strong></div>
    <p className="workspace-label">OPERATIONS</p><nav aria-label="Primary">{tabs.map(([id, icon, label]) => <button key={id} className={view === id ? 'selected' : ''} aria-current={view === id ? 'page' : undefined} onClick={() => setView(id)} disabled={!authenticated}><Icon name={icon} />{label}</button>)}</nav>
    <div className="admin-label"><Icon name="ShieldCheck" />Administrator workspace</div></aside>
    <div className="workspace"><header><span>Operations / {title}</span><div className="header-actions">
      {authenticated && <><label className="toggle"><input type="checkbox" checked={autoRefresh} onChange={e => setAutoRefresh(e.target.checked)} />Live refresh</label>
        <button className="icon-button" title="Refresh" aria-label="Refresh" onClick={load} disabled={busy}><Icon name="RefreshCw" /></button>
        <button className="icon-button" title="Sign out" aria-label="Sign out" onClick={signOut}><Icon name="LogOut" /></button></>}
    </div></header><main><div className="page-heading"><div><p className="eyebrow">FINANCIAL ASSISTANT</p>
      <h1 ref={heading} tabIndex="-1">{authenticated ? title : 'Administrator sign in'}</h1></div>
      {data && <div className="snapshot"><Status value={data.dashboard.overallStatus} /><small>Observed {time(data.dashboard.generatedAtUtc)}</small></div>}</div>
      {error && <div role="alert" className="alert"><Icon name="TriangleAlert" />{error}</div>}
      {!authenticated ? <form className="login" onSubmit={signIn}><label>Email<input name="email" type="email" autoComplete="username" required maxLength={320} /></label>
        <label>Password<input name="password" type="password" autoComplete="current-password" required minLength={12} maxLength={128} /></label>
        <button type="submit" className="primary" disabled={busy}>{busy ? 'Signing in...' : 'Sign in'}</button></form> : busy && !data ? <div className="empty" role="status">Loading operational snapshot...</div> : !data ? <div className="empty"><p>No current snapshot available.</p><button onClick={load}>Retry</button></div> : <div aria-busy={busy}>{view === 'overview' && <><div className="metrics summary-metrics">
        <Metric title="Healthy components" value={data.dashboard.readiness.healthyCount + ' / ' + data.dashboard.readiness.componentCount} note="Latest readiness probes" />
        <Metric title="Needs attention" value={data.dashboard.readiness.degradedCount + data.dashboard.readiness.unavailableCount + data.dashboard.readiness.notConfiguredCount} note="Degraded, unavailable or unconfigured" />
        <Metric title="Queued messages" value={number(data.dashboard.rabbitMq.queueDepth)} note="RabbitMQ queue depth" />
        <Metric title="Failed jobs" value={data.jobs.jobs.filter(job => job.state === 'failed').length} note="Retained operational observations" /></div>
        <div className="overview-grid"><Services dashboard={data.dashboard} compact /><Dependencies dashboard={data.dashboard} /></div><Jobs jobs={data.jobs} compact /></>}
        {view === 'services' && <><Services dashboard={data.dashboard} /><Dependencies dashboard={data.dashboard} /></>}
        {view === 'jobs' && <Jobs jobs={data.jobs} />}{view === 'usage' && <Usage dashboard={data.dashboard} />}
        {view === 'support' && <section className="section support"><Icon name="LifeBuoy" /><h2>User support lookup</h2><p>Not available in this release.</p>
          <label>Support reference<input disabled placeholder="Unavailable" /></label><button disabled>Look up</button></section>}
      </div>}
      <footer>Internal operations<span>Restricted access</span></footer>
    </main></div></div>;
}
createRoot(document.getElementById('root')).render(<App />);
