import type { RouteObject } from 'react-router-dom';
import { FeedPage } from './pages/FeedPage';
import { PostsPage } from './pages/PostsPage';

export const comRoutes: RouteObject[] = [
  { path: 'com/posts', element: <PostsPage /> },
  { path: 'com/feed', element: <FeedPage /> },
];
