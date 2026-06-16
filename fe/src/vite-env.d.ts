/// <reference types="vite/client" />

/** Midtrans Snap payment result object passed to callbacks. */
interface MidtransSnapResult {
  transaction_id: string;
  order_id: string;
  gross_amount: string;
  payment_type: string;
  transaction_status: string;
  fraud_status?: string;
  status_code: string;
  status_message?: string;
}

/** Options passed to window.snap.pay(). */
interface MidtransSnapOptions {
  onSuccess?: (result: MidtransSnapResult) => void;
  onPending?: (result: MidtransSnapResult) => void;
  onError?: (result: MidtransSnapResult) => void;
  onClose?: () => void;
}

interface MidtransSnap {
  pay(token: string, options?: MidtransSnapOptions): void;
}

interface Window {
  snap?: MidtransSnap;
}
