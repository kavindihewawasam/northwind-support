import type { CustomerDetail, CustomerListItem } from '../types/api';
import { request } from './client';

export const customersApi = {
  getCustomers: () => request<CustomerListItem[]>('/customers'),

  getCustomer: (id: number) => request<CustomerDetail>(`/customers/${id}`),
};
