// == App Layout == //
import { Link, Outlet } from "react-router-dom";
import { useNavigationContext } from "../contexts/NavigationContext";

export function Layout() {
  const { resetAll } = useNavigationContext();

  return (
    <div className="flex h-screen flex-col bg-gray-900">
      {/* == Top Nav == */}
      <nav className="flex items-center gap-6 border-b border-gray-700 px-6 py-3">
        <Link
          to="/home"
          onClick={resetAll}
          className="text-lg font-bold text-white transition-colors hover:text-monokai-pink"
        >
          CodeSmith
        </Link>
        <Link
          to="/pairedprogrammer"
          onClick={resetAll}
          className="text-sm text-gray-400 transition-colors hover:text-white"
        >
          Paired Programmer
        </Link>
        <Link
          to="/prompt-lab"
          onClick={resetAll}
          className="text-sm text-gray-400 transition-colors hover:text-white"
        >
          Prompt Lab
        </Link>
      </nav>

      {/* == Route Content == */}
      <main className="flex-1 overflow-hidden">
        <Outlet />
      </main>
    </div>
  );
}
