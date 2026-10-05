import type { Category } from '../types/api';
import { request } from './client';

export const categoriesApi = {
  getCategories: () => request<Category[]>('/categories'),
};
