import { router } from 'expo-router';

import { AuthForm } from '@/features/auth/AuthForm';
import { useAuth } from '@/features/auth/AuthProvider';

export default function SignInScreen() {
  const { signIn } = useAuth();
  return <AuthForm mode="sign-in" onAlternate={() => router.replace('/register')} onSubmit={signIn} />;
}
