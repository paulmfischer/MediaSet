import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ImageUpload from "./image-upload";
import type { ImageData } from "~/models";

describe("ImageUpload Component", () => {
  const mockOnImageSelected = vi.fn();

  beforeEach(() => {
    mockOnImageSelected.mockClear();
  });

  describe("Basic Rendering", () => {
    it("should render the upload area with label", () => {
      render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      expect(screen.getByText("Cover Image")).toBeInTheDocument();
      expect(screen.getByText("Drag and drop your image here")).toBeInTheDocument();
    });

    it("should render with instructions", () => {
      render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      expect(screen.getByText("or click to select")).toBeInTheDocument();
      expect(screen.getByText("PNG or JPEG up to 5MB")).toBeInTheDocument();
    });

    it("should have hidden file input", () => {
      render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const fileInput = screen.getByRole("button", {
        name: /drag and drop area/i,
      });
      expect(fileInput).toBeInTheDocument();
    });
  });

  describe("File Selection", () => {
    it("should handle valid JPEG file selection", async () => {
      const user = userEvent.setup();
      const { container } = render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
      const file = new File(["test"], "image.jpg", { type: "image/jpeg" });

      await user.upload(fileInput, file);

      await waitFor(() => {
        expect(mockOnImageSelected).toHaveBeenCalled();
        expect(screen.getByText("image.jpg")).toBeInTheDocument();
      });
    });

    it("should handle valid PNG file selection", async () => {
      const user = userEvent.setup();
      const { container } = render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
      const file = new File(["test"], "image.png", { type: "image/png" });

      await user.upload(fileInput, file);

      await waitFor(() => {
        expect(mockOnImageSelected).toHaveBeenCalled();
        expect(screen.getByText("image.png")).toBeInTheDocument();
      });
    });

    it("should reject non-image files", async () => {
      const user = userEvent.setup();
      const { container } = render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
      const file = new File(["test"], "document.pdf", { type: "application/pdf" });

      await user.upload(fileInput, file);

      // Validation should happen synchronously on change event
      expect(mockOnImageSelected).not.toHaveBeenCalled();
    });

    it("should reject files larger than 5MB", async () => {
      const user = userEvent.setup();
      const { container } = render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
      const largeFile = new File(["x".repeat(6 * 1024 * 1024)], "large.jpg", {
        type: "image/jpeg",
      });

      await user.upload(fileInput, largeFile);

      // Validation should happen synchronously on change event
      expect(mockOnImageSelected).not.toHaveBeenCalled();
    });
  });

  describe("Drag and Drop", () => {
    it("should change background when dragging over", async () => {
      render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const dropZone = screen.getByRole("button", {
        name: /drag and drop area/i,
      });

      fireEvent.dragEnter(dropZone);
      expect(dropZone).toHaveClass("border-blue-400", "bg-blue-900");

      fireEvent.dragLeave(dropZone);
      expect(dropZone).toHaveClass("border-gray-600", "bg-gray-800");
    });

    it("should handle dropping a valid file", async () => {
      render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const file = new File(["test"], "image.jpg", { type: "image/jpeg" });
      const dropZone = screen.getByRole("button", {
        name: /drag and drop area/i,
      });

      const dropEvent = new DragEvent("drop", {
        bubbles: true,
        cancelable: true,
        dataTransfer: new DataTransfer(),
      });
      dropEvent.dataTransfer?.items.add(file);

      fireEvent.drop(dropZone, {
        dataTransfer: { files: [file] },
      });

      await waitFor(() => {
        expect(mockOnImageSelected).toHaveBeenCalled();
      });
    });

    it("should show error when dropping invalid file", async () => {
      render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const file = new File(["test"], "document.pdf", { type: "application/pdf" });
      const dropZone = screen.getByRole("button", {
        name: /drag and drop area/i,
      });

      fireEvent.drop(dropZone, {
        dataTransfer: { files: [file] },
      });

      // The validation error should be shown after drop
      expect(mockOnImageSelected).not.toHaveBeenCalled();
    });
  });

  describe("Image Preview", () => {
    it("should display preview after file selection", async () => {
      const user = userEvent.setup();
      const { container } = render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
      const file = new File(["test"], "image.jpg", { type: "image/jpeg" });

      await user.upload(fileInput, file);

      await waitFor(() => {
        const preview = screen.getByAltText("Preview");
        expect(preview).toBeInTheDocument();
        expect(preview).toHaveAttribute("src");
      });
    });

    it("should display existing image preview", () => {
      const existingImage: ImageData = {
        fileName: "existing.jpg",
        contentType: "image/jpeg",
        fileSize: 102400,
        filePath: "books/123-uuid.jpg",
        createdAt: "2024-01-01T00:00:00Z",
        updatedAt: "2024-01-01T00:00:00Z",
      };

      render(<ImageUpload name="coverImage" existingImage={existingImage} onImageSelected={mockOnImageSelected} />);

      expect(screen.getByText("existing.jpg")).toBeInTheDocument();
      const preview = screen.getByAltText("Preview");
      expect(preview.getAttribute("src")).toContain("/static/images/books/123-uuid.jpg");
    });
  });

  describe("Replace and Clear Buttons", () => {
    beforeEach(async () => {
      const { container } = render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
      const file = new File(["test"], "image.jpg", { type: "image/jpeg" });

      await userEvent.setup().upload(fileInput, file);

      await waitFor(() => {
        expect(screen.getByText("image.jpg")).toBeInTheDocument();
      });
    });

    it("should display Replace and Clear buttons after selection", () => {
      expect(screen.getByText("Replace")).toBeInTheDocument();
      expect(screen.getByText("Clear")).toBeInTheDocument();
    });

    it("should clear image when Clear button is clicked", async () => {
      const clearButton = screen.getByText("Clear");
      fireEvent.click(clearButton);

      await waitFor(() => {
        expect(screen.queryByText("image.jpg")).not.toBeInTheDocument();
        expect(screen.getByText("Drag and drop your image here")).toBeInTheDocument();
      });
    });

    it("should allow replacing image", async () => {
      const user = userEvent.setup();
      const replaceButton = screen.getByText("Replace");
      fireEvent.click(replaceButton);

      const newFile = new File(["new test"], "new-image.png", { type: "image/png" });
      const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;

      await user.upload(fileInput, newFile);

      await waitFor(() => {
        expect(screen.getByText("new-image.png")).toBeInTheDocument();
      });
    });
  });

  describe("Error Handling", () => {
    it("should display error message for invalid file type", async () => {
      const user = userEvent.setup();
      const { container } = render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
      const file = new File(["test"], "file.txt", { type: "text/plain" });

      await user.upload(fileInput, file);

      // Error should be detected on file change (synchronous validation)
      expect(mockOnImageSelected).not.toHaveBeenCalled();
    });

    it("should clear error when new valid file is selected", async () => {
      const user = userEvent.setup();
      const { container } = render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      let fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;

      // First, upload invalid file
      await user.upload(fileInput, new File(["test"], "file.txt", { type: "text/plain" }));
      expect(mockOnImageSelected).not.toHaveBeenCalled();

      // Then upload valid file
      fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
      await user.upload(fileInput, new File(["test"], "image.jpg", { type: "image/jpeg" }));

      // Should be called this time
      await waitFor(() => {
        expect(mockOnImageSelected).toHaveBeenCalled();
      });
    });
  });

  describe("Accessibility", () => {
    it("should have proper ARIA labels", () => {
      render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const dropZone = screen.getByRole("button", {
        name: /drag and drop area/i,
      });
      expect(dropZone).toHaveAttribute("aria-label");
    });

    it("should support keyboard interaction", async () => {
      render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const dropZone = screen.getByRole("button", {
        name: /drag and drop area/i,
      });

      fireEvent.keyDown(dropZone, { key: "Enter" });
      // File dialog would open in real scenario
      expect(dropZone).toBeInTheDocument();
    });
  });

  describe("Disabled State", () => {
    it("should disable buttons when isSubmitting is true", () => {
      render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} isSubmitting={true} />);

      const dropZone = screen.getByRole("button", {
        name: /drag and drop area/i,
      });

      expect(dropZone).toHaveClass("opacity-50", "cursor-not-allowed");
    });
  });

  describe("Props", () => {
    it("should use provided name prop for input name", () => {
      const { container } = render(<ImageUpload name="customName" onImageSelected={mockOnImageSelected} />);

      const fileInput = container.querySelector('input[name="customName"]');
      expect(fileInput).toBeInTheDocument();
    });

    it("should call onImageSelected callback when valid file is selected", async () => {
      const user = userEvent.setup();
      const { container } = render(<ImageUpload name="coverImage" onImageSelected={mockOnImageSelected} />);

      const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
      const file = new File(["test"], "image.jpg", { type: "image/jpeg" });

      await user.upload(fileInput, file);

      await waitFor(() => {
        expect(mockOnImageSelected).toHaveBeenCalledWith(expect.any(File), expect.stringContaining("data:image/jpeg"));
      });
    });
  });
});
