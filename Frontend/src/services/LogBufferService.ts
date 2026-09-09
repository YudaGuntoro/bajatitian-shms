import { apiGet } from "@/lib/api";

export interface LogBuffer {
  id: number;
  device_id: string;
  payload: string;
  time_stamp: string;
}

export interface LogBufferQuery {
  page?: number;
  limit?: number;
}

export interface LogBufferPagedResponse {
  data: LogBuffer[];
  page_number: number;
  page_size: number;
  total_records: number;
  total_pages: number;
}

class LogBufferService {
  async getAll(query: LogBufferQuery = {}): Promise<LogBufferPagedResponse> {
    const params = new URLSearchParams();
    if (query.page) params.append("page", query.page.toString());
    if (query.limit) params.append("limit", query.limit.toString());

    return await apiGet<LogBufferPagedResponse>(`/api/log-buffer?${params.toString()}`);
  }
}

const service = new LogBufferService();
export default service;
