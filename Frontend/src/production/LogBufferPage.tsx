"use client";

import React from "react";
import PageBreadcrumb from "@/components/common/PageBreadCrumb";
import LogBufferTable from "@/components/log-buffer/LogBufferTable";

export default function LogBufferPage() {
  return (
    <div className="space-y-6">
      <PageBreadcrumb pageTitle="Log Buffer" />

      <div className="rounded-2xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-white/[0.03] sm:p-6">
        <div className="mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Device Logs</h2>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Real-time buffer of device payloads and timestamps.
          </p>
        </div>
        
        <LogBufferTable />
      </div>
    </div>
  );
}
