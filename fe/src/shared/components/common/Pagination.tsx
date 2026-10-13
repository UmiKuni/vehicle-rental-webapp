import { ChevronLeft, ChevronRight } from 'lucide-react'
import { cn } from '@/shared/utils'

interface PaginationProps {
  page: number
  totalPages: number
  totalElements: number
  size: number
  onPageChange: (page: number) => void
}

export function Pagination({ page, totalPages, totalElements, size, onPageChange }: PaginationProps) {
  const from = totalElements === 0 ? 0 : page * size + 1
  const to = Math.min((page + 1) * size, totalElements)

  const pages = buildPageList(page, totalPages)

  return (
    <div className="flex items-center justify-between px-1 py-3">
      <p className="text-sm text-muted-foreground">
        Hiển thị <span className="font-medium">{from}–{to}</span> trong{' '}
        <span className="font-medium">{totalElements}</span> kết quả
      </p>

      <div className="flex items-center gap-1">
        <PagButton
          onClick={() => onPageChange(page - 1)}
          disabled={page === 0}
          aria-label="Trang trước"
        >
          <ChevronLeft className="size-4" />
        </PagButton>

        {pages.map((p, i) =>
          p === '...' ? (
            <span key={`ellipsis-${i}`} className="px-2 text-muted-foreground">…</span>
          ) : (
            <PagButton
              key={p}
              onClick={() => onPageChange(p as number)}
              active={p === page}
            >
              {(p as number) + 1}
            </PagButton>
          )
        )}

        <PagButton
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages - 1}
          aria-label="Trang sau"
        >
          <ChevronRight className="size-4" />
        </PagButton>
      </div>
    </div>
  )
}

function PagButton({
  children,
  onClick,
  disabled,
  active,
  'aria-label': ariaLabel,
}: {
  children: React.ReactNode
  onClick: () => void
  disabled?: boolean
  active?: boolean
  'aria-label'?: string
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      aria-label={ariaLabel}
      className={cn(
        'inline-flex h-8 min-w-8 items-center justify-center rounded-md px-2 text-sm font-medium transition-colors',
        'disabled:pointer-events-none disabled:opacity-40',
        active
          ? 'bg-primary text-primary-foreground shadow-sm'
          : 'hover:bg-accent hover:text-accent-foreground'
      )}
    >
      {children}
    </button>
  )
}

function buildPageList(current: number, total: number): (number | '...')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i)
  if (current < 4) return [0, 1, 2, 3, 4, '...', total - 1]
  if (current >= total - 4) return [0, '...', total - 5, total - 4, total - 3, total - 2, total - 1]
  return [0, '...', current - 1, current, current + 1, '...', total - 1]
}
