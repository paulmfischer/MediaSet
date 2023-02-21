import { UnknownPageProps } from '$fresh/server.ts';
import Layout from '../components/Layout.tsx';
import { IconAnchor } from '../components/IconAnchor.tsx';
import IconRobot from 'tabler-icons/robot.tsx';

export default function NotFoundPage({ route }: UnknownPageProps) {
  return (
    <Layout route={route}>
      <div class='flex flex-col p-6'>
        <p class='flex justify-center'>
          This is not the page you are looking for.
        </p>
        <div class='flex justify-center pt-4'>
          <IconAnchor href='/'>
            <IconRobot class='w-48 h-48' />
          </IconAnchor>
        </div>
      </div>
    </Layout>
  );
}
