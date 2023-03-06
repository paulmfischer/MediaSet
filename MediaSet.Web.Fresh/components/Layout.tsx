import { RenderableProps } from 'preact';
import { Head } from '$fresh/runtime.ts';
import { Header } from '../components/Header.tsx';

interface LayoutProps {
  route: string;
  title?: string;
}

export default function Layout({ route, title, children }: RenderableProps<LayoutProps>) {
  const siteTitle = `My Media Set${title ? ' - ' + title : ''}`;
  return (
    <div class='min-h-screen'>
      <Head>
        <title>{siteTitle}</title>
      </Head>
      <Header activeRoute={route} />
      {/* xl:mt-12 */}
      <main class='flex '>
        <section class='container px-3 mx-auto mt-4'>
          {children}
        </section>
      </main>
    </div>
  );
}
