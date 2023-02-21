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
    <>
      <Head>
        <title>{siteTitle}</title>
      </Head>
      <main class='min-h-screen w-full'>
        <Header active={route} />
        <div class='my-2 mx-8'>
          {children}
        </div>
      </main>
    </>
  );
}
