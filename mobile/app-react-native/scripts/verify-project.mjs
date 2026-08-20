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
  'src/api/client.ts',
  'src/features/auth/AuthForm.tsx',
  'src/features/auth/AuthProvider.tsx',
  'src/security/sessionStore.ts',
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
if (/EXPO_PUBLIC_(?!API_URL)/.test(envExample)) failures.push('Only the public gateway URL may be exposed in the example environment.');
if (/(TOKEN|SECRET|PASSWORD|KEY)=\S+/i.test(envExample)) failures.push('The example environment contains a credential-like value.');

if (failures.length > 0) {
  for (const failure of failures) process.stderr.write(`ERROR: ${failure}\n`);
  process.exit(1);
}

process.stdout.write(`Verified ${requiredFiles.length} mobile foundation files and public configuration.\n`);
