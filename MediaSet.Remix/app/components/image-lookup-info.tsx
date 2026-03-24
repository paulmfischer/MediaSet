import { Form } from '@remix-run/react';
import { ImageOff } from 'lucide-react';
import type { ImageLookupData } from '~/models';

type ImageLookupInfoProps = {
  imageLookup: ImageLookupData;
  isClearing?: boolean;
};

export default function ImageLookupInfo({ imageLookup, isClearing = false }: ImageLookupInfoProps) {
  return (
    <div className="w-full rounded-lg border border-orange-500/30 bg-orange-500/10 p-3 mt-3 text-xs">
      <div className="mb-2 flex items-center gap-1.5 text-orange-400">
        <ImageOff className="h-3.5 w-3.5 shrink-0" />
        <span className="font-medium">Image Lookup Failed</span>
      </div>
      <div className="space-y-1 text-zinc-400">
        <p suppressHydrationWarning>
          <span className="text-zinc-500">Attempted:</span> {new Date(imageLookup.lookupAttemptedAt).toLocaleString()}
        </p>
        {imageLookup.failureReason && (
          <p>
            <span className="text-zinc-500">Reason:</span> {imageLookup.failureReason}
          </p>
        )}
        {imageLookup.permanentFailure && (
          <p className="text-red-400">Permanent failure — will not retry automatically</p>
        )}
      </div>
      <Form method="post" className="mt-2">
        <input type="hidden" name="intent" value="clear-image-lookup" />
        <button type="submit" disabled={isClearing}>
          {isClearing ? 'Clearing...' : 'Clear & retry'}
        </button>
      </Form>
    </div>
  );
}
