import apiClient from '@/shared/lib/axios'

export interface AuditEntry {
  id: string
  timestamp: string
  actorId: string
  ipAddress: string
  action: string
  entityName: string
  entityId: string
  beforeState: string | null
  afterState: string | null
}

export interface AuditPage {
  items: AuditEntry[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface AuditFilters {
  page: number
  pageSize: number
  actorId?: string
  entityName?: string
  entityId?: string
  action?: string
  from?: string
  to?: string
}

export const auditApi = {
  getPage: async (filters: AuditFilters) =>
    (await apiClient.get<AuditPage>('/audit', { params: filters })).data,

  getById: async (id: string) =>
    (await apiClient.get<AuditEntry>(`/audit/${encodeURIComponent(id)}`)).data,

  getEntityHistory: async (entityName: string, entityId: string) =>
    (await apiClient.get<AuditEntry[]>(`/audit/entity/${encodeURIComponent(entityName)}/${encodeURIComponent(entityId)}`)).data,

  getActorHistory: async (actorId: string) =>
    (await apiClient.get<AuditEntry[]>(`/audit/actor/${encodeURIComponent(actorId)}`)).data,
}
