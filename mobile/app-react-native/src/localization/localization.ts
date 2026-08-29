import { useMemo } from 'react';

import { englishMessages, ukrainianMessages, type MessageKey } from './catalogs';

type MessageValues = Record<string, string | number>;

function deviceLocale() {
  return Intl.DateTimeFormat().resolvedOptions().locale;
}

export function resolveLocale(locale?: string | null) {
  try {
    return Intl.getCanonicalLocales(locale?.trim() || deviceLocale())[0] ?? 'en-US';
  } catch {
    return 'en-US';
  }
}

function catalogFor(locale: string) {
  return locale.toLowerCase().startsWith('uk') ? ukrainianMessages : englishMessages;
}

export function translate(locale: string | null | undefined, key: MessageKey, values: MessageValues = {}) {
  const template = catalogFor(resolveLocale(locale))[key] ?? englishMessages[key];
  return template.replace(/\{(\w+)\}/g, (match, name: string) => (
    Object.hasOwn(values, name) ? String(values[name]) : match
  ));
}

export function useLocalization(locale?: string | null) {
  const resolvedLocale = resolveLocale(locale);
  return useMemo(() => ({
    locale: resolvedLocale,
    t: (key: MessageKey, values?: MessageValues) => translate(resolvedLocale, key, values),
  }), [resolvedLocale]);
}

export function formatCurrency(value: number, currency: string, locale?: string | null) {
  return new Intl.NumberFormat(resolveLocale(locale), { style: 'currency', currency }).format(value);
}

export function formatDateTime(
  value: string,
  locale?: string | null,
  options: Intl.DateTimeFormatOptions = { dateStyle: 'medium', timeStyle: 'short' },
) {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : new Intl.DateTimeFormat(resolveLocale(locale), options).format(parsed);
}

export function formatDateOnly(value: string, locale?: string | null) {
  const parts = value.split('-').map(Number);
  if (parts.length !== 3 || parts.some((part) => !Number.isInteger(part))) return value;
  const [year, month, day] = parts;
  const parsed = new Date(Date.UTC(year!, month! - 1, day));
  if (Number.isNaN(parsed.getTime())) return value;
  return new Intl.DateTimeFormat(resolveLocale(locale), {
    dateStyle: 'medium',
    timeZone: 'UTC',
  }).format(parsed);
}
