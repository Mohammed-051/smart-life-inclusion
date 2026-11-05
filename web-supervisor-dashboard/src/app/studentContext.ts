import { createContext, useContext } from 'react';

/** The learner picked in the app shell. Null when this account has no learners assigned. */
export const StudentContext = createContext<string | null>(null);

export function useStudentId(): string | null {
  return useContext(StudentContext);
}
