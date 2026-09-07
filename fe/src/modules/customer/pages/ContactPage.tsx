import { Mail, Send, MessageCircle, Clock } from "lucide-react";

import BackButton from "../../../components/common/BackButton";
import type { ContactItem } from "../../../api/index";

const CONTACT_DATA: ContactItem[] = [
  {
    icon: <Mail size={22} style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }} />,
    label: "Email Support",
    value: "support@digitalin.id",
    link: "mailto:support@digitalin.id",
    description: "Kirim pertanyaan atau keluhan melalui email. Tim kami akan merespons dalam 1×24 jam.",
  },
  {
    icon: <Send size={22} style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }} />,
    label: "Telegram",
    value: "@digitalin_support",
    link: "https://t.me/digitalin_support",
    description: "Chat langsung dengan tim support kami melalui Telegram untuk respon lebih cepat.",
  },
  {
    icon: <MessageCircle size={22} style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }} />,
    label: "WhatsApp",
    value: "+62 812-3456-7890",
    link: "https://wa.me/6281234567890",
    description: "Hubungi kami via WhatsApp untuk bantuan cepat terkait transaksi atau masalah akun.",
  },
  {
    icon: <Clock size={22} style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }} />,
    label: "Jam Operasional",
    value: "Senin – Minggu, 08:00 – 23:00 WIB",
    description: "Tim support kami siap membantu setiap hari termasuk hari libur nasional.",
  },
];

export default function ContactPage() {
  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      <BackButton />
      {/* Header */}
      <div className="text-center mb-10">
        <div className="flex justify-center mb-4">
          <div
            className="w-14 h-14 rounded-2xl flex items-center justify-center"
            style={{ backgroundColor: "rgba(44,94,245,0.1)" }}
          >
            <MessageCircle size={28} style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }} />
          </div>
        </div>
        <h1 style={{ color: "var(--color-brand-primary, #0F1419)", fontWeight: 700, fontSize: "1.5rem", marginBottom: 8 }}>
          Hubungi Kami
        </h1>
        <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.9rem", maxWidth: 480, margin: "0 auto" }}>
          Butuh bantuan atau memiliki pertanyaan? Tim kami siap membantu Anda melalui berbagai channel berikut.
        </p>
      </div>

      {/* Contact Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
        {CONTACT_DATA.map((contact) => {
          const Wrapper = contact.link ? "a" : "div";
          return (
            <Wrapper
              key={contact.label}
              href={contact.link}
              target={contact.link ? "_blank" : undefined}
              rel={contact.link ? "noopener noreferrer" : undefined}
              className="rounded-2xl p-6 no-underline transition-all hover:shadow-md"
              style={{
                backgroundColor: "var(--color-surface, #fff)",
                border: "1px solid rgba(0,0,0,0.07)",
                display: "block",
              }}
            >
              <div className="flex items-start gap-4">
                <div
                  className="flex-shrink-0 w-11 h-11 rounded-xl flex items-center justify-center"
                  style={{ backgroundColor: "rgba(44,94,245,0.08)" }}
                >
                  {contact.icon}
                </div>
                <div className="flex-1 min-w-0">
                  <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.78rem", marginBottom: 4 }}>
                    {contact.label}
                  </p>
                  <p
                    className="truncate"
                    style={{ color: "var(--color-brand-primary, #0F1419)", fontWeight: 600, fontSize: "0.9rem", marginBottom: 8 }}
                  >
                    {contact.value}
                  </p>
                  <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.82rem", lineHeight: 1.5 }}>
                    {contact.description}
                  </p>
                </div>
              </div>
            </Wrapper>
          );
        })}
      </div>

      {/* Additional info */}
      <div
        className="rounded-2xl p-6 mt-8 text-center"
        style={{ backgroundColor: "rgba(44,94,245,0.05)", border: "1px solid rgba(44,94,245,0.12)" }}
      >
        <p style={{ color: "var(--color-brand-primary, #0F1419)", fontWeight: 600, fontSize: "0.95rem", marginBottom: 6 }}>
          Pertanyaan tentang transaksi?
        </p>
        <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.85rem" }}>
          Sertakan nomor invoice (INV-XXXXXXXX-XXXX) agar tim kami dapat membantu lebih cepat.
        </p>
      </div>
    </div>
  );
}
