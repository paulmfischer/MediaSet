import { useEffect, useState } from "react";

type Props = {
  inputId?: string;
  existingUrl?: string;
  alt?: string;
};

export default function ImageUrlPreview({ inputId = "imageUrl", existingUrl, alt = "Image Preview" }: Props) {
  const [previewUrl, setPreviewUrl] = useState<string | null>(existingUrl ?? null);
  const [error, setError] = useState(false);

  useEffect(() => {
    const el = document.getElementById(inputId) as HTMLInputElement | null;

    const handler = () => {
      // Prefer the input value if present, otherwise fall back to the existingUrl prop
      const val = el?.value?.trim() ?? existingUrl?.trim() ?? "";
      if (!val) {
        setPreviewUrl(null);
        setError(false);
        return;
      }
      setPreviewUrl(val);
      setError(false);
    };

    // Listen for both input and change events to catch programmatic updates
    el?.addEventListener("input", handler);
    el?.addEventListener("change", handler);

    // Initialize from the input value if present (takes precedence over existingUrl)
    handler();

    return () => {
      el?.removeEventListener("input", handler);
      el?.removeEventListener("change", handler);
    };
  }, [inputId, existingUrl]);

  if (!previewUrl) {
    return null;
  }

  return (
    <div className="h-full flex items-center" data-testid="image-url-preview">
      <div className="w-32 flex flex-col items-center">
        <img
          src={previewUrl}
          alt={alt}
          className="h-24 w-auto object-contain"
          onError={() => setError(true)}
          onLoad={() => setError(false)}
        />
        {error ? (
          <p className="text-xs text-red-400 mt-1">Unable to load image</p>
        ) : null}
      </div>
    </div>
  );
}
