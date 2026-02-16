import { Shell } from "lucide-react";

export default function Spinner({ size }: { size?: number }) {
  return <Shell size={size} className="animate-spin" />;
}
