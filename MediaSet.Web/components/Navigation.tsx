import { JSX } from 'preact';
//import { FontAwesomeIcon } from 'npm:@fortawesome/react-fontawesome@latest';
//import { faBook } from 'npm:@fortawesome/free-solid-svg-icons@6.6.0';

export function Navigation(props: JSX.HTMLAttributes<HTMLElement>) {
  return (
    <nav {...props}>
      <ul>
        <li>
          <a href="/books" className="dark:text-slate-400 flex items-center">
            <div className="w-8 h-8 lg:w-4 lg:h-4 mr-4">
              {/* <FontAwesomeIcon icon={faBook} /> */}
            </div>
            <span className="text-3xl lg:text-lg text-blue-600 dark:text-blue-400">Books</span>
          </a>
        </li>
      </ul>
    </nav>
  );
};
