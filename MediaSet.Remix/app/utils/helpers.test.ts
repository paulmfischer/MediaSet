import { describe, it, expect } from "vitest";
import { toTitleCase, getEntityFromParams, singular, millisecondsToMinutesSeconds, formToDto } from "~/utils/helpers";
import { Entity, BookEntity, MovieEntity, GameEntity, MusicEntity } from "~/models";

describe("helpers.ts", () => {
  describe("toTitleCase", () => {
    it("should convert lowercase string to title case", () => {
      const result = toTitleCase("hello world");
      expect(result).toBe("Hello World");
    });

    it("should convert mixed case string to title case", () => {
      const result = toTitleCase("hELLO wORLD");
      expect(result).toBe("Hello World");
    });

    it("should handle single word", () => {
      const result = toTitleCase("hello");
      expect(result).toBe("Hello");
    });

    it("should handle already title cased string", () => {
      const result = toTitleCase("Hello World");
      expect(result).toBe("Hello World");
    });

    it("should return empty string for undefined", () => {
      const result = toTitleCase(undefined);
      expect(result).toBe("");
    });

    it("should handle string with multiple spaces", () => {
      const result = toTitleCase("hello   world");
      expect(result).toBe("Hello   World");
    });

    it("should handle string with special characters", () => {
      const result = toTitleCase("hello-world it's");
      expect(result).toBe("Hello-world It's");
    });

    it("should handle empty string", () => {
      const result = toTitleCase("");
      expect(result).toBe("");
    });

    it("should handle string with numbers", () => {
      const result = toTitleCase("hello 123 world");
      expect(result).toBe("Hello 123 World");
    });
  });

  describe("getEntityFromParams", () => {
    it('should return Books entity when params.entity is "books"', () => {
      const result = getEntityFromParams({ entity: "books" });
      expect(result).toBe(Entity.Books);
    });

    it('should return Movies entity when params.entity is "movies"', () => {
      const result = getEntityFromParams({ entity: "movies" });
      expect(result).toBe(Entity.Movies);
    });

    it('should return Games entity when params.entity is "games"', () => {
      const result = getEntityFromParams({ entity: "games" });
      expect(result).toBe(Entity.Games);
    });

    it('should return Musics entity when params.entity is "musics"', () => {
      const result = getEntityFromParams({ entity: "musics" });
      expect(result).toBe(Entity.Musics);
    });

    it("should handle mixed case entity names", () => {
      const result = getEntityFromParams({ entity: "BOOKS" });
      expect(result).toBe(Entity.Books);
    });

    it("should handle lowercase entity names", () => {
      const result = getEntityFromParams({ entity: "books" });
      expect(result).toBe(Entity.Books);
    });
  });

  describe("singular", () => {
    it("should convert Books to Book", () => {
      const result = singular(Entity.Books);
      expect(result).toBe("Book");
    });

    it("should convert Movies to Movie", () => {
      const result = singular(Entity.Movies);
      expect(result).toBe("Movie");
    });

    it("should convert Games to Game", () => {
      const result = singular(Entity.Games);
      expect(result).toBe("Game");
    });

    it("should convert Musics to Music", () => {
      const result = singular(Entity.Musics);
      expect(result).toBe("Music");
    });

    it("should remove last character from any string", () => {
      const result = singular("TestPlural" as Entity);
      expect(result).toBe("TestPlura");
    });

    it("should handle single character entity", () => {
      const result = singular("A" as Entity);
      expect(result).toBe("");
    });
  });

  describe("millisecondsToMinutesSeconds", () => {
    it("should convert milliseconds to MM:SS format", () => {
      const result = millisecondsToMinutesSeconds(65000); // 1:05
      expect(result).toBe("1:05");
    });

    it("should handle 0 milliseconds", () => {
      const result = millisecondsToMinutesSeconds(0);
      expect(result).toBe("");
    });

    it("should handle null", () => {
      const result = millisecondsToMinutesSeconds(null);
      expect(result).toBe("");
    });

    it("should handle undefined", () => {
      const result = millisecondsToMinutesSeconds(undefined);
      expect(result).toBe("");
    });

    it("should handle negative values", () => {
      const result = millisecondsToMinutesSeconds(-1000);
      expect(result).toBe("");
    });

    it("should pad seconds with leading zero", () => {
      const result = millisecondsToMinutesSeconds(5000); // 0:05
      expect(result).toBe("0:05");
    });

    it("should handle large time values", () => {
      const result = millisecondsToMinutesSeconds(600000); // 10:00
      expect(result).toBe("10:00");
    });

    it("should handle milliseconds less than 1 second", () => {
      const result = millisecondsToMinutesSeconds(500);
      expect(result).toBe("0:00");
    });

    it("should correctly calculate minutes and seconds", () => {
      const result = millisecondsToMinutesSeconds(125000); // 2:05
      expect(result).toBe("2:05");
    });

    it("should round down seconds", () => {
      const result = millisecondsToMinutesSeconds(65999); // 1:05.999
      expect(result).toBe("1:05");
    });
  });

  describe("formToDto", () => {
    describe("Book entity conversion", () => {
      it("should convert form data to BookEntity", () => {
        const formData = new FormData();
        formData.append("type", Entity.Books);
        formData.append("title", "Test Book");
        formData.append("authors", "Author 1,Author 2");
        formData.append("isbn", "978-1234567890");
        formData.append("publisher", "Test Publisher");
        formData.append("pages", "300");
        formData.append("format", "Hardcover");
        formData.append("genres", "Fiction,Mystery");
        formData.append("publicationDate", "2023-01-15");
        formData.append("plot", "A great story");
        formData.append("subtitle", "A Tale");
        formData.append("id", "123");

        const result = formToDto(formData);

        expect(result).toEqual({
          type: Entity.Books,
          title: "Test Book",
          authors: ["Author 1", "Author 2"],
          isbn: "978-1234567890",
          publisher: "Test Publisher",
          pages: 300,
          format: "Hardcover",
          genres: ["Fiction", "Mystery"],
          publicationDate: "2023-01-15",
          plot: "A great story",
          subtitle: "A Tale",
          id: "123",
        });
      });

      it("should handle empty string values as undefined for BookEntity", () => {
        const formData = new FormData();
        formData.append("type", Entity.Books);
        formData.append("title", "Test Book");
        formData.append("authors", "");
        formData.append("isbn", "");
        formData.append("id", "");

        const result = formToDto(formData) as BookEntity;

        expect(result.title).toBe("Test Book");
        expect(result.isbn).toBeUndefined();
        expect(result.id).toBeUndefined();
      });

      it("should handle missing optional fields for BookEntity", () => {
        const formData = new FormData();
        formData.append("type", Entity.Books);
        formData.append("title", "Test Book");

        const result = formToDto(formData) as BookEntity;

        expect(result.type).toBe(Entity.Books);
        expect(result.title).toBe("Test Book");
        expect(result.authors).toBeUndefined();
      });
    });

    describe("Movie entity conversion", () => {
      it("should convert form data to MovieEntity", () => {
        const formData = new FormData();
        formData.append("type", Entity.Movies);
        formData.append("title", "Test Movie");
        formData.append("studios", "Studio 1,Studio 2");
        formData.append("barcode", "123456789");
        formData.append("format", "Blu-ray");
        formData.append("genres", "Action,Thriller");
        formData.append("releaseDate", "2023-06-15");
        formData.append("plot", "An epic adventure");
        formData.append("runtime", "120");
        formData.append("isTvSeries", "");
        formData.append("id", "456");

        const result = formToDto(formData) as MovieEntity;

        expect(result).toEqual({
          type: Entity.Movies,
          title: "Test Movie",
          studios: ["Studio 1", "Studio 2"],
          barcode: "123456789",
          format: "Blu-ray",
          genres: ["Action", "Thriller"],
          releaseDate: "2023-06-15",
          plot: "An epic adventure",
          runtime: 120,
          isTvSeries: false,
          id: "456",
        });
      });

      it("should handle isTvSeries as true", () => {
        const formData = new FormData();
        formData.append("type", Entity.Movies);
        formData.append("title", "Test Series");
        formData.append("isTvSeries", "true");

        const result = formToDto(formData) as MovieEntity;

        expect(result.isTvSeries).toBe(true);
      });

      it("should handle isTvSeries as false when empty string", () => {
        const formData = new FormData();
        formData.append("type", Entity.Movies);
        formData.append("title", "Test Movie");
        formData.append("isTvSeries", "");

        const result = formToDto(formData) as MovieEntity;

        expect(result.isTvSeries).toBe(false);
      });

      it("should handle empty string values as undefined for MovieEntity", () => {
        const formData = new FormData();
        formData.append("type", Entity.Movies);
        formData.append("title", "Test Movie");
        formData.append("barcode", "");
        formData.append("format", "");

        const result = formToDto(formData) as MovieEntity;

        expect(result.title).toBe("Test Movie");
        expect(result.barcode).toBeUndefined();
        expect(result.format).toBeUndefined();
      });
    });

    describe("Game entity conversion", () => {
      it("should convert form data to GameEntity", () => {
        const formData = new FormData();
        formData.append("type", Entity.Games);
        formData.append("title", "Test Game");
        formData.append("developers", "Dev 1,Dev 2");
        formData.append("publishers", "Pub 1,Pub 2");
        formData.append("platform", "PlayStation 5");
        formData.append("barcode", "987654321");
        formData.append("format", "Disc");
        formData.append("genres", "Action,RPG");
        formData.append("releaseDate", "2023-09-20");
        formData.append("rating", "8.5");
        formData.append("description", "An amazing game");
        formData.append("id", "789");

        const result = formToDto(formData) as GameEntity;

        expect(result).toEqual({
          type: Entity.Games,
          title: "Test Game",
          developers: ["Dev 1", "Dev 2"],
          publishers: ["Pub 1", "Pub 2"],
          platform: "PlayStation 5",
          barcode: "987654321",
          format: "Disc",
          genres: ["Action", "RPG"],
          releaseDate: "2023-09-20",
          rating: "8.5",
          description: "An amazing game",
          id: "789",
        });
      });

      it("should handle empty string values as undefined for GameEntity", () => {
        const formData = new FormData();
        formData.append("type", Entity.Games);
        formData.append("title", "Test Game");
        formData.append("platform", "");
        formData.append("rating", "");

        const result = formToDto(formData) as GameEntity;

        expect(result.title).toBe("Test Game");
        expect(result.platform).toBeUndefined();
        expect(result.rating).toBeUndefined();
      });
    });

    describe("Music entity conversion", () => {
      it("should convert form data to MusicEntity with disc list", () => {
        const formData = new FormData();
        formData.append("type", Entity.Musics);
        formData.append("title", "Test Album");
        formData.append("artist", "Test Artist");
        formData.append("label", "Test Label");
        formData.append("barcode", "111222333");
        formData.append("format", "CD");
        formData.append("genres", "Rock,Pop");
        formData.append("releaseDate", "2023-03-10");
        formData.append("duration", "3:45");
        formData.append("tracks", "12");
        formData.append("discs", "1");
        formData.append("discList[0].trackNumber", "1");
        formData.append("discList[0].title", "Track 1");
        formData.append("discList[0].duration", "3:45");
        formData.append("discList[1].trackNumber", "2");
        formData.append("discList[1].title", "Track 2");
        formData.append("discList[1].duration", "4:15");
        formData.append("id", "101");

        const result = formToDto(formData) as MusicEntity;

        expect(result.type).toBe(Entity.Musics);
        expect(result.title).toBe("Test Album");
        expect(result.artist).toBe("Test Artist");
        expect(result.label).toBe("Test Label");
        expect(result.barcode).toBe("111222333");
        expect(result.format).toBe("CD");
        expect(result.genres).toEqual(["Rock", "Pop"]);
        expect(result.releaseDate).toBe("2023-03-10");
        expect(result.duration).toBe(225000); // 3:45 in ms
        expect(result.discList).toHaveLength(2);
        expect(result.discList?.[0]).toEqual({
          trackNumber: 1,
          title: "Track 1",
          duration: 225000,
        });
        expect(result.discList?.[1]).toEqual({
          trackNumber: 2,
          title: "Track 2",
          duration: 255000,
        });
      });

      it("should handle music with no disc list", () => {
        const formData = new FormData();
        formData.append("type", Entity.Musics);
        formData.append("title", "Single Track");
        formData.append("artist", "Artist");

        const result = formToDto(formData) as MusicEntity;

        expect(result.type).toBe(Entity.Musics);
        expect(result.title).toBe("Single Track");
        expect(result.discList).toBeUndefined();
      });

      it("should handle empty string values as undefined for MusicEntity", () => {
        const formData = new FormData();
        formData.append("type", Entity.Musics);
        formData.append("title", "Test Album");
        formData.append("artist", "");
        formData.append("label", "");
        formData.append("duration", "");

        const result = formToDto(formData) as MusicEntity;

        expect(result.title).toBe("Test Album");
        expect(result.artist).toBeUndefined();
        expect(result.label).toBeUndefined();
        expect(result.duration).toBeUndefined();
      });

      it("should parse disc list with track number as index when not provided", () => {
        const formData = new FormData();
        formData.append("type", Entity.Musics);
        formData.append("title", "Test Album");
        formData.append("discList[0].title", "Track 1");
        formData.append("discList[0].duration", "3:00");

        const result = formToDto(formData) as MusicEntity;

        expect(result.discList).toHaveLength(1);
        expect(result.discList?.[0].trackNumber).toBe(1);
        expect(result.discList?.[0].title).toBe("Track 1");
      });
    });

    describe("formToDto edge cases", () => {
      it("should return null for unknown entity type", () => {
        const formData = new FormData();
        formData.append("type", "UnknownEntity");

        const result = formToDto(formData);

        expect(result).toBeNull();
      });

      it("should handle form data with only type field", () => {
        const formData = new FormData();
        formData.append("type", Entity.Books);

        const result = formToDto(formData);

        expect(result?.type).toBe(Entity.Books);
      });

      it("should split comma-separated values into arrays", () => {
        const formData = new FormData();
        formData.append("type", Entity.Games);
        formData.append("title", "Test");
        formData.append("developers", "Dev1,Dev2,Dev3");
        formData.append("publishers", "Pub1,Pub2");

        const result = formToDto(formData) as GameEntity;

        expect(result.developers).toEqual(["Dev1", "Dev2", "Dev3"]);
        expect(result.publishers).toEqual(["Pub1", "Pub2"]);
      });

      it("should handle single value in comma-separated field", () => {
        const formData = new FormData();
        formData.append("type", Entity.Books);
        formData.append("title", "Test");
        formData.append("authors", "Single Author");

        const result = formToDto(formData) as BookEntity;

        expect(result.authors).toEqual(["Single Author"]);
      });
    });
  });
});
