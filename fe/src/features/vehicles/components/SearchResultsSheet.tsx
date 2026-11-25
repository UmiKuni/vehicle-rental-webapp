import { useCallback, useLayoutEffect, useRef, useState } from 'react'
import { Search } from 'lucide-react'
import type { Vehicle } from '@/types/vehicle.types'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import { ErrorState } from '@/shared/components/common/StateViews'
import { VehicleCard } from './VehicleCard'

const COLLAPSED_HEIGHT_RATIO = 0.4
const COLLAPSED_MAX_HEIGHT = 300
const SNAP_THRESHOLD_RATIO = 0.5

interface SearchResultsSheetProps {
  vehicles: Vehicle[]
  selectedVehicle?: Vehicle
  expanded: boolean
  loading: boolean
  error: boolean
  onExpandedChange: (expanded: boolean) => void
  onRetry: () => void
  onClearFilters: () => void
  onBook: (vehicle: Vehicle) => void
  onViewDetails: (vehicle: Vehicle) => void
}

interface DragSession {
  pointerId: number
  startY: number
  startTop: number
  currentTop: number
  moved: boolean
}

export function SearchResultsSheet({
  vehicles,
  selectedVehicle,
  expanded,
  loading,
  error,
  onExpandedChange,
  onRetry,
  onClearFilters,
  onBook,
  onViewDetails,
}: SearchResultsSheetProps) {
  const sheetRef = useRef<HTMLElement>(null)
  const dragRef = useRef<DragSession | null>(null)
  const suppressClickRef = useRef(false)
  const [dragTop, setDragTop] = useState<number | null>(null)
  const [dragging, setDragging] = useState(false)
  const [snapPoints, setSnapPoints] = useState({ expandedTop: 123, collapsedTop: 420 })

  const measureSnapPoints = useCallback(() => {
    const sheet = sheetRef.current
    const parent = sheet?.parentElement
    if (!sheet || !parent) return

    const parentRect = parent.getBoundingClientRect()
    const collapsedHeight = Math.min(COLLAPSED_MAX_HEIGHT, parentRect.height * COLLAPSED_HEIGHT_RATIO)
    const collapsedTop = Math.max(parentRect.height * (1 - COLLAPSED_HEIGHT_RATIO), parentRect.height - collapsedHeight)

    setSnapPoints({
      expandedTop: 0,
      collapsedTop,
    })
  }, [])

  useLayoutEffect(() => {
    measureSnapPoints()
    const parent = sheetRef.current?.parentElement
    if (!parent) return

    const observer = new ResizeObserver(measureSnapPoints)
    observer.observe(parent)
    window.visualViewport?.addEventListener('resize', measureSnapPoints)
    return () => {
      observer.disconnect()
      window.visualViewport?.removeEventListener('resize', measureSnapPoints)
    }
  }, [measureSnapPoints, vehicles.length, selectedVehicle?.id, loading, error])

  const finishDrag = (event: React.PointerEvent<HTMLButtonElement>, cancelled = false) => {
    const drag = dragRef.current
    if (!drag || drag.pointerId !== event.pointerId) return
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId)
    }

    if (!cancelled && drag.moved) {
      const parentHeight = sheetRef.current?.parentElement?.getBoundingClientRect().height ?? window.innerHeight
      onExpandedChange(drag.currentTop < parentHeight * SNAP_THRESHOLD_RATIO)
      suppressClickRef.current = true
    }

    dragRef.current = null
    setDragging(false)
    setDragTop(null)
  }

  const sheetTop = dragTop ?? (expanded ? snapPoints.expandedTop : snapPoints.collapsedTop)
  const visibleVehicles = expanded ? vehicles : selectedVehicle ? [selectedVehicle] : []
  const resultLabel = loading
    ? 'Finding cars…'
    : expanded
      ? `${vehicles.length} results found`
      : `${vehicles.length} cars nearby`

  return (
    <section
      ref={sheetRef}
      className={`search-results-sheet ${expanded ? 'is-expanded' : 'is-collapsed'} ${dragging ? 'is-dragging' : ''}`}
      style={{ top: `${sheetTop}px` }}
      aria-label="Search results"
    >
      <button
        type="button"
        className="sheet-handle-area"
        onClick={() => {
          if (suppressClickRef.current) {
            suppressClickRef.current = false
            return
          }
          onExpandedChange(!expanded)
        }}
        onKeyDown={(event) => {
          if (event.key === 'ArrowUp') onExpandedChange(true)
          if (event.key === 'ArrowDown') onExpandedChange(false)
        }}
        onPointerDown={(event) => {
          const sheet = sheetRef.current
          const parent = sheet?.parentElement
          if (!sheet || !parent) return
          const currentTop = sheet.getBoundingClientRect().top - parent.getBoundingClientRect().top
          dragRef.current = {
            pointerId: event.pointerId,
            startY: event.clientY,
            startTop: currentTop,
            currentTop,
            moved: false,
          }
          setDragTop(currentTop)
          setDragging(true)
          event.currentTarget.setPointerCapture(event.pointerId)
        }}
        onPointerMove={(event) => {
          const drag = dragRef.current
          if (!drag || drag.pointerId !== event.pointerId) return
          const nextTop = Math.min(
            snapPoints.collapsedTop,
            Math.max(snapPoints.expandedTop, drag.startTop + event.clientY - drag.startY),
          )
          drag.currentTop = nextTop
          drag.moved ||= Math.abs(event.clientY - drag.startY) > 5
          setDragTop(nextTop)
        }}
        onPointerUp={(event) => finishDrag(event)}
        onPointerCancel={(event) => finishDrag(event, true)}
        aria-label={expanded ? 'Collapse vehicle list' : 'Expand vehicle list'}
        aria-expanded={expanded}
      >
        <span className="sheet-handle" />
      </button>

      <div className="search-results-heading"><h1>{resultLabel}</h1></div>
      <div className="sheet-content">
        {loading && <div className="grid gap-3"><CardSkeleton /><CardSkeleton /></div>}
        {error && <ErrorState message="Could not load vehicles." onRetry={onRetry} />}
        {!loading && !error && vehicles.length === 0 && (
          <div className="search-empty">
            <Search size={24} />
            <strong>No matching cars found</strong>
            <span>Try changing your filters or search area.</span>
            <button type="button" onClick={onClearFilters}>Clear filters</button>
          </div>
        )}
        {!loading && !error && vehicles.length > 0 && (
          <div className={expanded ? 'search-vehicle-list' : 'search-featured-vehicle'}>
            {visibleVehicles.map((vehicle) => (
              <VehicleCard
                key={vehicle.id}
                vehicle={vehicle}
                onBook={onBook}
                onViewDetails={onViewDetails}
              />
            ))}
          </div>
        )}
      </div>
    </section>
  )
}
