"use client";

import dynamic from "next/dynamic";
import React from "react";

// Dynamically import the map to avoid SSR issues with Leaflet
const MapClient = dynamic(() => import("./MapClient"), {
  ssr: false,
  loading: () => (
    <div className="flex h-full w-full items-center justify-center bg-gray-100 dark:bg-gray-800">
      <span className="text-gray-500 font-medium">Loading Map...</span>
    </div>
  ),
});

type LocationMapProps = {
  bridgeName?: string;
  center: [number, number];
  zoom?: number;
};

export default function LocationMap({ bridgeName, center, zoom = 14 }: LocationMapProps) {
  return (
    <div className="h-[600px] w-full overflow-hidden rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm">
      <MapClient bridgeName={bridgeName} center={center} zoom={zoom} />
    </div>
  );
}
