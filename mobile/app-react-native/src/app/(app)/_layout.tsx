import { Stack } from 'expo-router';

import { CaptureProvider } from '@/features/capture/CaptureProvider';

export default function SignedInLayout() {
  return (
    <CaptureProvider>
      <Stack screenOptions={{ headerShown: false }} />
    </CaptureProvider>
  );
}
