import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const requiredFiles = [
  'app.json',
  'src/app/_layout.tsx',
  'src/app/(auth)/sign-in.tsx',
  'src/app/(auth)/register.tsx',
  'src/app/(app)/home.tsx',
  'src/app/(app)/add.tsx',
  'src/app/(app)/draft.tsx',
  'src/api/client.ts',
  'src/features/auth/AuthForm.tsx',
  'src/features/auth/AuthProvider.tsx',
  'src/security/sessionStore.ts',
  'src/features/capture/captureApi.ts',
  'src/features/capture/CaptureProvider.tsx',
  'src/features/capture/captureTypes.ts',
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
if (!combined.includes('FormData')) failures.push('Multipart receipt upload is missing.');
if (!combined.includes('AbortController')) failures.push('Receipt processing cancellation is missing.');
if (!combined.includes('ocr_completed')) failures.push('OCR suggestion polling is missing.');
if (!combined.includes('ocr_failed')) failures.push('OCR failure handling is missing.');
if (!combined.includes('expo-image-picker')) failures.push('Camera receipt selection is missing.');
if (!combined.includes('expo-document-picker')) failures.push('File receipt selection is missing.');
if (/['"`]\/api\/v1\//.test(combined)) failures.push('Mobile source must call only public gateway aliases.');
if (/EXPO_PUBLIC_(?!API_URL)/.test(envExample)) failures.push('Only the public gateway URL may be exposed in the example environment.');
if (/(TOKEN|SECRET|PASSWORD|KEY)=\S+/i.test(envExample)) failures.push('The example environment contains a credential-like value.');

if (failures.length > 0) {
  for (const failure of failures) process.stderr.write(`ERROR: ${failure}\n`);
  process.exit(1);
}

process.stdout.write(`Verified ${requiredFiles.length} mobile foundation files and public configuration.\n`);
