"use client";

import { useEffect, useRef, useMemo } from "react";
import DataTable, { DataTableColumn } from "@/components/common/DataTable";
import { useLogBuffers } from "@/hooks/useLogBuffers";
import { LogBuffer } from "@/services/LogBufferService";
import { useToast } from "@/context/ToastContext";

const dateFormatter = new Intl.DateTimeFormat("en-GB", {
  day: "2-digit",
  month: "short",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

const formatDate = (value: string) => {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";
  return dateFormatter.format(date);
};

const getErrorMessage = (error: unknown, fallback: string) =>
  error instanceof Error ? error.message : fallback;

export default function LogBufferTable() {
  const toast = useToast();
  const lastErrorRef = useRef<string | null>(null);

  const {
    data,
    error,
    isLoading,
    pagination,
    query,
    refetch,
    setLimit,
    setPage,
  } = useLogBuffers({
    limit: 10,
    page: 1,
  });

  useEffect(() => {
    if (error && error !== lastErrorRef.current) {
      toast.error({ message: getErrorMessage(error, "Failed to load log buffers") });
      lastErrorRef.current = error;
    }
    if (!error) {
      lastErrorRef.current = null;
    }
  }, [error, toast]);

  const columns = useMemo<DataTableColumn<LogBuffer>[]>(
    () => [
      {
        key: "device_id",
        header: "Device Id",
        accessor: "device_id",
        className: "w-[150px]",
        render: (_value, item) => <span className="font-medium text-gray-900 dark:text-white/90">{item.device_id}</span>,
      },
      {
        key: "payload",
        header: "Payload",
        accessor: "payload",
        render: (_value, item) => (
          <span className="text-gray-600 dark:text-gray-400 break-all">{item.payload}</span>
        ),
      },
      {
        key: "time_stamp",
        header: "Time Stamp",
        accessor: "time_stamp",
        className: "w-[200px]",
        render: (_value, item) => (
          <span className="text-gray-600 dark:text-gray-400">{formatDate(item.time_stamp)}</span>
        ),
      },
    ],
    []
  );

  return (
    <div className="space-y-4">
      {/* Table Top Actions */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
          <span>Show</span>
          <select
            className="rounded-lg border border-gray-300 bg-transparent px-2 py-1 outline-none focus:border-brand-300 focus:ring-3 focus:ring-brand-500/10 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
            value={query.limit}
            onChange={(e) => setLimit(Number(e.target.value))}
            disabled={isLoading}
          >
            {[10, 25, 50, 100].map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
          <span>entries</span>
        </div>
        
        <div className="flex items-center gap-3">
          <button
            onClick={() => refetch()}
            disabled={isLoading}
            className="h-10 rounded-lg bg-gray-100 px-4 text-sm font-medium text-gray-700 hover:bg-gray-200 disabled:opacity-50 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700"
          >
            Refresh Data
          </button>
        </div>
      </div>

      <DataTable
        columns={columns}
        data={data}
        emptyMessage="No log buffers found."
        isLoading={isLoading}
        minWidth="760px"
        onPageChange={setPage}
        pagination={{
          limit: pagination.pageSize,
          page: pagination.pageNumber,
          total: pagination.totalRecords,
          totalPage: pagination.totalPages,
        }}
        rowKey="id"
      />
    </div>
  );
}
