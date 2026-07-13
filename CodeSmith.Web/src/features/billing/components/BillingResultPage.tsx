// == Stripe Checkout redirect stubs (Inc 1 — no account UI) == //
import { Link } from "react-router-dom";

type BillingResultKind = "success" | "cancel";

export function BillingResultPage({ kind }: { kind: BillingResultKind }) {
  const isSuccess = kind === "success";

  return (
    <div className="flex h-full flex-col items-center justify-center p-6">
      <div className="max-w-md rounded-xl border border-gray-700 bg-gray-900 px-8 py-10 text-center">
        <h1 className="mb-3 text-2xl font-semibold text-white">
          {isSuccess ? "Payment received" : "Checkout canceled"}
        </h1>
        <p className="mb-8 text-sm text-gray-300">
          {isSuccess
            ? "Your credit pack purchase completed. You can close this tab or return home."
            : "No charge was made. You can return home and try again later."}
        </p>
        <Link
          to="/home"
          className="inline-block rounded-md bg-accent px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-accent-hover"
        >
          Back to home
        </Link>
      </div>
    </div>
  );
}
