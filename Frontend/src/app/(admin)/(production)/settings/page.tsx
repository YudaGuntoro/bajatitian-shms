import type { Metadata } from "next";
import SettingPage from "@/production/SettingPage";

export const metadata: Metadata = { title: "Setting | PT. Baja Titian Utama" };

export default function Page() {
  return <SettingPage />;
}
