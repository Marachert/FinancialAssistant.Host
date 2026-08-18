const localGatewayUrl = 'http://localhost:8080';

export function getApiBaseUrl(): string {
  const configuredUrl = process.env.EXPO_PUBLIC_API_URL?.trim();
  const value = configuredUrl || localGatewayUrl;

  try {
    const url = new URL(value);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') throw new Error('unsupported protocol');
    return url.toString().replace(/\/$/, '');
  } catch {
    throw new Error('EXPO_PUBLIC_API_URL must be an absolute HTTP or HTTPS URL.');
  }
}
