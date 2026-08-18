import { useState, type PropsWithChildren, type ReactNode } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  SafeAreaView,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';

import { theme, typography } from '@/app/theme';

export function ScreenScaffold({ children }: PropsWithChildren) {
  return (
    <SafeAreaView style={styles.safeArea}>
      <KeyboardAvoidingView style={styles.fill} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView contentContainerStyle={styles.screen} keyboardShouldPersistTaps="handled">
          {children}
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

export function TextField({ label, error, ...props }: TextInputProps & { label: string; error?: string }) {
  const [focused, setFocused] = useState(false);
  return (
    <View style={styles.fieldGroup}>
      <Text style={[typography.bodyStrong, styles.label]}>{label}</Text>
      <TextInput
        {...props}
        accessibilityLabel={label}
        accessibilityState={{ disabled: props.editable === false }}
        onBlur={(event) => {
          setFocused(false);
          props.onBlur?.(event);
        }}
        onFocus={(event) => {
          setFocused(true);
          props.onFocus?.(event);
        }}
        style={[styles.input, focused && styles.inputFocused, Boolean(error) && styles.inputInvalid, props.style]}
      />
      <Text accessibilityLiveRegion="polite" style={[typography.small, error ? styles.error : styles.help]}>
        {error || ' '}
      </Text>
    </View>
  );
}

export function PrimaryButton({ label, loading, disabled, onPress }: { label: string; loading?: boolean; disabled?: boolean; onPress: () => void }) {
  const unavailable = disabled || loading;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ busy: loading, disabled: unavailable }}
      disabled={unavailable}
      onPress={onPress}
      style={({ pressed }) => [styles.primaryButton, pressed && styles.primaryPressed, unavailable && styles.disabled]}
    >
      {loading ? <ActivityIndicator color={theme.colors.onAction} /> : <Text style={styles.primaryLabel}>{label}</Text>}
    </Pressable>
  );
}

export function LinkButton({ label, onPress }: { label: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="link" onPress={onPress} style={styles.linkButton}>
      <Text style={styles.linkLabel}>{label}</Text>
    </Pressable>
  );
}

export function StatusBanner({ children }: { children: ReactNode }) {
  return (
    <View accessibilityRole="alert" style={styles.banner}>
      <Text style={[typography.small, styles.bannerText]}>{children}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  safeArea: { flex: 1, backgroundColor: theme.colors.canvas },
  screen: { flexGrow: 1, padding: theme.spacing.lg, gap: theme.spacing.lg, justifyContent: 'center' },
  fieldGroup: { gap: theme.spacing.xs },
  label: { color: theme.colors.textPrimary },
  input: { minHeight: 48, borderWidth: 1, borderColor: theme.colors.border, borderRadius: theme.radius.control, backgroundColor: theme.colors.surface, paddingHorizontal: theme.spacing.md, paddingVertical: theme.spacing.md, color: theme.colors.textPrimary, ...typography.body },
  inputFocused: { borderColor: theme.colors.action, borderWidth: 2 },
  inputInvalid: { borderColor: theme.colors.critical },
  help: { color: theme.colors.textSecondary },
  error: { color: theme.colors.critical },
  primaryButton: { minHeight: 48, borderRadius: theme.radius.control, backgroundColor: theme.colors.action, alignItems: 'center', justifyContent: 'center', paddingHorizontal: theme.spacing.lg },
  primaryPressed: { backgroundColor: theme.colors.actionPressed },
  primaryLabel: { ...typography.bodyStrong, color: theme.colors.onAction },
  disabled: { opacity: 0.55 },
  linkButton: { minHeight: 44, alignItems: 'center', justifyContent: 'center', paddingHorizontal: theme.spacing.sm },
  linkLabel: { ...typography.bodyStrong, color: theme.colors.action },
  banner: { borderLeftWidth: 4, borderColor: theme.colors.critical, backgroundColor: theme.colors.surface, padding: theme.spacing.md },
  bannerText: { color: theme.colors.critical },
});
