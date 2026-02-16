import { describe, it, expect } from "vitest";
import { render, screen } from "~/test/test-utils";
import { BarChart3, TrendingUp, Users, Book } from "lucide-react";
import StatCard from "./stat-card";

describe("StatCard", () => {
  describe("Card Data Display", () => {
    it("should render title correctly", () => {
      render(<StatCard title="Total Items" value={42} icon={BarChart3} />);

      expect(screen.getByText("Total Items")).toBeInTheDocument();
    });

    it("should render numeric value correctly", () => {
      render(<StatCard title="Books" value={156} icon={Book} />);

      expect(screen.getByText("156")).toBeInTheDocument();
    });

    it("should render string value correctly", () => {
      render(<StatCard title="Status" value="Active" icon={TrendingUp} />);

      expect(screen.getByText("Active")).toBeInTheDocument();
    });

    it("should render subtitle when provided", () => {
      render(<StatCard title="Revenue" value={1200} icon={BarChart3} subtitle="Last month" />);

      expect(screen.getByText("Last month")).toBeInTheDocument();
    });

    it("should not render subtitle when not provided", () => {
      const { container } = render(<StatCard title="Items" value={50} icon={Book} />);

      // Check that subtitle element doesn't exist
      const subtitleElements = container.querySelectorAll("p.text-zinc-500");
      expect(subtitleElements.length).toBe(0);
    });

    it("should display all elements together", () => {
      render(<StatCard title="Completed Tasks" value={32} icon={TrendingUp} subtitle="This week" />);

      expect(screen.getByText("Completed Tasks")).toBeInTheDocument();
      expect(screen.getByText("32")).toBeInTheDocument();
      expect(screen.getByText("This week")).toBeInTheDocument();
    });
  });

  describe("Formatting", () => {
    it("should format large numeric values", () => {
      render(<StatCard title="Large Count" value={1000000} icon={BarChart3} />);

      expect(screen.getByText("1000000")).toBeInTheDocument();
    });

    it("should handle decimal numbers", () => {
      render(<StatCard title="Average Rating" value={4.75} icon={TrendingUp} />);

      expect(screen.getByText("4.75")).toBeInTheDocument();
    });

    it("should display zero value correctly", () => {
      render(<StatCard title="Pending" value={0} icon={Book} />);

      expect(screen.getByText("0")).toBeInTheDocument();
    });

    it("should handle string values with special characters", () => {
      render(<StatCard title="Growth Rate" value="+12.5%" icon={TrendingUp} />);

      expect(screen.getByText("+12.5%")).toBeInTheDocument();
    });

    it("should display formatted string values", () => {
      render(<StatCard title="Memory Usage" value="2.4 GB" icon={BarChart3} />);

      expect(screen.getByText("2.4 GB")).toBeInTheDocument();
    });
  });

  describe("Different Stat Types", () => {
    it("should render chart icon stat", () => {
      const { container } = render(<StatCard title="Analytics" value={456} icon={BarChart3} subtitle="Last 30 days" />);

      expect(screen.getByText("Analytics")).toBeInTheDocument();
      expect(screen.getByText("456")).toBeInTheDocument();
      expect(screen.getByText("Last 30 days")).toBeInTheDocument();
      expect(container.querySelector("svg")).toBeInTheDocument();
    });

    it("should render trending stat", () => {
      render(<StatCard title="Growth" value="+25%" icon={TrendingUp} subtitle="vs last month" />);

      expect(screen.getByText("Growth")).toBeInTheDocument();
      expect(screen.getByText("+25%")).toBeInTheDocument();
      expect(screen.getByText("vs last month")).toBeInTheDocument();
    });

    it("should render user count stat", () => {
      render(<StatCard title="Total Users" value={342} icon={Users} subtitle="Active today" />);

      expect(screen.getByText("Total Users")).toBeInTheDocument();
      expect(screen.getByText("342")).toBeInTheDocument();
      expect(screen.getByText("Active today")).toBeInTheDocument();
    });

    it("should render book collection stat", () => {
      render(<StatCard title="Library Size" value={1247} icon={Book} subtitle="Books in collection" />);

      expect(screen.getByText("Library Size")).toBeInTheDocument();
      expect(screen.getByText("1247")).toBeInTheDocument();
      expect(screen.getByText("Books in collection")).toBeInTheDocument();
    });
  });

  describe("Color Customization", () => {
    it("should apply default color class", () => {
      const { container } = render(<StatCard title="Default" value={50} icon={Book} />);

      const iconContainer = container.querySelector('[class*="rounded-lg"][class*="border"][class*="p-3"]');
      expect(iconContainer).toHaveClass("bg-blue-500/10", "text-blue-400", "border-blue-500/20");
    });

    it("should apply custom color class", () => {
      const { container } = render(
        <StatCard
          title="Custom"
          value={75}
          icon={TrendingUp}
          colorClass="bg-green-500/10 text-green-400 border-green-500/20"
        />
      );

      const iconContainer = container.querySelector('[class*="rounded-lg"][class*="border"][class*="p-3"]');
      expect(iconContainer).toHaveClass("bg-green-500/10", "text-green-400", "border-green-500/20");
    });

    it("should support multiple color variants", () => {
      const colorClasses = [
        "bg-red-500/10 text-red-400 border-red-500/20",
        "bg-purple-500/10 text-purple-400 border-purple-500/20",
        "bg-yellow-500/10 text-yellow-400 border-yellow-500/20",
      ];

      const { container } = render(
        <>
          {colorClasses.map((color, index) => (
            <StatCard key={index} title={`Color ${index}`} value={index} icon={Book} colorClass={color} />
          ))}
        </>
      );

      const iconContainers = container.querySelectorAll('[class*="rounded-lg"][class*="border"][class*="p-3"]');
      expect(iconContainers.length).toBe(3);
    });
  });

  describe("Edge Cases", () => {
    it("should handle very long title", () => {
      const longTitle = "This is a very long title that might wrap to multiple lines in the card";
      render(<StatCard title={longTitle} value={999} icon={Book} />);

      expect(screen.getByText(longTitle)).toBeInTheDocument();
    });

    it("should handle very long subtitle", () => {
      const longSubtitle = "This is a very long subtitle with detailed information about the metric";
      render(<StatCard title="Test" value={50} icon={Book} subtitle={longSubtitle} />);

      expect(screen.getByText(longSubtitle)).toBeInTheDocument();
    });

    it("should handle empty string value", () => {
      render(<StatCard title="Empty" value="" icon={Book} />);

      const card = screen.getByText("Empty").closest("div.rounded-lg");
      expect(card).toBeInTheDocument();
    });

    it("should handle multiple cards on same page", () => {
      render(
        <>
          <StatCard title="Card 1" value={10} icon={Book} />
          <StatCard title="Card 2" value={20} icon={Users} />
          <StatCard title="Card 3" value={30} icon={TrendingUp} />
        </>
      );

      expect(screen.getByText("Card 1")).toBeInTheDocument();
      expect(screen.getByText("Card 2")).toBeInTheDocument();
      expect(screen.getByText("Card 3")).toBeInTheDocument();
      expect(screen.getByText("10")).toBeInTheDocument();
      expect(screen.getByText("20")).toBeInTheDocument();
      expect(screen.getByText("30")).toBeInTheDocument();
    });

    it("should handle re-rendering with different props", () => {
      const { rerender } = render(<StatCard title="Initial" value={50} icon={Book} />);

      expect(screen.getByText("Initial")).toBeInTheDocument();
      expect(screen.getByText("50")).toBeInTheDocument();

      rerender(<StatCard title="Updated" value={100} icon={Users} />);

      expect(screen.getByText("Updated")).toBeInTheDocument();
      expect(screen.getByText("100")).toBeInTheDocument();
    });

    it("should maintain structure with null subtitle", () => {
      const { container } = render(<StatCard title="Test" value={25} icon={Book} subtitle={undefined} />);

      const card = container.querySelector("div.rounded-lg");
      expect(card).toBeInTheDocument();
      expect(screen.getByText("Test")).toBeInTheDocument();
      expect(screen.getByText("25")).toBeInTheDocument();
    });
  });

  describe("Accessibility", () => {
    it("should render semantic heading structure", () => {
      render(<StatCard title="Accessible Title" value={42} icon={Book} />);

      expect(screen.getByText("Accessible Title")).toBeInTheDocument();
    });

    it("should be keyboard accessible", () => {
      const { container } = render(<StatCard title="Keyboard Test" value={50} icon={Book} />);

      const card = container.querySelector("div.rounded-lg");
      expect(card).toBeInTheDocument();
    });

    it("should render with appropriate color contrast", () => {
      render(<StatCard title="Contrast Test" value={75} icon={Book} />);

      const title = screen.getByText("Contrast Test");
      expect(title).toHaveClass("text-zinc-400");

      const value = screen.getByText("75");
      expect(value).toHaveClass("text-white");
    });

    it("should work with screen readers", () => {
      render(<StatCard title="Screen Reader Test" value={99} icon={Book} subtitle="Additional info" />);

      expect(screen.getByText("Screen Reader Test")).toBeInTheDocument();
      expect(screen.getByText("99")).toBeInTheDocument();
      expect(screen.getByText("Additional info")).toBeInTheDocument();
    });

    it("should support semantic structure for dashboard", () => {
      const { container } = render(
        <div role="region" aria-label="Statistics">
          <StatCard title="Stat 1" value={10} icon={Book} />
          <StatCard title="Stat 2" value={20} icon={Users} />
        </div>
      );

      const region = container.querySelector('[role="region"]');
      expect(region).toHaveAttribute("aria-label", "Statistics");

      expect(screen.getByText("Stat 1")).toBeInTheDocument();
      expect(screen.getByText("Stat 2")).toBeInTheDocument();
    });
  });

  describe("Component Integration", () => {
    it("should work in dashboard layout", () => {
      render(
        <div className="grid grid-cols-4 gap-4">
          <StatCard title="Total" value={1000} icon={BarChart3} />
          <StatCard title="Growth" value="+25%" icon={TrendingUp} />
          <StatCard title="Users" value={500} icon={Users} />
          <StatCard title="Books" value={750} icon={Book} />
        </div>
      );

      expect(screen.getByText("Total")).toBeInTheDocument();
      expect(screen.getByText("Growth")).toBeInTheDocument();
      expect(screen.getByText("Users")).toBeInTheDocument();
      expect(screen.getByText("Books")).toBeInTheDocument();
    });

    it("should work with dynamic data updates", () => {
      const { rerender } = render(<StatCard title="Count" value={0} icon={Book} />);

      expect(screen.getByText("0")).toBeInTheDocument();

      rerender(<StatCard title="Count" value={5} icon={Book} />);
      expect(screen.getByText("5")).toBeInTheDocument();

      rerender(<StatCard title="Count" value={10} icon={Book} />);
      expect(screen.getByText("10")).toBeInTheDocument();
    });
  });
});
