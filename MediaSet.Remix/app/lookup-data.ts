import { baseUrl } from "./constants.server";
import { singular } from "./helpers";
import { BookEntity, Entity } from "./models";

type Link = {
  name: string;
  url: string;
};

type IsbnLookup = {
  title: string;
  subtitle: string;
  authors: Array<Link>;
  numberOfPages: number;
  publishers: Array<Omit<Link, 'url'>>;
  publishDate: string;
  subjects: Array<Link>;
};

const linkMap = (link: Link) => link.name;

export async function lookup(entityType: Entity, barcode: string) {
  if (entityType === Entity.Books) {
    const response = await fetch(`${baseUrl}/lookup/isbn/${barcode}`);
    if (response.status == 404) {
      return { error: { notFound: `No ${singular(entityType)} found for ISBN ${barcode}` } };
    }

    const bookLookup = await response.json() as IsbnLookup;
    return {
      type: Entity.Books,
      authors: bookLookup.authors.map(linkMap),
      pages: bookLookup.numberOfPages,
      isbn: barcode,
      publicationDate: bookLookup.publishDate,
      publisher: bookLookup.publishers.map(pub => pub.name)[0],
      title: bookLookup.title,
      subtitle: bookLookup.subtitle,
      genres: bookLookup.subjects.map(linkMap)
    } as BookEntity
  }
}