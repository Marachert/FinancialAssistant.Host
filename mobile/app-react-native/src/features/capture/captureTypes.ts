export type TransactionType = 'expense' | 'income';

export type TransactionDraft = {
  id: string;
  status: string;
  revision: number;
  inputSource: string;
  type: string;
  amount: number | null;
  currency: string | null;
  categoryId: string | null;
  merchant: string | null;
  date: string | null;
  confidence: number;
  ambiguities: string[];
  requiresReview: boolean;
  suggestion: {
    source: string;
    sourceReferenceId: string | null;
    outputAuthority: string;
    confidence: number;
    ambiguities: string[];
    missingFields: string[];
    reviewMessage: string;
  };
  createdAtUtc: string;
  note: string | null;
};

export type DraftUpdate = {
  expectedRevision: number;
  type: TransactionType;
  amount: number | null;
  currency: string | null;
  categoryId: string | null;
  merchant: string | null;
  date: string | null;
  note: string | null;
};

export type ConfirmedTransaction = {
  transactionId: string;
  draftId: string;
  status: string;
  transactionType: TransactionType;
  amount: number;
  currency: string;
  categoryId: string;
  merchant: string | null;
  date: string;
  confirmedAtUtc: string;
};

export type ReceiptSelection = {
  uri: string;
  name: string;
  contentType: 'image/jpeg' | 'image/png' | 'image/webp';
  sizeBytes?: number;
};

export type ReceiptStatus = {
  receiptId: string;
  status: string;
  contentType: string;
  sizeBytes: number;
  ocrConfidence: number | null;
  ocrAmbiguities: string[];
  uploadedAtUtc: string;
};
