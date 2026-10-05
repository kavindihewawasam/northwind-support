import { agentsApi } from '../api/agents';
import { categoriesApi } from '../api/categories';
import { customersApi } from '../api/customers';
import type { Agent, Category, CustomerListItem } from '../types/api';
import { useAsyncData } from './useAsyncData';

export interface ReferenceData {
  agents: Agent[];
  categories: Category[];
  customers: CustomerListItem[];
}

// Module level, so its identity is stable and useAsyncData only fetches once.
const loadReferenceData = async (): Promise<ReferenceData> => {
  const [agents, categories, customers] = await Promise.all([
    agentsApi.getAgents(),
    categoriesApi.getCategories(),
    customersApi.getCustomers(),
  ]);

  return { agents, categories, customers };
};

/** The agents, categories and customers the filter bar and the create form need. */
export function useReferenceData() {
  return useAsyncData(loadReferenceData, 'Unable to load reference data.');
}
