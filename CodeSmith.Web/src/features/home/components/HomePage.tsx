// == Home Page == //
import { Link } from "react-router-dom";

export function HomePage() {
  return (
    <div className="flex h-full items-center justify-center p-6">
      <section className="max-w-2xl text-center">
        <h1 className="mb-4 text-5xl font-bold text-white">Practice. Learn. Level up.</h1>
        <p className="mb-10 text-lg text-gray-300">
          Two ways to sharpen your skills with AI.
        </p>

        <div className="flex flex-col gap-4 sm:flex-row sm:justify-center">
          {/* == Paired Programmer CTA == */}
          <Link
            to="/pairedprogrammer"
            className="flex flex-col rounded-xl border border-gray-700 bg-gray-900 px-8 py-6 text-left transition-colors hover:border-monokai-pink hover:bg-gray-800"
          >
            <span className="mb-2 text-lg font-semibold text-white">Paired Programmer</span>
            <span className="text-sm text-gray-400">
              Tackle coding challenges with an AI tutor. Pick a language and difficulty,
              write code, and get real-time guidance.
            </span>
            <span className="mt-4 text-sm font-medium text-monokai-pink">Start coding →</span>
          </Link>

          {/* == Prompt Lab CTA == */}
          <Link
            to="/prompt-lab"
            className="flex flex-col rounded-xl border border-gray-700 bg-gray-900 px-8 py-6 text-left transition-colors hover:border-monokai-pink hover:bg-gray-800"
          >
            <span className="mb-2 text-lg font-semibold text-white">Prompt Lab</span>
            <span className="text-sm text-gray-400">
              Master prompt engineering. Craft system prompts and user messages that make
              AI models behave exactly as intended — across adversarial test suites.
            </span>
            <span className="mt-4 text-sm font-medium text-monokai-pink">Start prompting →</span>
          </Link>
        </div>
      </section>
    </div>
  );
}
