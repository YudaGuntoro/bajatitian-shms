import type { Metadata } from "next";
import UserTable from "@/components/user/UserTable";

export const metadata: Metadata = { title: "User | PT. Baja Titian Utama" };

export default function Page() {
  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-xl font-black text-slate-900 dark:text-white">User</h1>
        <div className="flex items-center gap-2 text-sm font-semibold text-slate-400 dark:text-slate-500">
          <span>Home</span>
          <span className="text-slate-500">&gt;</span>
          <span>Master Data</span>
          <span className="text-slate-500">&gt;</span>
          <span className="text-slate-900 dark:text-white">User</span>
        </div>
      </div>

      <UserTable />
    </div>
  );
}
