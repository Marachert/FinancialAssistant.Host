import { Stack } from 'expo-router';

import { CaptureProvider } from '@/features/capture/CaptureProvider';
import { InsightsProvider } from '@/features/insights/InsightsProvider';

export default function SignedInLayout() {
  return (
    <InsightsProvider>
      <CaptureProvider>
        <Stack screenOptions={{ headerShown: false }} />
      </CaptureProvider>
    </InsightsProvider>
  );
}
