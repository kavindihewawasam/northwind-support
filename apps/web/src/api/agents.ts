import type { Agent } from '../types/api';
import { request } from './client';

export const agentsApi = {
  getAgents: () => request<Agent[]>('/agents'),
};
