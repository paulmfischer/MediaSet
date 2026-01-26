import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "~/test/test-utils";
import { MemoryRouter } from "react-router-dom";
import ErrorScreen from "./error-screen";

describe("ErrorScreen", () => {
  const baseProps = {
    title: "Something went wrong",
  };

  const renderComponent = (overrideProps = {}) =>
    render(
      <MemoryRouter>
        <ErrorScreen {...baseProps} {...overrideProps} />
      </MemoryRouter>
    );

  it("renders title, message, and Go Home link", () => {
    renderComponent({ message: "Please try again." });

    expect(screen.getByText("Something went wrong")).toBeInTheDocument();
    expect(screen.getByText("Please try again.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Go Home" })).toHaveAttribute("href", "/");
  });

  it("shows diagnostics when details are enabled", () => {
    renderComponent({
      statusCode: 500,
      statusText: "Internal Server Error",
      data: "Stacktrace here",
      showDetails: true,
    });

    // Status code appears in both badge and details, so use getAllByText
    expect(screen.getAllByText("500")).toHaveLength(2);
    expect(screen.getByText("Internal Server Error")).toBeInTheDocument();
    expect(screen.getByText("Stacktrace here")).toBeInTheDocument();
  });

  it("hides diagnostics when details are disabled", () => {
    renderComponent({
      statusCode: 404,
      statusText: "Not Found",
      data: "Missing page",
      showDetails: false,
    });

    expect(screen.queryByText("Not Found")).not.toBeInTheDocument();
    expect(screen.queryByText("Missing page")).not.toBeInTheDocument();
  });

  it("invokes retry handler when Try Again is clicked", () => {
    const onRetry = vi.fn();
    renderComponent({ onRetry });

    fireEvent.click(screen.getByRole("button", { name: "Try Again" }));

    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
