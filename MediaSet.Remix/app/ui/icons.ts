// The icons the legacy app used (the migration keeps them all; later tickets consume them).
// Each is imported from its own lucide module so only these icon nodes are ever loaded, never
// the full ~3,800 icon set. lucide has no "exports" map or per-icon entry point, so the
// `dist/esm/icons/*.mjs` paths are the only way to avoid loading the whole set; if a lucide
// upgrade moves them, `npm run typecheck` fails here. Home, Loader2, Music, Album and Trash2
// are renamed upstream, so they map to the current file names. Keep the list alphabetical.
export { default as Album } from 'lucide/dist/esm/icons/square-bookmark.mjs';
export { default as AlertTriangle } from 'lucide/dist/esm/icons/triangle-alert.mjs';
export { default as BookOpen } from 'lucide/dist/esm/icons/book-open.mjs';
export { default as Check } from 'lucide/dist/esm/icons/check.mjs';
export { default as ChevronDown } from 'lucide/dist/esm/icons/chevron-down.mjs';
export { default as ChevronLeft } from 'lucide/dist/esm/icons/chevron-left.mjs';
export { default as ChevronRight } from 'lucide/dist/esm/icons/chevron-right.mjs';
export { default as ChevronUp } from 'lucide/dist/esm/icons/chevron-up.mjs';
export { default as Clapperboard } from 'lucide/dist/esm/icons/clapperboard.mjs';
export { default as Disc } from 'lucide/dist/esm/icons/disc.mjs';
export { default as Files } from 'lucide/dist/esm/icons/files.mjs';
export { default as FileText } from 'lucide/dist/esm/icons/file-text.mjs';
export { default as Gamepad2 } from 'lucide/dist/esm/icons/gamepad-2.mjs';
export { default as HardDrive } from 'lucide/dist/esm/icons/hard-drive.mjs';
export { default as Hash } from 'lucide/dist/esm/icons/hash.mjs';
export { default as Home } from 'lucide/dist/esm/icons/house.mjs';
export { default as ImageOff } from 'lucide/dist/esm/icons/image-off.mjs';
export { default as Layers } from 'lucide/dist/esm/icons/layers.mjs';
export { default as LibraryBig } from 'lucide/dist/esm/icons/library-big.mjs';
export { default as Link2Off } from 'lucide/dist/esm/icons/link-2-off.mjs';
export { default as Loader2 } from 'lucide/dist/esm/icons/loader-circle.mjs';
export { default as Menu } from 'lucide/dist/esm/icons/menu.mjs';
export { default as Monitor } from 'lucide/dist/esm/icons/monitor.mjs';
export { default as Music } from 'lucide/dist/esm/icons/eighth-notes.mjs';
export { default as Pencil } from 'lucide/dist/esm/icons/pencil.mjs';
export { default as Plus } from 'lucide/dist/esm/icons/plus.mjs';
export { default as Settings } from 'lucide/dist/esm/icons/settings.mjs';
export { default as Shell } from 'lucide/dist/esm/icons/shell.mjs';
export { default as Tag } from 'lucide/dist/esm/icons/tag.mjs';
export { default as Trash2 } from 'lucide/dist/esm/icons/trash.mjs';
export { default as Tv } from 'lucide/dist/esm/icons/tv.mjs';
export { default as Users } from 'lucide/dist/esm/icons/users.mjs';
export { default as X } from 'lucide/dist/esm/icons/x.mjs';
