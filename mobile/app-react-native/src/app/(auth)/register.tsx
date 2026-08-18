import { router } from 'expo-router';

import { AuthForm } from '@/features/auth/AuthForm';
import { useAuth } from '@/features/auth/AuthProvider';

export default function RegisterScreen() {
  const { register } = useAuth();
  return <AuthForm mode="register" onAlternate={() => router.replace('/sign-in')} onSubmit={register} />;
}
