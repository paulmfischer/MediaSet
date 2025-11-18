import { useRef, useState } from "react";
import type { FormProps, ImageData } from "~/models";

type ImageUploadProps = FormProps & {
  name: string;
  existingImage?: ImageData;
  onImageSelected?: (file: File, preview: string) => void;
};

export default function ImageUpload(props: ImageUploadProps) {
  const [previewUrl, setPreviewUrl] = useState<string | null>(
    props.existingImage?.filePath ? `${import.meta.env.VITE_API_URL}/static/images/${props.existingImage.filePath}` : null
  );
  const [fileName, setFileName] = useState<string>(
    props.existingImage?.fileName ?? ""
  );
  const [dragActive, setDragActive] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [imageCleared, setImageCleared] = useState(false);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const ALLOWED_TYPES = ["image/jpeg", "image/png"];
  const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB

  const validateFile = (file: File): string | null => {
    if (!ALLOWED_TYPES.includes(file.type)) {
      return "Only JPEG and PNG images are allowed";
    }
    if (file.size > MAX_FILE_SIZE) {
      return "File size must be less than 5MB";
    }
    return null;
  };

  const handleFile = (file: File) => {
    const validationError = validateFile(file);
    if (validationError) {
      setError(validationError);
      setPreviewUrl(null);
      setFileName("");
      return;
    }

    setError(null);
    setFileName(file.name);

    const reader = new FileReader();
    reader.onload = (e) => {
      const preview = e.target?.result as string;
      setPreviewUrl(preview);
      props.onImageSelected?.(file, preview);
    };
    reader.readAsDataURL(file);
  };

  const handleFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      // Clear the clear marker since a new file is selected
      setImageCleared(false);
      handleFile(file);
    }
  };

  const handleDrag = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.type === "dragenter" || e.type === "dragover") {
      setDragActive(true);
    } else if (e.type === "dragleave") {
      setDragActive(false);
    }
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);

    const file = e.dataTransfer.files?.[0];
    if (file) {
      handleFile(file);
    }
  };

  const handleClear = () => {
    setPreviewUrl(null);
    setFileName("");
    setError(null);
    setImageCleared(true);
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  return (
    <div className="w-full space-y-4">
      <label className="block text-sm font-medium text-gray-200">
        Cover Image
      </label>

      <input
        ref={fileInputRef}
        type="file"
        name={props.name}
        accept="image/jpeg,image/png"
        onChange={handleFileInputChange}
        disabled={props.isSubmitting}
        className="hidden"
        aria-label="Upload cover image"
      />

      <input
        id={`${props.name}-clear-marker`}
        type="hidden"
        name={`${props.name}-cleared`}
        value={imageCleared ? "true" : ""}
      />

      <div
        onDragEnter={handleDrag}
        onDragLeave={handleDrag}
        onDragOver={handleDrag}
        onDrop={handleDrop}
        onClick={() => fileInputRef.current?.click()}
        className={`relative border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-colors ${
          dragActive
            ? "border-blue-400 bg-blue-900 bg-opacity-30"
            : "border-gray-600 bg-gray-800 hover:border-gray-500 hover:bg-gray-700"
        } ${props.isSubmitting ? "opacity-50 cursor-not-allowed" : ""}`}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            fileInputRef.current?.click();
          }
        }}
        aria-label="Drag and drop area for image upload"
      >
        {previewUrl ? (
          <div className="space-y-2">
            <img
              src={previewUrl}
              alt="Preview"
              className="mx-auto h-40 w-auto object-contain"
            />
            <p className="text-sm text-gray-300">{fileName}</p>
          </div>
        ) : (
          <div className="space-y-2">
            <svg
              className="mx-auto h-12 w-12 text-gray-400"
              stroke="currentColor"
              fill="none"
              viewBox="0 0 48 48"
              aria-hidden="true"
            >
              <path
                d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-8l-3.172-3.172a4 4 0 00-5.656 0L28 20M8 28l3.172-3.172a4 4 0 015.656 0L28 28"
                strokeWidth={2}
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
            <div className="text-sm text-gray-300">
              <p className="font-medium">Drag and drop your image here</p>
              <p className="text-xs text-gray-400">or click to select</p>
            </div>
            <p className="text-xs text-gray-400">
              PNG or JPEG up to 5MB
            </p>
          </div>
        )}
      </div>

      {error && (
        <div
          className="rounded-md bg-red-50 p-4 border border-red-200"
          role="alert"
        >
          <p className="text-sm font-medium text-red-800">{error}</p>
        </div>
      )}

      {previewUrl && (
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={props.isSubmitting}
            className="tertiary disabled:cursor-not-allowed"
          >
            Replace
          </button>
          <button
            type="button"
            onClick={handleClear}
            disabled={props.isSubmitting}
            className="tertiary disabled:cursor-not-allowed"
          >
            Clear
          </button>
        </div>
      )}
    </div>
  );
}
