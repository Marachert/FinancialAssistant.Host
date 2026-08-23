import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const requiredFiles = [
  'app.json',
  'src/app/_layout.tsx',
  'src/app/(app)/_layout.tsx',
  'src/app/(auth)/sign-in.tsx',
  'src/app/(auth)/register.tsx',
  'src/app/(app)/home.tsx',
  'src/app/(app)/analytics.tsx',
  'src/app/(app)/onboarding.tsx',
  'src/app/(app)/add.tsx',
  'src/app/(app)/draft.tsx',
  'src/app/(app)/score.tsx',
  'src/app/(app)/recommendations.tsx',
  'src/app/(app)/settings.tsx',
  'src/api/client.ts',
  'src/features/auth/AuthForm.tsx',
  'src/features/auth/AuthProvider.tsx',
  'src/security/sessionStore.ts',
  'src/features/capture/captureApi.ts',
  'src/features/capture/CaptureProvider.tsx',
  'src/features/capture/captureTypes.ts',
  'src/features/insights/insightsApi.ts',
  'src/features/insights/InsightsProvider.tsx',
  'src/features/insights/insightsTypes.ts',
  'src/shared/ui.tsx',
];

const contents = await Promise.all(requiredFiles.map(async (path) => [path, await readFile(resolve(root, path), 'utf8')]));
const combined = contents.map(([, content]) => content).join('\n');
const envExample = await readFile(resolve(root, '.env.example'), 'utf8');

const failures = [];
if (!combined.includes('expo-secure-store')) failures.push('SecureStore is required for authentication state.');
if (!combined.includes('/auth/v1/refresh')) failures.push('Token refresh route is missing.');
if (!combined.includes('refreshInFlight')) failures.push('Refresh-token rotation must be serialized.');
if (!combined.includes('refreshOnUnauthorized: false')) failures.push('Logout must not rotate the refresh token automatically.');
if (!combined.includes('Stack.Protected')) failures.push('Authenticated route protection is missing.');
if (!combined.includes("passwordVisible ? 'Hide' : 'Show'")) failures.push('Password fields require an accessible visibility control.');
if (/AsyncStorage/.test(combined)) failures.push('Authentication tokens must not use AsyncStorage.');
if (/console\.(log|debug|info|warn|error)/.test(combined)) failures.push('Authentication source must not log sensitive state.');
if (!combined.includes('/transactions/intake')) failures.push('Gateway transaction intake route is missing.');
if (!combined.includes('/receipts')) failures.push('Gateway receipt upload route is missing.');
if (!combined.includes('/transactions/drafts/receipts/')) failures.push('Owner-scoped receipt draft lookup is missing.');
if (!combined.includes('expectedRevision')) failures.push('Draft optimistic concurrency is missing.');
if (!combined.includes('/confirm')) failures.push('Backend draft confirmation is missing.');
if (!combined.includes('/reject')) failures.push('Backend draft rejection is missing.');
if (!combined.includes('api.rejectDraft(draft.id)')) failures.push('Draft discard must reject the backend draft.');
if (!combined.includes("'Discard this draft?'")) failures.push('Draft rejection requires explicit confirmation.');
if (!combined.includes('isConfirmationReplayState(latest.status)')) failures.push('Lost confirmation response recovery is missing.');
if (!combined.includes('api.confirmDraft(latest.id)')) failures.push('Idempotent confirmation replay is missing.');
if (!combined.includes('FormData')) failures.push('Multipart receipt upload is missing.');
if (!combined.includes('AbortController')) failures.push('Receipt processing cancellation is missing.');
if (!combined.includes('ocr_completed')) failures.push('OCR suggestion polling is missing.');
if (!combined.includes('ocr_failed')) failures.push('OCR failure handling is missing.');
if (!combined.includes('expo-image-picker')) failures.push('Camera receipt selection is missing.');
if (!combined.includes('expo-document-picker')) failures.push('File receipt selection is missing.');
if (!combined.includes('/analytics/dashboard')) failures.push('Dashboard analytics route is missing.');
if (!combined.includes('/analytics/category-breakdown')) failures.push('Analytics category breakdown route is missing.');
if (!combined.includes('/financial-score/current')) failures.push('Financial score route is missing.');
if (!combined.includes('/recommendations')) failures.push('Recommendation route is missing.');
if (!combined.includes('/users/me/preferences')) failures.push('Profile preferences route is missing.');
if (!combined.includes('/notification-preferences')) failures.push('Notification preferences route is missing.');
if (!combined.includes('RefreshControl')) failures.push('Insight screens require pull-to-refresh.');
if (!combined.includes('label="Dashboard period"')) failures.push('Dashboard period selection is missing.');
if (!combined.includes('label="Analytics period"')) failures.push('Analytics period selection is missing.');
if (!combined.includes("Daily: 'daily'") || !combined.includes("Weekly: 'weekly'") || !combined.includes("Monthly: 'monthly'")) failures.push('Analytics periods are incomplete.');
if (!combined.includes('dashboard.weeklySummary') || !combined.includes('dashboard.monthlySummary')) failures.push('Dashboard period summaries are incomplete.');
if (!combined.includes('No activity for this period yet.')) failures.push('Dashboard empty state is missing.');
if (!combined.includes('No spending categories for this period yet.')) failures.push('Analytics category empty state is missing.');
if (!combined.includes('Loading analytics...')) failures.push('Analytics loading state is missing.');
if (!combined.includes('Retry analytics')) failures.push('Analytics error recovery is missing.');
if (!combined.includes('label="Upload receipt"')) failures.push('Dashboard receipt quick action is missing.');
if (!combined.includes('<Switch')) failures.push('Settings require accessible binary controls.');
if (!combined.includes('Promise.allSettled')) failures.push('Independent insight failures must preserve available data.');
if (!combined.includes('if (notifications) saves.push')) failures.push('Unavailable notification settings must not block profile saves.');
if (!combined.includes('Retry notifications')) failures.push('Notification preferences require an explicit retry path.');
if (!combined.includes('requestPermissionsAsync')) failures.push('Onboarding notification permission request is missing.');
if (!/profile\?\.profileOnboardingCompleted\s*&&\s*profile\.preferencesOnboardingCompleted/.test(combined)) failures.push('Initial navigation must require completed onboarding.');
if (!combined.includes('profileOnboardingCompleted: true') || !combined.includes('preferencesOnboardingCompleted: true')) failures.push('Onboarding completion state is not persisted.');
if (!combined.includes('Skip budget')) failures.push('Optional budget setup requires an explicit skip path.');
if (combined.includes('maximumFractionDigits: 0')) failures.push('Currency formatting must preserve standard fractional precision.');
if (/['"`]\/api\/v1\//.test(combined)) failures.push('Mobile source must call only public gateway aliases.');
if (/EXPO_PUBLIC_(?!API_URL)/.test(envExample)) failures.push('Only the public gateway URL may be exposed in the example environment.');
if (/(TOKEN|SECRET|PASSWORD|KEY)=\S+/i.test(envExample)) failures.push('The example environment contains a credential-like value.');

if (failures.length > 0) {
  for (const failure of failures) process.stderr.write(`ERROR: ${failure}\n`);
  process.exit(1);
}

process.stdout.write(`Verified ${requiredFiles.length} mobile foundation files and public configuration.\n`);
