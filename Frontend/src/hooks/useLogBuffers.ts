"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import LogBufferService, { LogBufferPagedResponse, LogBufferQuery } from "@/services/LogBufferService";

type UseLogBuffersOptions = LogBufferQuery & {
  enabled?: boolean;
};

type LogBufferQueryState = {
  page: number;
  limit: number;
};

const getInitialQuery = (options: UseLogBuffersOptions): LogBufferQueryState => ({
  page: options.page ?? 1,
  limit: options.limit ?? 10,
});

export const useLogBuffers = (options: UseLogBuffersOptions = {}) => {
  const { enabled = true } = options;
  const [query, setQueryState] = useState<LogBufferQueryState>(() => getInitialQuery(options));
  const [response, setResponse] = useState<LogBufferPagedResponse | null>(null);
  const [isLoading, setIsLoading] = useState(enabled);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const requestQuery = useMemo(
    () => ({
      page: query.page,
      limit: query.limit,
    }),
    [query.limit, query.page]
  );

  const setPage = useCallback((page: number) => {
    setIsLoading(true);
    setError(null);
    setQueryState((prev) => ({ ...prev, page }));
  }, []);

  const setLimit = useCallback((limit: number) => {
    setIsLoading(true);
    setError(null);
    setQueryState((prev) => ({ ...prev, limit, page: 1 }));
  }, []);

  const setQuery = useCallback((newQuery: Partial<LogBufferQueryState>) => {
    setIsLoading(true);
    setError(null);
    setQueryState((prev) => ({ ...prev, ...newQuery }));
  }, []);

  const refetch = useCallback(() => {
    setIsLoading(true);
    setError(null);
    setReloadKey((prev) => prev + 1);
  }, []);

  useEffect(() => {
    let ignore = false;

    if (!enabled) return;

    LogBufferService.getAll(requestQuery)
      .then((res) => {
        if (!ignore) {
          setResponse(res);
          setIsLoading(false);
        }
      })
      .catch((err) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "Failed to load log buffers");
          setIsLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [enabled, reloadKey, requestQuery]);

  return {
    data: response?.data ?? [],
    pagination: {
      pageNumber: response?.page_number ?? query.page,
      pageSize: response?.page_size ?? query.limit,
      totalRecords: response?.total_records ?? 0,
      totalPages: response?.total_pages ?? 0,
    },
    isLoading,
    error,
    query,
    setPage,
    setLimit,
    setQuery,
    refetch,
  };
};
