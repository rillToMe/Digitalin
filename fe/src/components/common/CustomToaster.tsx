import { CircleAlert, CircleCheck, Info, LoaderCircle, X } from "lucide-react";
import { Toaster } from "sonner";

export default function CustomToaster() {
  return (
    <Toaster
      className="digitalin-toaster"
      position="top-right"
      closeButton
      offset={{ top: 20, right: 20 }}
      mobileOffset={{ top: 12, right: 12, left: 12 }}
      icons={{
        success: <CircleCheck size={18} aria-hidden="true" />,
        error: <CircleAlert size={18} aria-hidden="true" />,
        info: <Info size={18} aria-hidden="true" />,
        loading: <LoaderCircle size={18} aria-hidden="true" />,
        close: <X size={15} aria-hidden="true" />,
      }}
      toastOptions={{ closeButtonAriaLabel: "Tutup notifikasi" }}
    />
  );
}
