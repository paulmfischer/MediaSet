import { useState } from "react";
import type { ImageData } from "~/models";
import { Entity } from "~/models";

type ImageDisplayProps = {
  imageData?: ImageData;
  alt: string;
  entityType?: Entity;
  entityId?: string;
  size?: "xsmall" | "small" | "medium" | "large" | "responsive";
  className?: string;
};

const sizeMap = {
  xsmall: "h-16 w-16",
  small: "h-32 w-32",
  medium: "h-48 w-48",
  large: "h-64 w-64",
  responsive: "h-48 w-48 lg:h-64 lg:w-64",
};

export default function ImageDisplay({
  imageData,
  alt,
  entityType,
  entityId,
  size = "responsive",
  className,
}: ImageDisplayProps) {
  const [showModal, setShowModal] = useState(false);
  const [imageError, setImageError] = useState(false);

  const getImagePath = () => {
    // We need imageData to confirm an image exists, entityType/entityId to construct the URL, and filePath for the direct API path
    if (!imageData || !entityType || !entityId) {
      return null;
    }
    // Construct direct API image URL: /static/images/{filePath}
    const apiUrl = import.meta.env.VITE_API_URL;
    if (!apiUrl) {
      console.warn("ImageDisplay: VITE_API_URL environment variable not set");
      return null;
    }
    const imageUrl = `${apiUrl}/static/images/${imageData.filePath}`;
    return imageUrl;
  };

  const imagePath = getImagePath();

  const handleImageError = () => {
    setImageError(true);
  };

  const handleImageLoad = () => {
    setImageError(false);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Escape") {
      setShowModal(false);
    }
  };

  const placeholderContent = (
    <div className="flex items-center justify-center w-full h-full bg-gray-700 rounded-lg">
      <div className="text-center">
        <svg
          className="mx-auto h-16 w-16 text-gray-400"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
          aria-hidden="true"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"
          />
        </svg>
        <p className="mt-2 text-sm text-gray-400">No image available</p>
      </div>
    </div>
  );

  return (
    <>
      <div
        className={`flex items-center justify-center bg-gray-800 rounded-lg overflow-hidden ${
          sizeMap[size]
        } ${className || ""}`}
        role="img"
        aria-label={alt}
      >
        {imagePath && !imageError ? (
          <button
            type="button"
            onClick={() => setShowModal(true)}
            className="image-button relative w-full h-full"
            aria-label={`View full size image: ${alt}`}
          >
            <img
              src={imagePath}
              alt={alt}
              className="w-full h-full object-cover"
              loading="lazy"
              onError={handleImageError}
              onLoad={handleImageLoad}
            />
          </button>
        ) : (
          placeholderContent
        )}
      </div>

      {showModal && imagePath && (
        <div
          className="fixed inset-0 z-50 bg-black bg-opacity-75 flex items-center justify-center p-4"
          onClick={() => setShowModal(false)}
          onKeyDown={handleKeyDown}
          role="dialog"
          aria-modal="true"
          aria-labelledby="modal-title"
        >
          <div
            className="relative max-w-4xl max-h-screen flex items-center justify-center"
            onClick={(e) => e.stopPropagation()}
          >
            <button
              type="button"
              onClick={() => setShowModal(false)}
              className="absolute top-4 right-4 text-white hover:text-gray-300 focus:outline-none focus:ring-2 focus:ring-blue-400 rounded p-1"
              aria-label="Close image modal"
            >
              <svg
                className="h-6 w-6"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M6 18L18 6M6 6l12 12"
                />
              </svg>
            </button>

            <img
              src={imagePath}
              alt={alt}
              className="max-w-full max-h-screen object-contain rounded-lg"
            />

            {imageData && (
              <div className="absolute bottom-4 left-4 right-4 bg-black bg-opacity-75 rounded-lg p-4 text-white text-sm">
                <p className="font-medium">{imageData.fileName}</p>
                <p className="text-gray-300">
                  {(imageData.fileSize / 1024 / 1024).toFixed(2)} MB
                </p>
              </div>
            )}
          </div>
        </div>
      )}
    </>
  );
}
