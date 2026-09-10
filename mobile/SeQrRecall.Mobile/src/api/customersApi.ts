import { api } from '@api/client';
import { DEFAULT_PAGE_SIZE } from '@constants/index';
import type { Customer, PagedRequest, PagedResult } from '@models/index';

export interface CustomerFields {
  name: string;
  companyName?: string | null;
  mobile?: string | null;
  email?: string | null;
}

export interface PhotoUpload {
  uri: string;
  name: string;
  type: string;
}

export function listCustomers(request: PagedRequest = {}): Promise<PagedResult<Customer>> {
  const pageNumber = request.pageNumber ?? 1;
  const pageSize = request.pageSize ?? DEFAULT_PAGE_SIZE;
  const params = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
  });
  const search = request.search?.trim();
  if (search) {
    params.set('search', search);
  }

  return api.get<PagedResult<Customer>>(`/customers?${params.toString()}`);
}

export function createCustomer(fields: CustomerFields): Promise<Customer> {
  return api.post<Customer>('/customers', {
    name: fields.name,
    companyName: emptyToNull(fields.companyName),
    mobile: emptyToNull(fields.mobile),
    email: emptyToNull(fields.email),
  });
}

export function getCustomer(customerId: string): Promise<Customer> {
  return api.get<Customer>(`/customers/${customerId}`);
}

export function updateCustomer(customerId: string, fields: CustomerFields): Promise<Customer> {
  return api.put<Customer>(`/customers/${customerId}`, {
    name: fields.name,
    companyName: emptyToNull(fields.companyName),
    mobile: emptyToNull(fields.mobile),
    email: emptyToNull(fields.email),
  });
}

export function deleteCustomer(customerId: string): Promise<unknown> {
  return api.delete(`/customers/${customerId}`);
}

export function uploadCustomerPhoto(customerId: string, file: PhotoUpload): Promise<Customer> {
  const form = new FormData();
  const uri = file.uri.startsWith('file://') ? file.uri : `file://${file.uri}`;
  form.append('file', {
    uri,
    name: file.name,
    type: file.type,
  } as unknown as Blob);
  return api.upload<Customer>(`/customers/${customerId}/photo`, form);
}

export async function getCustomerPhotoDataUri(customerId: string): Promise<string> {
  const binary = await api.getBinary(`/customers/${customerId}/photo`);
  return `data:${binary.contentType};base64,${binary.base64}`;
}

function emptyToNull(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}
