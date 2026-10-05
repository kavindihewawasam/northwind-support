import { useCallback, useEffect, useState } from 'react';
import { toErrorMessage } from '../api/client';

interface AsyncState<T> {
  isLoading: boolean;
  data?: T;
  error?: string;
}

export interface AsyncData<T> extends AsyncState<T> {
  /** Runs the loader again, for a retry button or after a mutation. */
  reload: () => Promise<void>;
  /** Replaces the data without a round trip, e.g. with the response of a PATCH. */
  setData: (value: T) => void;
}

/**
 * Loads data when the given loader changes, and tracks loading and error state.
 *
 * Pass a loader with a stable identity (a module-level function, or one wrapped in
 * `useCallback`), otherwise this will fetch on every render.
 */
export function useAsyncData<T>(load: () => Promise<T>, errorMessage: string): AsyncData<T> {
  const [state, setState] = useState<AsyncState<T>>({ isLoading: true });

  const run = useCallback(async () => {
    setState((current) => ({ ...current, isLoading: true, error: undefined }));

    try {
      const data = await load();
      setState({ isLoading: false, data });
    } catch (error) {
      setState((current) => ({
        ...current,
        isLoading: false,
        error: toErrorMessage(error, errorMessage),
      }));
    }
  }, [load, errorMessage]);

  useEffect(() => {
    void run();
  }, [run]);

  const setData = useCallback((value: T) => {
    setState({ isLoading: false, data: value });
  }, []);

  return { ...state, reload: run, setData };
}
