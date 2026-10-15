import { notFound } from "next/navigation";
import SettingPage, { type SettingsSection } from "@/production/SettingPage";
import StorageSettingsPage from "@/production/StorageSettingsPage";

export const metadata = { title: "Setting | PT. Baja Titian Utama" };

export default async function Page({ params }: { params: Promise<{ section: string }> }) {
  const { section } = await params;
  if (section === "storage") return <StorageSettingsPage />;
  if (!["display", "server", "backup", "alerts", "system"].includes(section)) notFound();
  return <SettingPage section={section as SettingsSection} />;
}
