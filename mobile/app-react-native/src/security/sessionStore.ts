import * as Crypto from 'expo-crypto';
import * as SecureStore from 'expo-secure-store';

import type { AuthSession } from '@/features/auth/authTypes';

const sessionKey = 'financial-assistant.auth-session.v1';
const clientInstanceKey = 'financial-assistant.client-instance.v1';
const secureOptions: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
};

function isAuthSession(value: unknown): value is AuthSession {
  if (!value || typeof value !== 'object') return false;
  const session = value as Partial<AuthSession>;
  return Boolean(
    session.accessToken &&
      session.refreshToken &&
      session.accessTokenExpiresAtUtc &&
      session.refreshTokenExpiresAtUtc &&
      session.user?.userId &&
      session.user.sessionId,
  );
}

export async function readSession(): Promise<AuthSession | null> {
  const serialized = await SecureStore.getItemAsync(sessionKey);
  if (!serialized) return null;

  try {
    const parsed: unknown = JSON.parse(serialized);
    if (isAuthSession(parsed)) return parsed;
  } catch {
    // Invalid secure state is cleared below and treated as signed out.
  }

  await clearSession();
  return null;
}

export async function writeSession(session: AuthSession): Promise<void> {
  await SecureStore.setItemAsync(sessionKey, JSON.stringify(session), secureOptions);
}

export async function clearSession(): Promise<void> {
  await SecureStore.deleteItemAsync(sessionKey);
}

export async function getClientInstanceId(): Promise<string> {
  const existing = await SecureStore.getItemAsync(clientInstanceKey);
  if (existing && existing.length >= 8) return existing;

  const created = Crypto.randomUUID();
  await SecureStore.setItemAsync(clientInstanceKey, created, secureOptions);
  return created;
}
