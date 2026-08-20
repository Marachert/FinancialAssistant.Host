import { useRef, useState } from 'react';
import * as Crypto from 'expo-crypto';
import { StyleSheet, Text, View } from 'react-native';

import { ApiProblem } from '@/api/client';
import { theme, typography } from '@/app/theme';
import { LinkButton, PrimaryButton, ScreenScaffold, StatusBanner, TextField } from '@/shared/ui';

import type { AuthCredentials } from './authTypes';

type Props = {
  mode: 'sign-in' | 'register';
  onSubmit: (credentials: AuthCredentials) => Promise<void>;
  onAlternate: () => void;
};

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function AuthForm({ mode, onSubmit, onAlternate }: Props) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string>();
  const registrationKey = useRef(mode === 'register' ? Crypto.randomUUID() : undefined);

  const emailError = email.length > 0 && !emailPattern.test(email) ? 'Enter a valid email address.' : undefined;
  const passwordError = password.length > 0 && password.length < 12 ? 'Password must contain at least 12 characters.' : undefined;
  const title = mode === 'register' ? 'Create account' : 'Sign in';

  const submit = async () => {
    setError(undefined);
    setSubmitting(true);
    try {
      await onSubmit({ email: email.trim(), password, idempotencyKey: registrationKey.current });
    } catch (reason) {
      setError(reason instanceof ApiProblem ? reason.message : 'Could not reach Financial Assistant. Check your connection and try again.');
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
          {mode === 'register' ? 'Start with a secure account for your financial workspace.' : 'Use your Financial Assistant account.'}
        </Text>
      </View>
      {error ? <StatusBanner>{error}</StatusBanner> : null}
      <View>
        <TextField autoCapitalize="none" autoComplete="email" keyboardType="email-address" label="Email" onChangeText={setEmail} value={email} error={emailError} />
        <TextField
          autoCapitalize="none"
          autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
          error={passwordError}
          label="Password"
          onChangeText={setPassword}
          secureTextEntry={!passwordVisible}
          trailingAction={{
            label: passwordVisible ? 'Hide' : 'Show',
            onPress: () => setPasswordVisible((visible) => !visible),
          }}
          value={password}
        />
      </View>
      <PrimaryButton disabled={disabled} label={title} loading={submitting} onPress={() => void submit()} />
      <LinkButton label={mode === 'register' ? 'Already have an account? Sign in' : 'New here? Create account'} onPress={onAlternate} />
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  header: { gap: theme.spacing.sm },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
});
