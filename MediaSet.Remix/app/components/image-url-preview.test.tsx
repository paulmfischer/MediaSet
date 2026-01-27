import { describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import ImageUrlPreview from "./image-url-preview";

describe("ImageUrlPreview Component", () => {
  beforeEach(() => {
    // Clean up any existing input elements
    const existing = document.getElementById("imageUrl");
    if (existing) existing.remove();
  });

  it("should render existing URL preview", () => {
    render(<ImageUrlPreview existingUrl="https://example.com/image.jpg" />);

    const img = screen.getByAltText("Image Preview") as HTMLImageElement;
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("src", "https://example.com/image.jpg");
  });

  it("should update when the input value changes", () => {
    const input = document.createElement("input");
    input.id = "imageUrl";
    document.body.appendChild(input);

    render(<ImageUrlPreview />);

    input.value = "https://example.com/new.jpg";
    fireEvent.input(input);

    const img = screen.getByAltText("Image Preview") as HTMLImageElement;
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("src", "https://example.com/new.jpg");
  });

  it("should show error message when image fails to load", () => {
    const input = document.createElement("input");
    input.id = "imageUrl";
    document.body.appendChild(input);

    render(<ImageUrlPreview />);

    input.value = "https://example.com/broken.jpg";
    fireEvent.input(input);

    const img = screen.getByAltText("Image Preview") as HTMLImageElement;
    // Simulate image load error
    fireEvent.error(img);

    expect(screen.getByText("Unable to load image")).toBeInTheDocument();
  });

  it("should remove preview when input is cleared", () => {
    const input = document.createElement("input");
    input.id = "imageUrl";
    document.body.appendChild(input);

    render(<ImageUrlPreview />);

    input.value = "https://example.com/new.jpg";
    fireEvent.input(input);

    let img = screen.getByAltText("Image Preview");
    expect(img).toBeInTheDocument();

    // Clear the input
    input.value = "";
    fireEvent.input(input);

    expect(screen.queryByAltText("Image Preview")).not.toBeInTheDocument();
  });
});
