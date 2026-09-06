/* Disclosure wajib reCAPTCHA v3 (badge disembunyikan di globals.css).
   Teks dan link mengikuti redaksi Google — jangan diterjemahkan/diubah. */
export default function RecaptchaNotice() {
  return (
    <p className="recaptcha-notice">
      This site is protected by reCAPTCHA and the{" "}
      <a
        href="https://policies.google.com/privacy"
        target="_blank"
        rel="noopener noreferrer"
      >
        Google Privacy Policy
      </a>{" "}
      and{" "}
      <a
        href="https://policies.google.com/terms"
        target="_blank"
        rel="noopener noreferrer"
      >
        Terms of Service
      </a>{" "}
      apply.
    </p>
  );
}
