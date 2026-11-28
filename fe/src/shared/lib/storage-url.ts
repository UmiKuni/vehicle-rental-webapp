export function resolvePrivateStorageUrl(viewUrl: string) {
  if (!import.meta.env.DEV) return viewUrl

  try {
    const url = new URL(viewUrl)
    if (url.hostname !== 'storage.carrental.vn' || !url.pathname.startsWith('/private/')) return viewUrl
    return `/api/storage/private/${url.pathname.slice('/private/'.length)}${url.search}`
  } catch {
    return viewUrl
  }
}
