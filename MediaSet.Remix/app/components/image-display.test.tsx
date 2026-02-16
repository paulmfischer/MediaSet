import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, beforeEach } from "vitest";
import ImageDisplay from "./image-display";
import { Entity } from "~/models";
import type { ImageData } from "~/models";

describe("ImageDisplay Component", () => {
  const mockImageData: ImageData = {
    fileName: "test-book.jpg",
    contentType: "image/jpeg",
    fileSize: 2048576, // 2MB
    filePath: "books/123-uuid.jpg",
    createdAt: "2025-01-01T00:00:00Z",
    updatedAt: "2025-01-01T00:00:00Z",
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("should render placeholder when no image data is provided", () => {
    render(<ImageDisplay alt="Test book" />);

    const placeholder = screen.getByText("No image available");
    expect(placeholder).toBeInTheDocument();
  });

  it("should render placeholder when image data is provided but no entity info", () => {
    render(<ImageDisplay imageData={mockImageData} alt="Test book" />);

    const placeholder = screen.getByText("No image available");
    expect(placeholder).toBeInTheDocument();
  });

  it("should render image when all required props are provided", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book cover"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const image = screen.getByRole("button", {
      name: /view full size image/i,
    }) as HTMLElement;
    expect(image).toBeInTheDocument();

    const imgElement = image.querySelector("img");
    // Check that the src contains the static/images path and filePath
    expect(imgElement?.getAttribute("src")).toContain("/static/images/books/123-uuid.jpg");
    expect(imgElement).toHaveAttribute("alt", "Test book cover");
  });

  it("should apply correct size classes", () => {
    const { rerender } = render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test"
        entityType={Entity.Movies}
        entityId="456"
        size="small"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButton = screen.getByRole("button", {
      name: /view full size image/i,
    });
    const container = imageButton.closest(".h-32");
    expect(container).toHaveClass("h-32", "w-32");

    rerender(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test"
        entityType={Entity.Movies}
        entityId="456"
        size="large"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButtonLarge = screen.getByRole("button", {
      name: /view full size image/i,
    });
    const containerLarge = imageButtonLarge.closest(".h-64");
    expect(containerLarge).toHaveClass("h-64", "w-64");
  });

  it("should apply custom className", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test"
        entityType={Entity.Games}
        entityId="789"
        className="custom-class"
        apiUrl="http://localhost:5000"
      />
    );

    const container = document.querySelector(".custom-class");
    expect(container).toHaveClass("custom-class");
  });

  it("should open modal when image button is clicked", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButton = screen.getByRole("button", {
      name: /view full size image/i,
    });
    fireEvent.click(imageButton);

    const modalImages = screen.getAllByAltText("Test book");
    expect(modalImages.length).toBeGreaterThan(1);
  });

  it("should close modal when close button is clicked", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButton = screen.getByRole("button", {
      name: /view full size image/i,
    });
    fireEvent.click(imageButton);

    const closeButton = screen.getByLabelText("Close image modal");
    expect(closeButton).toBeInTheDocument();

    fireEvent.click(closeButton);

    // Modal should be removed
    const closeButtonAfter = screen.queryByLabelText("Close image modal");
    expect(closeButtonAfter).not.toBeInTheDocument();
  });

  it("should close modal when clicking on backdrop", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButton = screen.getByRole("button", {
      name: /view full size image/i,
    });
    fireEvent.click(imageButton);

    const backdrop = screen.getByRole("dialog");
    fireEvent.click(backdrop);

    const closeButton = screen.queryByLabelText("Close image modal");
    expect(closeButton).not.toBeInTheDocument();
  });

  it("should close modal when Escape key is pressed", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButton = screen.getByRole("button", {
      name: /view full size image/i,
    });
    fireEvent.click(imageButton);

    const backdrop = screen.getByRole("dialog");
    fireEvent.keyDown(backdrop, { key: "Escape" });

    const closeButton = screen.queryByLabelText("Close image modal");
    expect(closeButton).not.toBeInTheDocument();
  });

  it("should not close modal when clicking on modal content", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButton = screen.getByRole("button", {
      name: /view full size image/i,
    });
    fireEvent.click(imageButton);

    const modalImages = screen.getAllByAltText("Test book");
    const fullSizeImage = modalImages[modalImages.length - 1];
    fireEvent.click(fullSizeImage.parentElement!);

    const closeButton = screen.getByLabelText("Close image modal");
    expect(closeButton).toBeInTheDocument();
  });

  it("should display image metadata in modal", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButton = screen.getByRole("button", {
      name: /view full size image/i,
    });
    fireEvent.click(imageButton);

    expect(screen.getByText("test-book.jpg")).toBeInTheDocument();
    // Check for the MB text in any element
    const sizeElements = screen.queryAllByText((content) => content.includes("MB"));
    expect(sizeElements.length).toBeGreaterThan(0);
  });

  it("should display placeholder when image fails to load", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const images = document.querySelectorAll("img") as NodeListOf<HTMLImageElement>;
    const mainImage = images[0];
    fireEvent.error(mainImage);

    // After error, the placeholder should appear
    const placeholder = screen.getByText("No image available");
    expect(placeholder).toBeInTheDocument();
  });

  it("should have lazy loading enabled", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const images = screen.getAllByRole("img", { hidden: true }) as HTMLImageElement[];
    const mainImage = images.find((img) => img.alt === "Test book") as HTMLImageElement;
    expect(mainImage).toHaveAttribute("loading", "lazy");
  });

  it("should support different entity types", () => {
    const entityTypes = [Entity.Books, Entity.Movies, Entity.Games, Entity.Musics];

    entityTypes.forEach((entityType) => {
      const { unmount } = render(
        <ImageDisplay
          imageData={mockImageData}
          alt={`Test ${entityType}`}
          entityType={entityType}
          entityId="123"
          apiUrl="http://localhost:5000"
        />
      );

      const images = document.querySelectorAll("img") as NodeListOf<HTMLImageElement>;
      const mainImage = images[0];
      // Check that src contains the static/images path and filePath
      expect(mainImage.getAttribute("src")).toContain("/static/images/books/123-uuid.jpg");

      unmount();
    });
  });

  it("should have proper accessibility attributes", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const button = screen.getByRole("button", {
      name: /view full size image/i,
    });
    const container = button.parentElement;
    expect(container).toHaveAttribute("aria-label", "Test book");

    expect(button).toBeInTheDocument();
  });

  it("should have accessible modal dialog", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButton = screen.getByRole("button", {
      name: /view full size image/i,
    });
    fireEvent.click(imageButton);

    const dialog = screen.getByRole("dialog");
    expect(dialog).toHaveAttribute("aria-modal", "true");
  });

  it("should apply hover effect to image button", () => {
    render(
      <ImageDisplay
        imageData={mockImageData}
        alt="Test book"
        entityType={Entity.Books}
        entityId="123"
        apiUrl="http://localhost:5000"
      />
    );

    const imageButton = screen.getByRole("button", {
      name: /view full size image/i,
    });
    expect(imageButton).toHaveClass("image-button");
  });
});
