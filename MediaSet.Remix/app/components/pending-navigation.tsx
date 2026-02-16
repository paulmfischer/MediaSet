import { useNavigation } from '@remix-run/react';
import Spinner from './spinner';

export default function PendingNavigation() {
  const navigation = useNavigation();

  const spinner = (
    <>
      <div className="fixed inset-0 2xl:mx-14 top-16 z-40 bg-gray-900 bg-opacity-60"></div>
      <div className="fixed z-50 top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 flex justify-center text-slate-400">
        <div id="dialog-body">
          <Spinner size={84} />
        </div>
      </div>
    </>
  );

  return navigation.state === 'loading' || navigation.state === 'submitting' ? spinner : null;
}
