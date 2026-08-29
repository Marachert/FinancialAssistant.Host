import { useRef, useState } from 'react';
import * as Crypto from 'expo-crypto';
import { StyleSheet, Text, View } from 'react-native';

import { ApiProblem } from '@/api/client';
import { theme, typography } from '@/app/theme';
import { useLocalization } from '@/localization/localization';
import { LinkButton, PrimaryButton, ScreenScaffold, StatusBanner, TextField } from '@/shared/ui';

import type { AuthCredentials } from './authTypes';

type Props = {
  mode: 'sign-in' | 'register';
  onSubmit: (credentials: AuthCredentials) => Promise<void>;
  onAlternate: () => void;
};

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function AuthForm({ mode, onSubmit, onAlternate }: Props) {
  const { t } = useLocalization();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string>();
  const registrationKey = useRef(mode === 'register' ? Crypto.randomUUID() : undefined);

  const emailError = email.length > 0 && !emailPattern.test(email) ? t('auth.emailInvalid') : undefined;
  const passwordError = password.length > 0 && password.length < 12 ? t('auth.passwordInvalid') : undefined;
  const title = mode === 'register' ? t('auth.createAccount') : t('auth.signIn');

  const submit = async () => {
    setError(undefined);
    setSubmitting(true);
    try {
      await onSubmit({ email: email.trim(), password, idempotencyKey: registrationKey.current });
    } catch (reason) {
      setError(reason instanceof ApiProblem ? reason.message : t('auth.connectionError'));
    } finally {
      setSubmitting(false);
    }
  };

  const disabled = !emailPattern.test(email.trim()) || password.length < 12;

  return (
    <ScreenScaffold>
      <View style={styles.header}>
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>{title}</Text>
        <Text style={[typography.body, styles.supporting]}>
          {mode === 'register' ? t('auth.registerIntro') : t('auth.signInIntro')}
        </Text>
      </View>
      {error ? <StatusBanner>{error}</StatusBanner> : null}
      <View>
        <TextField autoCapitalize="none" autoComplete="email" keyboardType="email-address" label={t('auth.email')} onChangeText={setEmail} value={email} error={emailError} />
        <TextField
          autoCapitalize="none"
          autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
          error={passwordError}
          label={t('auth.password')}
          onChangeText={setPassword}
          secureTextEntry={!passwordVisible}
          trailingAction={{
            label: passwordVisible ? t('auth.hidePassword') : t('auth.showPassword'),
            onPress: () => setPasswordVisible((visible) => !visible),
          }}
          value={password}
        />
      </View>
      <PrimaryButton disabled={disabled} label={title} loading={submitting} onPress={() => void submit()} />
      <LinkButton label={mode === 'register' ? t('auth.alreadyRegistered') : t('auth.newAccount')} onPress={onAlternate} />
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  header: { gap: theme.spacing.sm },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
});
