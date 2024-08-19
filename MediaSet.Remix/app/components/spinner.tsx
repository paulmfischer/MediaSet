import { IconInnerShadowTop } from "@tabler/icons-react";

export default function Spinner({ size }: { size?: number }) {
  return (
    <IconInnerShadowTop size={size} className="animate-spin" />
  )
}