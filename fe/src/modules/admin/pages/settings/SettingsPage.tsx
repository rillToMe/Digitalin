import React from "react";
import { AdminLayout } from "../../../../layouts/AdminLayout";
import { PaymentSettingsCard } from "./components/PaymentSettingsCard";
import { MaintenanceSettingsCard } from "./components/MaintenanceSettingsCard";

export default function SettingsPage() {
  return (
    <AdminLayout title="System Settings" breadcrumb={["Admin", "Settings"]}>
      <div className="max-w-4xl mx-auto space-y-6">
        <div>
          <h2
            style={{
              color: "var(--color-brand-primary)",
              fontWeight: 700,
              fontSize: "1.25rem",
            }}
          >
            System Settings
          </h2>
          <p
            style={{
              color: "var(--color-brand-secondary)",
              fontSize: "0.85rem",
              marginTop: 2,
            }}
          >
            Kelola konfigurasi sistem Digitalin.
          </p>
        </div>

        <PaymentSettingsCard />
        <MaintenanceSettingsCard />
      </div>
    </AdminLayout>
  );
}
