import { useNavigation } from "@remix-run/react";
import Spinner from "./spinner";

export default function PendingNavigation() {
  const navigation = useNavigation();
  const spinner = (
    <>
      <div className="absolute z-40 w-full h-full mt-16 bg-gray-900 bg-opacity-60"></div>
      <div className="fixed z-50 top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 flex justify-center">
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