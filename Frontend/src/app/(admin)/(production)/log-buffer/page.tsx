import type { Metadata } from "next";
import LogBufferPage from "@/production/LogBufferPage";

export const metadata: Metadata = { title: "Log Buffer | PT. Baja Titian Utama" };

export default function Page() {
  return <LogBufferPage />;
}
