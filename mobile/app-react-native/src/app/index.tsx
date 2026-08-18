import { Redirect } from 'expo-router';

import { useAuth } from '@/features/auth/AuthProvider';

export default function IndexScreen() {
  const { state } = useAuth();
  return <Redirect href={state === 'authenticated' ? '/home' : '/welcome'} />;
}
