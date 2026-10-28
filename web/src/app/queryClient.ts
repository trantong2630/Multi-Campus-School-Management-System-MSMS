import { QueryCache, QueryClient } from '@tanstack/react-query';
import { showQueryError } from '@/shared/lib/feedback';

export const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: (error, query) => {
      if (!query.meta?.silent) showQueryError(error);
    },
  }),
  defaultOptions: {
    queries: {
      retry: false,
      refetchOnWindowFocus: false,
    },
  },
});
