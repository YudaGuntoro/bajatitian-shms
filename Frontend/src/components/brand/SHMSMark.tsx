type SHMSMarkProps = {
  className?: string;
  variant?: "auto" | "dark" | "light";
};

export default function SHMSMark({ className = "", variant = "auto" }: SHMSMarkProps) {
  if (variant === "dark") {
    return <img alt="Baja Titian Utama" className={className} src="/images/logo/baja-titian-logo.png" />;
  }

  if (variant === "light") {
    return <img alt="Baja Titian Utama" className={className} src="/images/logo/baja-titian-logo-light.png" />;
  }

  return (
    <>
      <img alt="Baja Titian Utama" className={`${className} dark:hidden`} src="/images/logo/baja-titian-logo-light.png" />
      <img alt="Baja Titian Utama" className={`${className} hidden dark:block`} src="/images/logo/baja-titian-logo.png" />
    </>
  );
}
