import type { Metadata } from "next";
import ProductionDashboard from "@/production/ProductionDashboard";

export const metadata: Metadata = {
  title: "SHMS-System | PT. Baja Titian Utama",
  description: "SHMS-System and inspection monitoring dashboard",
};

export default function SHMSSystemHome() {
  return <ProductionDashboard />;
}
