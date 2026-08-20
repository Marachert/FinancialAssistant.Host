import { Stack } from 'expo-router';

export default function AnonymousLayout() {
  return <Stack screenOptions={{ headerShown: false }} />;
}
