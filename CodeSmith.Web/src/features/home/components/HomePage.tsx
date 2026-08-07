// == Home Page == //
import { Link } from "react-router-dom";

export function HomePage() {
  return (
    <div className="flex h-full flex-col items-center p-6">
      <section className="my-auto w-full max-w-3xl text-center">
        <h1 className="mb-4 text-5xl font-bold text-white">Forge the skill. Then prove it.</h1>
        <p className="mb-10 text-lg text-gray-300">
          Multiple disciplines. One habit: deliberate practice.
        </p>

        {/* == Feature tiles: primary (Paired Programmer) above secondary pair == */}
        <div className="flex flex-col gap-4">
          {/* == Paired Programmer CTA (centerpiece) == */}
          <Link
            to="/pairedprogrammer"
            className="flex flex-col rounded-2xl border border-accent/40 bg-gray-900 px-10 py-10 text-left transition-colors hover:border-accent hover:bg-gray-800"
          >
            <span className="mb-3 text-2xl font-semibold text-white">Paired Programmer</span>
            <span className="text-base text-gray-400">
              Tackle coding challenges with an AI tutor. Pick a language and difficulty,
              write code, and get real-time guidance.
            </span>
            <span className="mt-6 text-base font-medium text-accent">Start coding →</span>
          </Link>

          {/* == Secondary labs == */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {/* == Prompt Lab CTA == */}
            <Link
              to="/prompt-lab"
              className="flex flex-col rounded-xl border border-gray-700 bg-gray-900 px-6 py-5 text-left transition-colors hover:border-accent hover:bg-gray-800"
            >
              <span className="mb-1.5 text-base font-semibold text-white">Prompt Lab</span>
              <span className="text-sm text-gray-400">
                Master prompt engineering. Craft system prompts and user messages that make
                AI models behave exactly as intended — across adversarial test suites.
              </span>
              <span className="mt-3 text-sm font-medium text-accent">Start prompting →</span>
            </Link>

            {/* == System Lab CTA == */}
            <Link
              to="/system-lab"
              className="flex flex-col rounded-xl border border-gray-700 bg-gray-900 px-6 py-5 text-left transition-colors hover:border-accent hover:bg-gray-800"
            >
              <span className="mb-1.5 text-base font-semibold text-white">System Lab</span>
              <span className="text-sm text-gray-400">
                Build infrastructure intuition. Justify design decisions across cloud, networking,
                and resilience scenarios — and get scored on your reasoning.
              </span>
              <span className="mt-3 text-sm font-medium text-accent">Start designing →</span>
            </Link>
          </div>
        </div>
      </section>

      {/* == Solo-maintainer / scale-to-zero note == */}
      <footer className="mt-10 max-w-xl text-center text-sm leading-relaxed text-gray-500">
        <p className="mb-3">
          CodeSmith is developed and maintained by one engineer. (Hello!)
        </p>
        <p className="mb-3">
          All infrastructure and token costs come out of my personal finances. In order to help
          reduce cost all servers scale to zero wherever possible. This means that servers need
          to warm up when they have not been used recently. This can cause longer load times and
          the occasional error during that warm up period.
        </p>
        <p className="mb-3">
          If you run into an error the first time you use a functionality please give the servers
          a few seconds to spin up then reattempt. Thank you for your patience.
        </p>
        <p>
          To support CodeSmith, please purchase credits, learn something new, and enjoy the craft
          of engineering. With love, Isaac {"<3"}
        </p>
      </footer>
    </div>
  );
}
