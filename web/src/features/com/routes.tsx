import type { RouteObject } from 'react-router-dom';
import { PostsPage } from './pages/PostsPage';

export const comRoutes: RouteObject[] = [
  { path: 'com/posts', element: <PostsPage /> },
];
