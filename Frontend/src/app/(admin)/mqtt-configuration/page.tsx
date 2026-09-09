import type { Metadata } from "next";
import MqttConfigurationPage from "@/production/MqttConfigurationPage";

export const metadata: Metadata = { title: "MQTT Configuration | PT. Baja Titian Utama" };

export default function Page() {
  return <MqttConfigurationPage />;
}
