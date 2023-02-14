import { RenderableProps } from 'preact';
import { Head } from "$fresh/runtime.ts";
import { Header } from "../components/Header.tsx";

interface LayoutProps {
  route: string;
  title?: string;
}

export default function Layout({ route, title, children }: RenderableProps<LayoutProps>) {
  const siteTitle = `My Media Set${title ? ' - ' + title : ''}`;
  return (
    <>
      <Head>
        <title>{siteTitle}</title>
      </Head>
      <div class="p-4 mx-auto max-w-screen-md">
        <Header active={route} />
        <div class="my-6 mx-8">
          {children}
        </div>
      </div>
    </>
  );
}