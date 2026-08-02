import Navbar from "../components/common/Navbar";
import Footer from "../components/common/Footer";

interface CustomerLayoutProps {
  children: React.ReactNode;
}

export function CustomerLayout({ children }: CustomerLayoutProps) {
  return (
    <div
      className="flex flex-col"
      style={{ fontFamily: "Inter, sans-serif", backgroundColor: "var(--color-neutral)", minHeight: "100vh" }}
    >
      <Navbar />

      {/* flex-1: halaman pendek (404, empty state) tidak menyisakan celah
          abu-abu di bawah footer. */}
      <main className="flex-1">{children}</main>

      <Footer />
    </div>
  );
}
