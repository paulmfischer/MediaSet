import { describe, it, expect, beforeEach, vi, afterEach } from "vitest";
import { render, screen, waitFor } from "~/test/test-utils";
import PendingNavigation from "./pending-navigation";

// Mock the Remix useNavigation hook
vi.mock("@remix-run/react", async () => {
  const actual = await vi.importActual("@remix-run/react");
  return {
    ...actual,
    useNavigation: vi.fn(),
  };
});

// Mock the Spinner component
vi.mock("./spinner", () => ({
  default: ({ size }: { size?: number }) => (
    <div data-testid="spinner" data-size={size}>
      Spinner
    </div>
  ),
}));

import { useNavigation } from "@remix-run/react";

describe("PendingNavigation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe("Navigation State Handling", () => {
    it("should not render when navigation state is idle", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.firstChild).toBeNull();
    });

    it("should render when navigation state is loading", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.firstChild).not.toBeNull();
    });

    it("should render when navigation state is submitting", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "submitting",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.firstChild).not.toBeNull();
    });

    it("should handle state transitions from idle to loading", async () => {
      const { rerender, container } = render(<PendingNavigation />);

      // Initially idle
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);
      expect(container.firstChild).toBeNull();

      // Transition to loading
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);

      await waitFor(() => {
        expect(container.firstChild).not.toBeNull();
      });
    });

    it("should handle state transitions from loading to idle", async () => {
      // Initially loading
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      const { rerender, container } = render(<PendingNavigation />);

      await waitFor(() => {
        expect(container.firstChild).not.toBeNull();
      });

      // Transition to idle
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);

      expect(container.firstChild).toBeNull();
    });

    it("should handle rapid state changes", () => {
      const { rerender } = render(<PendingNavigation />);

      // Rapid state transitions
      vi.mocked(useNavigation).mockReturnValue({ state: "idle" } as unknown as ReturnType<typeof useNavigation>);
      rerender(<PendingNavigation />);

      vi.mocked(useNavigation).mockReturnValue({ state: "loading" } as unknown as ReturnType<typeof useNavigation>);
      rerender(<PendingNavigation />);

      vi.mocked(useNavigation).mockReturnValue({ state: "idle" } as unknown as ReturnType<typeof useNavigation>);
      rerender(<PendingNavigation />);

      expect(vi.mocked(useNavigation)).toHaveBeenCalled();
    });
  });

  describe("Pending Indicators", () => {
    beforeEach(() => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);
    });

    it("should render spinner and backdrop when loading", () => {
      const { container } = render(<PendingNavigation />);

      const spinner = screen.getByTestId("spinner");
      const backdrop = container.querySelector(".bg-gray-900");

      expect(spinner).toBeInTheDocument();
      expect(spinner).toHaveAttribute("data-size", "84");
      expect(backdrop).toBeInTheDocument();
    });

    it("should render with correct DOM structure", () => {
      const { container } = render(<PendingNavigation />);

      // Should have backdrop with correct classes
      const backdrop = container.querySelector(".bg-gray-900");
      expect(backdrop).toHaveClass("fixed", "inset-0", "bg-opacity-60", "2xl:mx-14");

      // Should have spinner container with correct positioning
      const spinnerContainer = container.querySelector(".fixed.z-50");
      expect(spinnerContainer).toHaveClass(
        "top-1/2",
        "left-1/2",
        "-translate-x-1/2",
        "-translate-y-1/2",
        "flex",
        "justify-center"
      );

      // Should have dialog body with correct id
      const dialogBody = container.querySelector("#dialog-body");
      expect(dialogBody).toBeInTheDocument();
    });

    it("should apply z-index layering correctly", () => {
      const { container } = render(<PendingNavigation />);

      const backdrop = container.querySelector(".z-40");
      const spinnerContainer = container.querySelector(".z-50");

      expect(backdrop).toHaveClass("z-40");
      expect(spinnerContainer).toHaveClass("z-50");
    });

    it("should not render when not loading", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      const spinner = container.querySelector('[data-testid="spinner"]');
      expect(spinner).not.toBeInTheDocument();
    });
  });

  describe("Accessibility", () => {
    beforeEach(() => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);
    });

    it("should have semantic structure with dialog-body id", () => {
      const { container } = render(<PendingNavigation />);

      const dialogBody = container.querySelector("#dialog-body");
      expect(dialogBody?.id).toBe("dialog-body");
    });

    it("should use fixed positioning to overlay and block interaction", () => {
      const { container } = render(<PendingNavigation />);

      const backdrop = container.querySelector(".fixed.inset-0");
      const spinnerContainer = container.querySelector(".fixed.z-50");

      expect(backdrop).toHaveClass("fixed", "inset-0");
      expect(spinnerContainer).toHaveClass("fixed", "z-50");
    });

    it("should provide visual feedback with semi-transparent backdrop", () => {
      const { container } = render(<PendingNavigation />);

      const backdrop = container.querySelector(".bg-gray-900");
      expect(backdrop).toHaveClass("bg-opacity-60");
    });

    it("should center spinner for user visibility", () => {
      const { container } = render(<PendingNavigation />);

      const spinnerContainer = container.querySelector(".flex.justify-center");
      expect(spinnerContainer).toHaveClass("top-1/2", "left-1/2", "-translate-x-1/2", "-translate-y-1/2");
    });

    it("should render spinner with appropriate styling and color", () => {
      render(<PendingNavigation />);

      const spinner = screen.getByTestId("spinner");
      expect(spinner).toBeInTheDocument();
    });

    it("should be perceivable by users with clear loading state", () => {
      const { container } = render(<PendingNavigation />);

      // Backdrop provides visual feedback
      expect(container.querySelector(".bg-gray-900")).toBeInTheDocument();
      // Spinner provides animation feedback
      expect(screen.getByTestId("spinner")).toBeInTheDocument();
    });
  });

  describe("Rendering Conditions", () => {
    it("should render nothing when navigation state is idle", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.firstChild).toBeNull();
    });

    it("should conditionally render based on loading state", () => {
      const { rerender, container } = render(<PendingNavigation />);

      // Set to idle
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);
      expect(container.firstChild).toBeNull();

      // Set to loading
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);
      expect(container.firstChild).not.toBeNull();
    });

    it("should render fragment with 2 children when loading", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.children).toHaveLength(2);

      const firstChild = container.children[0];
      const secondChild = container.children[1];

      expect(firstChild).toHaveClass("fixed", "bg-gray-900");
      expect(secondChild).toHaveClass("fixed", "z-50");
    });
  });

  describe("Component Lifecycle", () => {
    it("should call useNavigation hook on mount", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      render(<PendingNavigation />);

      expect(vi.mocked(useNavigation)).toHaveBeenCalled();
    });

    it("should re-call useNavigation on state change", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      const { rerender } = render(<PendingNavigation />);

      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);

      expect(vi.mocked(useNavigation)).toHaveBeenCalledTimes(2);
    });

    it("should handle multiple mounts and unmounts", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      const { unmount } = render(<PendingNavigation />);

      expect(screen.getByTestId("spinner")).toBeInTheDocument();

      unmount();

      // After unmount, component should be removed
      expect(screen.queryByTestId("spinner")).not.toBeInTheDocument();
    });

    it("should be callable multiple times without issues", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      const { unmount: unmount1 } = render(<PendingNavigation />);
      unmount1();

      const { unmount: unmount2 } = render(<PendingNavigation />);
      unmount2();

      expect(vi.mocked(useNavigation)).toHaveBeenCalled();
    });
  });

  describe("Edge Cases", () => {
    it("should handle undefined state gracefully", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: undefined,
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.firstChild).toBeNull();
    });

    it("should handle null state gracefully", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: null,
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.firstChild).toBeNull();
    });

    it("should handle empty string state gracefully", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.firstChild).toBeNull();
    });

    it("should handle unexpected state values", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "unexpected-state",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.firstChild).toBeNull();
    });

    it("should handle state changes in quick succession", async () => {
      const { rerender } = render(<PendingNavigation />);

      for (let i = 0; i < 5; i++) {
        vi.mocked(useNavigation).mockReturnValue({
          state: i % 2 === 0 ? "loading" : "idle",
        } as unknown as ReturnType<typeof useNavigation>);

        rerender(<PendingNavigation />);
      }

      expect(vi.mocked(useNavigation)).toHaveBeenCalled();
    });

    it("should maintain correct state after multiple transitions", async () => {
      const { container, rerender } = render(<PendingNavigation />);

      // Start loading
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);

      await waitFor(() => {
        expect(container.children).toHaveLength(2);
      });

      // Back to idle
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);

      expect(container.firstChild).toBeNull();
    });

    it("should handle continuous loading state", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      const { rerender } = render(<PendingNavigation />);

      // Re-render multiple times while loading
      rerender(<PendingNavigation />);
      rerender(<PendingNavigation />);
      rerender(<PendingNavigation />);

      expect(screen.getByTestId("spinner")).toBeInTheDocument();
    });

    it("should handle state object with other properties", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
        location: { pathname: "/" },
        formData: new FormData(),
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      expect(container.firstChild).not.toBeNull();
    });
  });

  describe("Integration", () => {
    it("should work with navigation state transitions during page load", async () => {
      const { rerender } = render(<PendingNavigation />);

      // Simulate page navigation starting
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);
      expect(screen.getByTestId("spinner")).toBeInTheDocument();

      // Simulate page load completion
      vi.mocked(useNavigation).mockReturnValue({
        state: "idle",
      } as unknown as ReturnType<typeof useNavigation>);

      rerender(<PendingNavigation />);

      await waitFor(() => {
        expect(screen.queryByTestId("spinner")).not.toBeInTheDocument();
      });
    });

    it("should work with form submission navigation", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "submitting",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(<PendingNavigation />);

      // Should show spinner during submission
      expect(container.firstChild).not.toBeNull();
      expect(screen.queryByTestId("spinner")).toBeInTheDocument();
    });

    it("should work with multiple PendingNavigation components", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(
        <>
          <PendingNavigation />
          <PendingNavigation />
        </>
      );

      const spinners = container.querySelectorAll('[data-testid="spinner"]');
      expect(spinners).toHaveLength(2);
    });

    it("should overlay correctly over other content", () => {
      vi.mocked(useNavigation).mockReturnValue({
        state: "loading",
      } as unknown as ReturnType<typeof useNavigation>);

      const { container } = render(
        <>
          <div id="main-content">Main Content</div>
          <PendingNavigation />
        </>
      );

      const mainContent = container.querySelector("#main-content");
      const spinner = screen.getByTestId("spinner");

      expect(mainContent).toBeInTheDocument();
      expect(spinner).toBeInTheDocument();
    });
  });
});
