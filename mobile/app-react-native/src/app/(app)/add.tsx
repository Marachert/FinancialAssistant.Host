import { useRef, useState } from 'react';
import { router } from 'expo-router';
import * as Crypto from 'expo-crypto';
import * as DocumentPicker from 'expo-document-picker';
import * as ImagePicker from 'expo-image-picker';
import { Image, Linking, StyleSheet, Text, View } from 'react-native';

import { ApiProblem } from '@/api/client';
import { theme, typography } from '@/app/theme';
import { useCapture } from '@/features/capture/CaptureProvider';
import type { ReceiptSelection } from '@/features/capture/captureTypes';
import { useInsights } from '@/features/insights/InsightsProvider';
import { useLocalization } from '@/localization/localization';
import {
  PrimaryButton,
  ScreenScaffold,
  SecondaryButton,
  StatusBanner,
  TextField,
} from '@/shared/ui';

const maximumReceiptSize = 10 * 1024 * 1024;
const supportedTypes = new Set(['image/jpeg', 'image/png', 'image/webp']);

type Phase = 'idle' | 'creating' | 'uploading' | 'processing';
type ReceiptPermission = 'camera' | 'gallery';
type PermissionRecovery = { source: ReceiptPermission; canAskAgain: boolean };

function normalizeSelection(
  uri: string,
  contentType?: string | null,
  name?: string | null,
  sizeBytes?: number,
): ReceiptSelection | null {
  const normalizedType = contentType?.toLowerCase();
  if (!normalizedType || !supportedTypes.has(normalizedType)) return null;
  return {
    uri,
    contentType: normalizedType as ReceiptSelection['contentType'],
    name: name || `receipt.${normalizedType.split('/')[1]}`,
    sizeBytes,
  };
}

function wait(milliseconds: number, signal: AbortSignal) {
  return new Promise<void>((resolve, reject) => {
    const timeout = setTimeout(resolve, milliseconds);
    signal.addEventListener(
      'abort',
      () => {
        clearTimeout(timeout);
        const error = new Error('Receipt processing cancelled.');
        error.name = 'AbortError';
        reject(error);
      },
      { once: true },
    );
  });
}

