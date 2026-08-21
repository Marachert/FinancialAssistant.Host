import { createContext, useContext, useMemo, useState, type PropsWithChildren } from 'react';

import { useAuth } from '@/features/auth/AuthProvider';

import { createCaptureApi } from './captureApi';
import type { ConfirmedTransaction, ReceiptSelection, TransactionDraft } from './captureTypes';

type CaptureContextValue = {
  api: ReturnType<typeof createCaptureApi>;
  input: string;
  setInput: (input: string) => void;
  receipt: ReceiptSelection | null;
  setReceipt: (receipt: ReceiptSelection | null) => void;
  draft: TransactionDraft | null;
  setDraft: (draft: TransactionDraft | null) => void;
  confirmed: ConfirmedTransaction | null;
  setConfirmed: (confirmed: ConfirmedTransaction | null) => void;
  reset: () => void;
};

const CaptureContext = createContext<CaptureContextValue | null>(null);

export function CaptureProvider({ children }: PropsWithChildren) {
  const { request } = useAuth();
  const [input, setInput] = useState('');
  const [receipt, setReceipt] = useState<ReceiptSelection | null>(null);
  const [draft, setDraft] = useState<TransactionDraft | null>(null);
  const [confirmed, setConfirmed] = useState<ConfirmedTransaction | null>(null);
  const api = useMemo(() => createCaptureApi(request), [request]);

  const value = useMemo<CaptureContextValue>(
    () => ({
      api,
      input,
      setInput,
      receipt,
      setReceipt,
      draft,
      setDraft,
      confirmed,
      setConfirmed,
      reset: () => {
        setInput('');
        setReceipt(null);
        setDraft(null);
        setConfirmed(null);
      },
    }),
    [api, confirmed, draft, input, receipt],
  );

  return <CaptureContext.Provider value={value}>{children}</CaptureContext.Provider>;
}

export function useCapture(): CaptureContextValue {
  const context = useContext(CaptureContext);
  if (!context) throw new Error('useCapture must be used within CaptureProvider.');
  return context;
}
