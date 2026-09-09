import type { Metadata } from "next";
import LocationPage from "@/production/LocationPage";

export const metadata: Metadata = { title: "Location | PT. Baja Titian Utama" };

export default function Page() {
  return <LocationPage />;
}
