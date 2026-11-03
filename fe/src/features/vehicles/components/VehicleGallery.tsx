import { useEffect, useRef, useState } from 'react'
import { ChevronLeft, ChevronRight, Expand, X } from 'lucide-react'
import type { Vehicle } from '@/types/vehicle.types'

export function VehicleGallery({ vehicle }: { vehicle: Vehicle }) {
  const images = [...new Set([vehicle.imageUrl, ...(vehicle.images ?? [])].filter((src): src is string => Boolean(src)))]
  const [activeIndex, setActiveIndex] = useState(0)
  const [isFullscreen, setIsFullscreen] = useState(false)
  const [touchStartX, setTouchStartX] = useState<number | null>(null)
  const suppressClick = useRef(false)
  const image = images[activeIndex] ?? vehicle.imageUrl

  const showImage = (index: number) => setActiveIndex((index + images.length) % images.length)

  useEffect(() => {
    if (!isFullscreen) return
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsFullscreen(false)
      if (event.key === 'ArrowRight') setActiveIndex((index) => (index + 1) % images.length)
      if (event.key === 'ArrowLeft') setActiveIndex((index) => (index - 1 + images.length) % images.length)
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [isFullscreen, images.length])

  const handleTouchEnd = (event: React.TouchEvent) => {
    if (touchStartX === null) return
    const delta = event.changedTouches[0].clientX - touchStartX
    if (Math.abs(delta) > 45) {
      showImage(activeIndex + (delta < 0 ? 1 : -1))
      suppressClick.current = true
    }
    setTouchStartX(null)
  }

  return (
    <>
      <section className="vehicle-gallery" aria-label="Vehicle photos">
        <button
          type="button"
          className="vehicle-gallery-image"
          onClick={(event) => {
            if (suppressClick.current) {
              event.preventDefault()
              suppressClick.current = false
              return
            }
            setIsFullscreen(true)
          }}
          onTouchStart={(event) => setTouchStartX(event.touches[0].clientX)}
          onTouchEnd={handleTouchEnd}
          aria-label="Open vehicle photo full screen"
        >
          <img src={image} alt={`${vehicle.name}, photo ${activeIndex + 1}`} />
          <span className="vehicle-gallery-expand"><Expand size={16} /> View photo</span>
        </button>
        {images.length > 1 && (
          <>
            <button type="button" className="vehicle-gallery-arrow previous" onClick={() => showImage(activeIndex - 1)} aria-label="Previous photo"><ChevronLeft size={20} /></button>
            <button type="button" className="vehicle-gallery-arrow next" onClick={() => showImage(activeIndex + 1)} aria-label="Next photo"><ChevronRight size={20} /></button>
            <div className="vehicle-gallery-count" aria-live="polite">{activeIndex + 1} / {images.length}</div>
            <div className="vehicle-gallery-thumbnails" aria-label="Choose a vehicle photo">
              {images.map((src, index) => (
                <button key={`${src}-${index}`} type="button" onClick={() => showImage(index)} className={index === activeIndex ? 'active' : ''} aria-label={`Show photo ${index + 1}`} aria-pressed={index === activeIndex}>
                  <img src={src} alt="" />
                </button>
              ))}
            </div>
          </>
        )}
        {images.length === 1 && <div className="vehicle-gallery-count">1 / 1</div>}
      </section>

      {isFullscreen && (
        <div className="vehicle-gallery-lightbox" role="dialog" aria-modal="true" aria-label="Vehicle photo viewer" onClick={() => setIsFullscreen(false)}>
          <button type="button" className="vehicle-gallery-close" onClick={() => setIsFullscreen(false)} aria-label="Close photo viewer"><X size={22} /></button>
          <img src={image} alt={`${vehicle.name}, photo ${activeIndex + 1}`} onClick={(event) => event.stopPropagation()} />
          {images.length > 1 && (
            <>
              <button type="button" className="vehicle-gallery-arrow previous" onClick={(event) => { event.stopPropagation(); showImage(activeIndex - 1) }} aria-label="Previous photo"><ChevronLeft size={24} /></button>
              <button type="button" className="vehicle-gallery-arrow next" onClick={(event) => { event.stopPropagation(); showImage(activeIndex + 1) }} aria-label="Next photo"><ChevronRight size={24} /></button>
            </>
          )}
          <span className="vehicle-gallery-count">{activeIndex + 1} / {images.length}</span>
        </div>
      )}
    </>
  )
}
