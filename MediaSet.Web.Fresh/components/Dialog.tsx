import { RenderableProps } from 'preact';

export default function Dialog(props: RenderableProps<unknown>) {
  return (
    <>
      <div class='absolute top-0 left-0 w-full h-full flex items-center justify-center bg-gray-600 bg-opacity-50 z-10'>
        <div class='bg-white p-4 rounded'>
          {props.children}
        </div>
      </div>
    </>
  );
}
