import type { Metadata } from "next";
import { redirect } from "next/navigation";

export const metadata: Metadata = { title: "Setting | PT. Baja Titian Utama" };

export default function Page() {
  redirect("/settings/display");
}
