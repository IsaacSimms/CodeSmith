// == Home Page == //
import { Link } from "react-router-dom";

export function HomePage() {
  return (
    <div className="flex h-full items-center justify-center p-6">
      <section className="max-w-xl text-center">
        <h1 className="mb-4 text-5xl font-bold text-white">Practice. Learn. Level up.</h1>
        <p className="mb-8 text-lg text-gray-300">
          Sharpen your problem-solving skills with an AI pair programmer. Pick a
          difficulty, tackle a challenge, and get real-time guidance.
        </p>
        <Link
          to="/pairedprogrammer"
          className="inline-block rounded-lg bg-monokai-pink px-6 py-3 font-semibold text-white transition-colors hover:bg-monokai-pink-light"
        >
          Start Paired Programming
        </Link>
      </section>
    </div>
  );
}