export default function AddTransactionScreen() {
  const { api, input, setInput, receipt, setReceipt, setDraft, setConfirmed } = useCapture();
  const { profile } = useInsights();
  const { t } = useLocalization(profile?.locale);
  const [phase, setPhase] = useState<Phase>('idle');
  const [error, setError] = useState<string | null>(null);
  const [permissionPrompt, setPermissionPrompt] = useState<ReceiptPermission | null>(null);
  const [permissionRecovery, setPermissionRecovery] = useState<PermissionRecovery | null>(null);
  const [permissionBusy, setPermissionBusy] = useState(false);
  const textRequest = useRef<{ input: string; key: string } | null>(null);
  const receiptRequest = useRef<{ uri: string; key: string } | null>(null);
  const abortController = useRef<AbortController | null>(null);
  const busy = phase !== 'idle';

  const setImagePickerSelection = (asset: ImagePicker.ImagePickerAsset) => {
    const selection = normalizeSelection(asset.uri, asset.mimeType, asset.fileName, asset.fileSize);
    setReceipt(selection);
    if (!selection) setError('Use a JPEG, PNG, or WebP receipt image.');
  };

  const launchCamera = async () => {
    try {
      const result = await ImagePicker.launchCameraAsync({
        mediaTypes: ['images'],
        allowsEditing: false,
        quality: 0.9,
      });
      if (result.canceled) return;
      const asset = result.assets[0];
      if (!asset) return;
      setImagePickerSelection(asset);
    } catch {
      setError('The camera could not be opened. Choose a receipt file or try again.');
    }
  };

  const launchGallery = async () => {
    try {
      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ['images'],
        allowsEditing: false,
        quality: 1,
        selectionLimit: 1,
      });
      if (result.canceled) return;
      const asset = result.assets[0];
      if (!asset) return;
      setImagePickerSelection(asset);
    } catch {
      setError('The photo library could not be opened. Choose a receipt file or try again.');
    }
  };

  const launchPermissionSource = async (source: ReceiptPermission) => {
    if (source === 'camera') await launchCamera();
    else await launchGallery();
  };

  const inspectPermission = async (source: ReceiptPermission) => {
    setError(null);
    setPermissionRecovery(null);
    try {
      const permission = source === 'camera'
        ? await ImagePicker.getCameraPermissionsAsync()
        : await ImagePicker.getMediaLibraryPermissionsAsync();
      if (permission.granted) {
        await launchPermissionSource(source);
      } else if (permission.canAskAgain) {
        setPermissionPrompt(source);
      } else {
        setPermissionRecovery({ source, canAskAgain: false });
      }
    } catch {
      setError(t('permissions.checkFailed'));
    }
  };

  const requestPermission = async (source: ReceiptPermission) => {
    setPermissionBusy(true);
    setError(null);
    try {
      const permission = source === 'camera'
        ? await ImagePicker.requestCameraPermissionsAsync()
        : await ImagePicker.requestMediaLibraryPermissionsAsync();
      setPermissionPrompt(null);
      if (permission.granted) {
        setPermissionRecovery(null);
        await launchPermissionSource(source);
      } else {
        setPermissionRecovery({ source, canAskAgain: permission.canAskAgain });
      }
    } catch {
      setPermissionPrompt(null);
      setError(t('permissions.requestFailed'));
    } finally {
      setPermissionBusy(false);
    }
  };

  const openDeviceSettings = async () => {
    setError(null);
    try {
      await Linking.openSettings();
    } catch {
      setError(t('permissions.settingsFailed'));
    }
  };

  const chooseFile = async () => {
    setError(null);
    setPermissionPrompt(null);
    setPermissionRecovery(null);
    try {
      const result = await DocumentPicker.getDocumentAsync({
        type: ['image/jpeg', 'image/png', 'image/webp'],
        copyToCacheDirectory: true,
        multiple: false,
      });
      if (result.canceled) return;
      const asset = result.assets[0];
      if (!asset) return;
      const selection = normalizeSelection(asset.uri, asset.mimeType, asset.name, asset.size);
      setReceipt(selection);
      if (!selection) setError('Use a JPEG, PNG, or WebP receipt image.');
    } catch {
      setError('The file picker could not be opened. Take a receipt photo or try again.');
    }
  };

  const submitText = async () => {
    const normalized = input.trim();
    if (!normalized) {
      setError('Enter a short money phrase before continuing.');
      return;
    }
    setError(null);
    setConfirmed(null);
    setPhase('creating');
    try {
      if (textRequest.current?.input !== normalized) {
        textRequest.current = { input: normalized, key: Crypto.randomUUID() };
      }
      const draft = await api.createTextDraft(normalized, textRequest.current.key);
      setDraft(draft);
      router.push('/draft');
    } catch (reason) {
      setError(
        reason instanceof ApiProblem
          ? reason.message
          : 'The draft could not be created. Your phrase is preserved; try again.',
      );
    } finally {
      setPhase('idle');
    }
  };

  const uploadReceipt = async () => {
    if (!receipt) {
      setError('Take a receipt photo or choose a receipt file first.');
      return;
    }
    if (receipt.sizeBytes && receipt.sizeBytes > maximumReceiptSize) {
      setError('Receipt images must be 10 MB or smaller.');
      return;
    }
    setError(null);
    setConfirmed(null);
    const controller = new AbortController();
    abortController.current = controller;
    try {
      setPhase('uploading');
      if (receiptRequest.current?.uri !== receipt.uri) {
        receiptRequest.current = { uri: receipt.uri, key: Crypto.randomUUID() };
      }
      let status = await api.uploadReceipt(receipt, receiptRequest.current.key, controller.signal);
      setPhase('processing');
      for (let attempt = 0; attempt < 30; attempt += 1) {
        if (status.status === 'ocr_failed') throw new Error('Receipt analysis failed. Try another image or enter the transaction instead.');
        if (status.status === 'ocr_completed') {
          try {
            const draft = await api.getReceiptDraft(status.receiptId, controller.signal);
            setDraft(draft);
            router.push('/draft');
            return;
          } catch (reason) {
            if (!(reason instanceof ApiProblem) || reason.status !== 404) throw reason;
          }
        }
        await wait(1500, controller.signal);
        status = await api.getReceipt(status.receiptId, controller.signal);
      }
      throw new Error('Receipt analysis is taking longer than expected. Try again to resume safely.');
    } catch (reason) {
      if (!(reason instanceof Error && reason.name === 'AbortError')) {
        setError(
          reason instanceof ApiProblem || reason instanceof Error
            ? reason.message
            : 'The receipt could not be processed. Try again or enter the transaction.',
        );
      }
    } finally {
      abortController.current = null;
      setPhase('idle');
    }
  };

  return (
    <ScreenScaffold centered={false}>
      <View style={styles.heading}>
        <Text accessibilityRole="header" style={[typography.title, styles.title]}>Add transaction</Text>
        <Text style={[typography.body, styles.supporting]}>Describe one expense or income. You will review every field before it is confirmed.</Text>
      </View>
      {error ? <StatusBanner>{error}</StatusBanner> : null}
      <TextField
        label="Money phrase"
        value={input}
        onChangeText={(value) => {
          setInput(value);
          setError(null);
        }}
        placeholder="Coffee 4.50"
        multiline
        numberOfLines={4}
        editable={!busy}
        style={styles.multiline}
      />
      <PrimaryButton label="Review phrase" loading={phase === 'creating'} disabled={busy || !input.trim()} onPress={() => void submitText()} />
      <View style={styles.dividerRow}>
        <View style={styles.divider} />
        <Text style={[typography.small, styles.supporting]}>or use a receipt</Text>
        <View style={styles.divider} />
      </View>
      <StatusBanner tone="info">{t('permissions.receiptPrivacy')}</StatusBanner>
      <View style={styles.actions}>
        <SecondaryButton label={t('permissions.camera')} disabled={busy || permissionBusy} onPress={() => void inspectPermission('camera')} />
        <SecondaryButton label={t('permissions.gallery')} disabled={busy || permissionBusy} onPress={() => void inspectPermission('gallery')} />
        <SecondaryButton label={t('permissions.files')} disabled={busy || permissionBusy} onPress={() => void chooseFile()} />
      </View>
      {permissionPrompt ? (
        <View style={styles.permissionFlow}>
          <Text accessibilityRole="header" style={[typography.heading, styles.title]}>
            {permissionPrompt === 'camera'
              ? t('permissions.cameraTitle')
              : t('permissions.galleryTitle')}
          </Text>
          <Text style={[typography.body, styles.supporting]}>
            {permissionPrompt === 'camera'
              ? t('permissions.cameraRationale')
              : t('permissions.galleryRationale')}
          </Text>
          <PrimaryButton
            label={t('permissions.continue')}
            loading={permissionBusy}
            onPress={() => void requestPermission(permissionPrompt)}
          />
          <SecondaryButton
            label={t('permissions.notNow')}
            disabled={permissionBusy}
            onPress={() => setPermissionPrompt(null)}
          />
        </View>
      ) : null}
      {permissionRecovery ? (
        <View style={styles.permissionFlow}>
          <StatusBanner tone="warning">
            {permissionRecovery.source === 'camera'
              ? t('permissions.cameraDenied')
              : t('permissions.galleryDenied')}
          </StatusBanner>
          {permissionRecovery.canAskAgain ? (
            <SecondaryButton
              label={t('permissions.tryAgain')}
              disabled={permissionBusy}
              onPress={() => void requestPermission(permissionRecovery.source)}
            />
          ) : (
            <SecondaryButton
              label={t('permissions.openSettings')}
              disabled={permissionBusy}
              onPress={() => void openDeviceSettings()}
            />
          )}
          <SecondaryButton
            label={permissionRecovery.source === 'camera'
              ? t('permissions.useGallery')
              : t('permissions.useCamera')}
            disabled={permissionBusy}
            onPress={() => void inspectPermission(permissionRecovery.source === 'camera' ? 'gallery' : 'camera')}
          />
          <SecondaryButton label={t('permissions.useFiles')} disabled={permissionBusy} onPress={() => void chooseFile()} />
        </View>
      ) : null}
      {receipt ? (
        <View style={styles.preview}>
          <Image accessibilityLabel="Selected receipt preview" source={{ uri: receipt.uri }} style={styles.image} resizeMode="contain" />
          <Text style={[typography.small, styles.supporting]}>Receipt image selected. It is uploaded only when you continue.</Text>
          <PrimaryButton
            label={phase === 'processing' ? 'Analyzing receipt' : 'Upload receipt'}
            loading={phase === 'uploading' || phase === 'processing'}
            disabled={busy}
            onPress={() => void uploadReceipt()}
          />
          {busy ? (
            <SecondaryButton label="Cancel" onPress={() => abortController.current?.abort()} />
          ) : (
            <SecondaryButton label="Remove receipt" onPress={() => setReceipt(null)} />
          )}
        </View>
      ) : null}
      <SecondaryButton label="Back to home" disabled={busy} onPress={() => router.back()} />
    </ScreenScaffold>
  );
}

const styles = StyleSheet.create({
  heading: { gap: theme.spacing.sm },
  title: { color: theme.colors.textPrimary },
  supporting: { color: theme.colors.textSecondary },
  multiline: { minHeight: 112, textAlignVertical: 'top' },
  dividerRow: { flexDirection: 'row', alignItems: 'center', gap: theme.spacing.sm },
  divider: { flex: 1, height: 1, backgroundColor: theme.colors.border },
  actions: { gap: theme.spacing.sm },
  permissionFlow: { gap: theme.spacing.md },
  preview: { gap: theme.spacing.md },
  image: { width: '100%', aspectRatio: 4 / 3, borderRadius: theme.radius.control, backgroundColor: theme.colors.surfaceSubtle },
});
