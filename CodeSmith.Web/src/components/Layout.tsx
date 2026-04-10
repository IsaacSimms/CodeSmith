// == App Layout == //
import { Link, Outlet } from "react-router-dom";

export function Layout() {
  return (
    <div className="flex h-screen flex-col bg-gray-900">
      {/* == Top Nav == */}
      <nav className="flex items-center border-b border-gray-700 px-6 py-3">
        <Link
          to="/home"
          className="text-lg font-bold text-white transition-colors hover:text-monokai-pink"
        >
          CodeSmith
        </Link>
      </nav>

      {/* == Route Content == */}
      <main className="flex-1 overflow-hidden">
        <Outlet />
      </main>
    </div>
  );
}
