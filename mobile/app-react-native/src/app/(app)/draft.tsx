import { useState } from 'react';
import { Redirect, router } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';

import { ApiProblem } from '@/api/client';
import { theme, typography } from '@/app/theme';
import { useCapture } from '@/features/capture/CaptureProvider';
import type { TransactionDraft, TransactionType } from '@/features/capture/captureTypes';
import {
  PrimaryButton,
  ScreenScaffold,
  SecondaryButton,
  SegmentedControl,
  StatusBanner,
  TextField,
} from '@/shared/ui';

type FormState = {
  type: TransactionType;
  amount: string;
  currency: string;
  categoryId: string;
  merchant: string;
  date: string;
  note: string;
};

function formFromDraft(draft: TransactionDraft): FormState {
  return {
    type: draft.type === 'income' ? 'income' : 'expense',
    amount: draft.amount?.toString() || '',
    currency: draft.currency || '',
    categoryId: draft.categoryId || '',
    merchant: draft.merchant || '',
    date: draft.date || '',
    note: draft.note || '',
  };
}

function isConfirmationReplayState(status: string) {
  return status === 'confirming' || status === 'confirmed';
}

export default function DraftReviewScreen() {
  const { api, draft, setDraft, confirmed, setConfirmed, reset } = useCapture();
  const [form, setForm] = useState<FormState | null>(draft ? formFromDraft(draft) : null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!draft || !form) return <Redirect href="/add" />;

  const setField = <Field extends keyof FormState>(field: Field, value: FormState[Field]) => {
    setForm((current) => current ? { ...current, [field]: value } : current);
    setError(null);
  };

  const confirm = async () => {
    const amount = Number(form.amount);
    if (!Number.isFinite(amount) || amount <= 0 || !form.currency.trim() || !form.categoryId.trim() || !form.date.trim()) {
      setError('Check amount, currency, category, and date before confirming.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      if (isConfirmationReplayState(draft.status)) {
        const result = await api.confirmDraft(draft.id);
        setConfirmed(result);
        return;
      }

      const reviewed = await api.updateDraft(draft.id, {
        expectedRevision: draft.revision,
        type: form.type,
        amount,
        currency: form.currency.trim(),
        categoryId: form.categoryId.trim(),
        merchant: form.merchant.trim() || null,
        date: form.date.trim(),
        note: form.note.trim() || null,
      });
      setDraft(reviewed);
      if (reviewed.requiresReview) {
        setError(reviewed.suggestion.reviewMessage || 'Some fields still need review.');
        return;
      }
      const result = await api.confirmDraft(reviewed.id);
      setConfirmed(result);
    } catch (reason) {
      if (reason instanceof ApiProblem && reason.status === 409) {
        try {
          const latest = await api.getDraft(draft.id);
          setDraft(latest);
          setForm(formFromDraft(latest));
          if (isConfirmationReplayState(latest.status)) {
            const result = await api.confirmDraft(latest.id);
            setConfirmed(result);
            return;
          }
          setError('The draft changed on the server. Latest values are shown; review them again.');
        } catch {
          setError('The draft changed on the server and could not be refreshed. Try again.');
        }
      } else {
        setError(
          reason instanceof ApiProblem
            ? reason.message
            : 'Confirmation did not complete. Your edits are preserved and retry is safe.',
        );
      }
    } finally {
      setBusy(false);
    }
  };

  if (confirmed) {
    return (
      <ScreenScaffold>
        <StatusBanner tone="success">Transaction confirmed by the backend.</StatusBanner>
        <View style={styles.success}>
          <Text accessibilityRole="header" style={[typography.title, styles.title]}>Saved</Text>
          <Text style={[typography.display, styles.amount]}>{confirmed.amount} {confirmed.currency}</Text>
          <Text style={[typography.body, styles.supporting]}>{confirmed.transactionType === 'income' ? 'Income' : 'Expense'} · {confirmed.categoryId}</Text>
        </View>
        <PrimaryButton
          label="Return home"
          onPress={() => {
            reset();
            router.replace('/home');
          }}
        />
      </ScreenScaffold>
    );
  }

  const uncertain = draft.requiresReview || draft.confidence < 0.8 || draft.ambiguities.length > 0;
  return (
    <ScreenScaffold centered={false}>
      <View style={styles.heading}>
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>Review transaction</Text>
        <Text style={[typography.body, styles.supporting]}>This is an editable suggestion. Nothing is saved as a financial record until you confirm.</Text>
      </View>
      {uncertain ? (
        <StatusBanner tone="warning">
          Please check this suggestion. {draft.ambiguities.length ? `Uncertain: ${draft.ambiguities.map((item) => item.replaceAll('_', ' ')).join(', ')}.` : draft.suggestion.reviewMessage}
        </StatusBanner>
      ) : null}
      {error ? <StatusBanner>{error}</StatusBanner> : null}
      <View style={styles.field}>
        <Text style={[typography.bodyStrong, styles.title]}>Type</Text>
        <SegmentedControl
          label="Transaction type"
          options={['expense', 'income']}
          value={form.type}
          onChange={(value) => setField('type', value as TransactionType)}
        />
      </View>
      <TextField label="Amount" value={form.amount} onChangeText={(value) => setField('amount', value)} keyboardType="decimal-pad" editable={!busy} />
      <TextField label="Currency" value={form.currency} onChangeText={(value) => setField('currency', value)} autoCapitalize="characters" editable={!busy} />
      <TextField label="Category" value={form.categoryId} onChangeText={(value) => setField('categoryId', value)} autoCapitalize="none" editable={!busy} />
      <TextField label="Merchant or source (optional)" value={form.merchant} onChangeText={(value) => setField('merchant', value)} editable={!busy} />
      <TextField label="Date (YYYY-MM-DD)" value={form.date} onChangeText={(value) => setField('date', value)} editable={!busy} />
      <TextField label="Note (optional)" value={form.note} onChangeText={(value) => setField('note', value)} multiline editable={!busy} />
      <PrimaryButton label="Confirm transaction" loading={busy} onPress={() => void confirm()} />
      <SecondaryButton label="Back to edit input" disabled={busy} onPress={() => router.back()} />
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  heading: { gap: theme.spacing.sm },
  field: { gap: theme.spacing.sm },
  success: { alignItems: 'center', gap: theme.spacing.sm },
  title: { color: theme.colors.textPrimary },
  amount: { color: theme.colors.textPrimary, fontVariant: ['tabular-nums'] },
  supporting: { color: theme.colors.textSecondary },
});
