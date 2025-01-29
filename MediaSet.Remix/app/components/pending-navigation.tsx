import { useNavigation } from "@remix-run/react";
import Spinner from "./spinner";
import { useEffect, useState } from "react";

export default function PendingNavigation() {
  const navigation = useNavigation();
  const [mainEl, setMainEl] = useState<HTMLElement | null>(null);
  useEffect(() => {
    setMainEl(document.getElementById('main-content'));
  });

  const spinner = (
    <>
      <div style={{ width: `${mainEl?.offsetWidth}px`, height: `${mainEl?.offsetHeight}px` }} className="absolute z-40 mt-16 bg-gray-900 bg-opacity-60"></div>
      <div className="fixed z-50 top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 flex justify-center text-slate-400">
        <div id="dialog-body">
          <Spinner size={84} />
        </div>
      </div>
    </>
  );
  return navigation.state === "loading" ? (
    spinner
  ) : null;
}