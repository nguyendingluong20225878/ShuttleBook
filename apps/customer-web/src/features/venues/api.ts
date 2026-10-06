const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export async function getPublic<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${apiBaseUrl}/api/v1${path}`, { signal });
  if (!response.ok) {
    if (response.status === 404) throw new Error('Cơ sở hoặc sân không còn hiển thị.');
    if (response.status === 400) throw new Error('Ngày hoặc vị trí tìm kiếm chưa hợp lệ.');
    throw new Error('Chưa tải được dữ liệu sân. Vui lòng thử lại.');
  }
  const result = await response.json() as { data?: T };
  if (!result.data) throw new Error('Phản hồi từ máy chủ chưa hợp lệ.');
  return result.data;
}

export function publicImageUrl(path: string | null): string | null {
  return path ? `${apiBaseUrl}${path}` : null;
}
