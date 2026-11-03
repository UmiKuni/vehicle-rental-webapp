import { useMemo, useState } from 'react'
import { Star } from 'lucide-react'
import type { VehicleReview } from '@/types/vehicle.types'

const PAGE_SIZE = 5

export function VehicleReviews({ reviews = [], totalCount = reviews.length }: { reviews?: VehicleReview[]; totalCount?: number }) {
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE)
  const visibleReviews = useMemo(() => reviews.slice(0, visibleCount), [reviews, visibleCount])

  return (
    <section id="vehicle-reviews" className="detail-section vehicle-reviews" aria-labelledby="vehicle-reviews-title">
      <div className="detail-section-heading">
        <div><span className="detail-eyebrow">RENTER FEEDBACK</span><h2 id="vehicle-reviews-title">Reviews</h2></div>
        <span className="vehicle-review-total">{totalCount} reviews</span>
      </div>
      {reviews.length === 0 ? (
        <p className="vehicle-reviews-empty">{totalCount === 0 ? 'No reviews yet.' : 'Review details are currently unavailable.'}</p>
      ) : (
        <>
          <div className="vehicle-review-list">
            {visibleReviews.map((review) => (
              <article key={review.id} className="vehicle-review-card">
                <div className="vehicle-review-header">
                  <div><strong>{review.reviewerName}</strong><time dateTime={review.createdAt}>{new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(review.createdAt))}</time></div>
                  <span><Star size={14} fill="currentColor" /> {review.rating.toFixed(1)}</span>
                </div>
                <p>{review.comment}</p>
              </article>
            ))}
          </div>
          {visibleCount < reviews.length && (
            <button type="button" className="vehicle-reviews-more" onClick={() => setVisibleCount((count) => count + PAGE_SIZE)}>
              Show {Math.min(PAGE_SIZE, reviews.length - visibleCount)} more reviews
            </button>
          )}
        </>
      )}
    </section>
  )
}
