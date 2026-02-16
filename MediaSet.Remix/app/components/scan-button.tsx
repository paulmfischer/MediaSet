import React, { useState, useEffect, Suspense } from 'react';
import { useSubmit } from '@remix-run/react';

const BarcodeScanner = React.lazy(() => import('~/components/barcode-scanner'));

type Props = {
  inputId?: string;
  fieldName?: string;
  disabled?: boolean;
  buttonLabel?: string;
};

export default function ScanButton({ inputId = 'barcode', fieldName, disabled = false, buttonLabel = 'Scan' }: Props) {
  const [open, setOpen] = useState(false);
  const [isMobile, setIsMobile] = useState(false);
  const submit = useSubmit();

  useEffect(() => {
    // Check if device is mobile based on touch capability and screen size
    const checkMobile = () => {
      const hasTouch = 'ontouchstart' in window || navigator.maxTouchPoints > 0;
      const isMobileScreen = window.matchMedia('(max-width: 768px)').matches;
      const isMobileUA = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);

      setIsMobile(hasTouch && (isMobileScreen || isMobileUA));
    };

    checkMobile();

    // Recheck on resize
    window.addEventListener('resize', checkMobile);
    return () => window.removeEventListener('resize', checkMobile);
  }, []);

  const handleDetected = (value: string) => {
    const input = document.getElementById(inputId) as HTMLInputElement | null;
    if (input) input.value = value;

    const form = input?.closest('form') as HTMLFormElement | null;
    const fd = form ? new FormData(form) : new FormData();
    fd.append('intent', 'lookup');
    fd.append('fieldName', fieldName ?? inputId);
    fd.append('identifierValue', value);

    submit(fd, { method: 'post' });
    setOpen(false);
  };

  // Only render scan button on mobile devices
  if (!isMobile) {
    return null;
  }

  return (
    <>
      <button type="button" onClick={() => setOpen(true)} disabled={disabled}>
        {buttonLabel}
      </button>
      <Suspense fallback={null}>
        <BarcodeScanner open={open} onClose={() => setOpen(false)} onDetected={handleDetected} />
      </Suspense>
    </>
  );
}
