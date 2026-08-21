import type { RequestOptions } from '@/api/client';

import type {
  ConfirmedTransaction,
  DraftUpdate,
  ReceiptSelection,
  ReceiptStatus,
  TransactionDraft,
} from './captureTypes';

type Request = <T>(path: string, options?: RequestOptions) => Promise<T>;

export function createCaptureApi(request: Request) {
  return {
    createTextDraft: (input: string, idempotencyKey: string) =>
      request<TransactionDraft>('/transactions/intake', {
        method: 'POST',
        headers: { 'Idempotency-Key': idempotencyKey },
        body: JSON.stringify({ input }),
      }),

    uploadReceipt: (selection: ReceiptSelection, idempotencyKey: string, signal: AbortSignal) => {
      const form = new FormData();
      form.append(
        'file',
        {
          uri: selection.uri,
          name: selection.name,
          type: selection.contentType,
        } as unknown as Blob,
      );
      return request<ReceiptStatus>('/receipts', {
        method: 'POST',
        headers: { 'Idempotency-Key': idempotencyKey },
        body: form,
        signal,
      });
    },

    getReceipt: (receiptId: string, signal: AbortSignal) =>
      request<ReceiptStatus>(`/receipts/${encodeURIComponent(receiptId)}`, { signal }),

    getReceiptDraft: (receiptId: string, signal: AbortSignal) =>
      request<TransactionDraft>(
        `/transactions/drafts/receipts/${encodeURIComponent(receiptId)}`,
        { signal },
      ),

    getDraft: (draftId: string) =>
      request<TransactionDraft>(`/transactions/drafts/${encodeURIComponent(draftId)}`),

    updateDraft: (draftId: string, update: DraftUpdate) =>
      request<TransactionDraft>(`/transactions/drafts/${encodeURIComponent(draftId)}`, {
        method: 'PUT',
        body: JSON.stringify(update),
      }),

    confirmDraft: (draftId: string) =>
      request<ConfirmedTransaction>(
        `/transactions/drafts/${encodeURIComponent(draftId)}/confirm`,
        { method: 'POST' },
      ),
  };
}
