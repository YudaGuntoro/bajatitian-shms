import SignInPage from "@/auth/SignInPage";
import { Metadata } from "next";

export const metadata: Metadata = {
  title: "Sign In | STRUCTURAL HEALTH MONITORING SYSTEM",
  description: "STRUCTURAL HEALTH MONITORING SYSTEM sign in",
};

export const dynamic = "force-dynamic";
export const revalidate = 0;

export default function SignIn() {
  return <SignInPage />;
}
