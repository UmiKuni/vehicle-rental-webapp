import { useMemo } from 'react'
import { Star } from 'lucide-react'
import type { VehicleReview } from '@/types/vehicle.types'

const PAGE_SIZE = 5

export function VehicleReviews({
  reviews = [],
  totalCount = reviews.length,
  hasMore = false,
  isLoadingMore = false,
  isLoading = false,
  isError = false,
  onLoadMore,
}: {
  reviews?: VehicleReview[]
  totalCount?: number
  hasMore?: boolean
  isLoadingMore?: boolean
  isLoading?: boolean
  isError?: boolean
  onLoadMore?: () => void
}) {
  const visibleReviews = useMemo(() => reviews.slice(0, Math.max(PAGE_SIZE, reviews.length)), [reviews])

  return (
    <section id="vehicle-reviews" className="detail-section vehicle-reviews" aria-labelledby="vehicle-reviews-title">
      <div className="detail-section-heading">
        <div><span className="detail-eyebrow">RENTER FEEDBACK</span><h2 id="vehicle-reviews-title">Reviews</h2></div>
        <span className="vehicle-review-total">{totalCount} reviews</span>
      </div>
      {isLoading ? (
        <p className="vehicle-reviews-empty">Loading reviews…</p>
      ) : isError ? (
        <p className="vehicle-reviews-empty">Reviews could not be loaded. Please try again later.</p>
      ) : reviews.length === 0 ? (
        <p className="vehicle-reviews-empty">{totalCount === 0 ? 'No reviews yet.' : 'Review details are currently unavailable.'}</p>
      ) : (
        <>
          <div className="vehicle-review-list">
            {visibleReviews.map((review) => (
              <article key={review.id} className="vehicle-review-card">
                <div className="vehicle-review-header">
                  <div><strong>{review.reviewerName}</strong><time dateTime={review.createdAt}>{new Intl.DateTimeFormat('en-US', { dateStyle: 'medium' }).format(new Date(review.createdAt))}</time></div>
                  <span><Star size={14} fill="currentColor" /> {review.rating.toFixed(1)}</span>
                </div>
                <p>{review.comment}</p>
              </article>
            ))}
          </div>
          {hasMore && onLoadMore && (
            <button type="button" className="vehicle-reviews-more" onClick={onLoadMore} disabled={isLoadingMore}>
              {isLoadingMore ? 'Loading reviews…' : 'Show more reviews'}
            </button>
          )}
        </>
      )}
    </section>
  )
}
