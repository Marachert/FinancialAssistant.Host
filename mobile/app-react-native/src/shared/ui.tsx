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

export function ScreenScaffold({ children, centered = true }: PropsWithChildren<{ centered?: boolean }>) {
  return (
    <SafeAreaView style={styles.safeArea}>
      <KeyboardAvoidingView style={styles.fill} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView
          contentContainerStyle={[styles.screen, centered && styles.screenCentered]}
          keyboardShouldPersistTaps="handled"
        >
          {children}
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

type TextFieldProps = TextInputProps & {
  label: string;
  error?: string;
  trailingAction?: { label: string; onPress: () => void };
};

export function TextField({ label, error, trailingAction, ...props }: TextFieldProps) {
  const [focused, setFocused] = useState(false);
  return (
    <View style={styles.fieldGroup}>
      <Text style={[typography.bodyStrong, styles.label]}>{label}</Text>
      <View style={[styles.inputShell, focused && styles.inputFocused, Boolean(error) && styles.inputInvalid]}>
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
          style={[styles.input, props.style]}
        />
        {trailingAction ? (
          <Pressable
            accessibilityLabel={trailingAction.label}
            accessibilityRole="button"
            onPress={trailingAction.onPress}
            style={styles.fieldAction}
          >
            <Text style={styles.fieldActionLabel}>{trailingAction.label}</Text>
          </Pressable>
        ) : null}
      </View>
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

export function SecondaryButton({ label, disabled, onPress }: { label: string; disabled?: boolean; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled }}
      disabled={disabled}
      onPress={onPress}
      style={({ pressed }) => [styles.secondaryButton, pressed && styles.secondaryPressed, disabled && styles.disabled]}
    >
      <Text style={styles.secondaryLabel}>{label}</Text>
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

export function StatusBanner({ children, tone = 'error' }: { children: ReactNode; tone?: 'error' | 'warning' | 'info' | 'success' }) {
  return (
    <View accessibilityRole="alert" style={[styles.banner, styles[`banner_${tone}`]]}>
      <Text style={[typography.small, styles[`bannerText_${tone}`]]}>{children}</Text>
    </View>
  );
}

export function SegmentedControl({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: readonly string[];
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <View accessibilityRole="radiogroup" accessibilityLabel={label} style={styles.segmentGroup}>
      {options.map((option) => {
        const selected = option === value;
        return (
          <Pressable
            key={option}
            accessibilityRole="radio"
            accessibilityState={{ selected }}
            onPress={() => onChange(option)}
            style={[styles.segment, selected && styles.segmentSelected]}
          >
            <Text style={[typography.bodyStrong, styles.segmentLabel, selected && styles.segmentLabelSelected]}>{option}</Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  fill: { flex: 1 },
  safeArea: { flex: 1, backgroundColor: theme.colors.canvas },
  screen: { flexGrow: 1, padding: theme.spacing.lg, gap: theme.spacing.lg },
  screenCentered: { justifyContent: 'center' },
  fieldGroup: { gap: theme.spacing.xs },
  label: { color: theme.colors.textPrimary },
  inputShell: { minHeight: 48, flexDirection: 'row', alignItems: 'center', borderWidth: 1, borderColor: theme.colors.border, borderRadius: theme.radius.control, backgroundColor: theme.colors.surface },
  input: { minHeight: 46, flex: 1, paddingHorizontal: theme.spacing.md, paddingVertical: theme.spacing.md, color: theme.colors.textPrimary, ...typography.body },
  inputFocused: { borderColor: theme.colors.action, borderWidth: 2 },
  inputInvalid: { borderColor: theme.colors.critical },
  fieldAction: { minWidth: 48, minHeight: 44, alignItems: 'center', justifyContent: 'center', paddingHorizontal: theme.spacing.sm },
  fieldActionLabel: { ...typography.small, color: theme.colors.action, fontWeight: '600' },
  help: { color: theme.colors.textSecondary },
  error: { color: theme.colors.critical },
  primaryButton: { minHeight: 48, borderRadius: theme.radius.control, backgroundColor: theme.colors.action, alignItems: 'center', justifyContent: 'center', paddingHorizontal: theme.spacing.lg },
  primaryPressed: { backgroundColor: theme.colors.actionPressed },
  primaryLabel: { ...typography.bodyStrong, color: theme.colors.onAction },
  disabled: { opacity: 0.55 },
  secondaryButton: { minHeight: 48, borderRadius: theme.radius.control, borderWidth: 1, borderColor: theme.colors.action, backgroundColor: theme.colors.surface, alignItems: 'center', justifyContent: 'center', paddingHorizontal: theme.spacing.lg },
  secondaryPressed: { backgroundColor: theme.colors.surfaceSubtle },
  secondaryLabel: { ...typography.bodyStrong, color: theme.colors.action },
  linkButton: { minHeight: 44, alignItems: 'center', justifyContent: 'center', paddingHorizontal: theme.spacing.sm },
  linkLabel: { ...typography.bodyStrong, color: theme.colors.action },
  banner: { borderLeftWidth: 4, backgroundColor: theme.colors.surface, padding: theme.spacing.md },
  banner_error: { borderColor: theme.colors.critical },
  banner_warning: { borderColor: theme.colors.warning },
  banner_info: { borderColor: theme.colors.info },
  banner_success: { borderColor: theme.colors.positive },
  bannerText_error: { color: theme.colors.critical },
  bannerText_warning: { color: theme.colors.warning },
  bannerText_info: { color: theme.colors.info },
  bannerText_success: { color: theme.colors.positive },
  segmentGroup: { minHeight: 48, flexDirection: 'row', borderWidth: 1, borderColor: theme.colors.border, borderRadius: theme.radius.control, overflow: 'hidden' },
  segment: { minHeight: 46, flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.surface, paddingHorizontal: theme.spacing.sm },
  segmentSelected: { backgroundColor: theme.colors.action },
  segmentLabel: { color: theme.colors.textPrimary },
  segmentLabelSelected: { color: theme.colors.onAction },
});
