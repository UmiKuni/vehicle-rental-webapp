import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Fuel, Gauge, MapPin, ShieldCheck, Star, Users } from 'lucide-react'
import { useVehicleDetail } from '../hooks/useVehicles'
import { BookingModal } from '../components/BookingModal'
import { VehicleGallery } from '../components/VehicleGallery'
import { VehicleReviews } from '../components/VehicleReviews'
import { ErrorState } from '@/shared/components/common/StateViews'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import type { Vehicle } from '@/types/vehicle.types'
import { ROUTES } from '@/shared/constants'

interface VehicleDetailLocationState {
  pickupAt?: string
  returnAt?: string
  resumeBookingVehicleId?: string
}

const money = (amount: number) => new Intl.NumberFormat('vi-VN').format(amount)

export default function VehicleDetailPage() {
  const { vehicleId } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const { data: vehicle, isLoading, isError, refetch } = useVehicleDetail(vehicleId)
  const [bookingVehicle, setBookingVehicle] = useState<Vehicle | null>(null)
  const [descriptionExpanded, setDescriptionExpanded] = useState(false)
  const reviewsRef = useRef<HTMLDivElement>(null)
  const locationState = (location.state as VehicleDetailLocationState | null) ?? {}

  useEffect(() => {
    setDescriptionExpanded(false)
    setBookingVehicle(null)
  }, [vehicleId])

  useEffect(() => {
    if (!vehicle || locationState.resumeBookingVehicleId !== vehicle.id) return
    setBookingVehicle(vehicle)
    navigate(`${location.pathname}${location.search}`, {
      replace: true,
      state: { pickupAt: locationState.pickupAt, returnAt: locationState.returnAt },
    })
  }, [vehicle, locationState.resumeBookingVehicleId, locationState.pickupAt, locationState.returnAt, location.pathname, location.search, navigate])

  if (isLoading) return <div className="vehicle-detail-loading"><CardSkeleton /></div>
  if (isError || !vehicle) return <div className="vehicle-detail-loading"><ErrorState message="Không thể tải thông tin xe." onRetry={() => void refetch()} /></div>

  const description = vehicle.description?.trim() ?? ''
  const shouldTruncateDescription = description.length > 190
  const visibleDescription = shouldTruncateDescription && !descriptionExpanded ? `${description.slice(0, 190).trimEnd()}…` : description
  const reviewCount = vehicle.reviewCount ?? vehicle.reviews?.length ?? 0
  const provider = vehicle.provider

  const goBack = () => {
    if (location.key !== 'default') navigate(-1)
    else navigate(ROUTES.SEARCH)
  }

  return (
    <div className="vehicle-detail-page">
      <header className="detail-topbar">
        <button type="button" onClick={goBack} aria-label="Quay lại"><ArrowLeft size={19} /></button>
        <h1>Chi tiết xe</h1>
      </header>

      <VehicleGallery key={vehicle.id} vehicle={vehicle} />

      <main className="detail-main">
        <section className="vehicle-overview">
          <div className="detail-eyebrow">{vehicle.brand} · {vehicle.modelYear} · {vehicle.category.replaceAll('_', ' ')}</div>
          <div className="detail-title-row">
            <h2>{vehicle.name}</h2>
            <button type="button" className="detail-rating" onClick={() => reviewsRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })} aria-label={`Xem ${reviewCount} đánh giá`}>
              <Star size={15} fill="currentColor" /> {vehicle.rating > 0 ? vehicle.rating.toFixed(1) : '—'} <small>({reviewCount})</small>
            </button>
          </div>
          <p className="detail-location"><MapPin size={16} /> {vehicle.location}</p>
          <p className={`vehicle-description ${shouldTruncateDescription && !descriptionExpanded ? 'is-clamped' : ''}`}>
            {visibleDescription || 'Chủ xe chưa cung cấp mô tả cho phương tiện này.'}
          </p>
          {shouldTruncateDescription && <button type="button" className="vehicle-description-toggle" onClick={() => setDescriptionExpanded((expanded) => !expanded)}>{descriptionExpanded ? 'Thu gọn' : 'Xem thêm'}</button>}
        </section>

        <section className="detail-specs" aria-label="Thông số kỹ thuật">
          <div><Users size={18} /><span>{vehicle.seats} chỗ</span></div>
          <div><Gauge size={18} /><span>{vehicle.transmission === 'AUTOMATIC' ? 'Số tự động' : 'Số sàn'}</span></div>
          <div><Fuel size={18} /><span>{vehicle.fuelType === 'ELECTRIC' ? 'Điện' : vehicle.fuelType === 'DIESEL' ? 'Diesel' : vehicle.fuelType === 'HYBRID' ? 'Hybrid' : 'Xăng'}</span></div>
        </section>

        <section className="detail-section">
          <h2>Tiện nghi</h2>
          {vehicle.features.length > 0 ? <div className="detail-features">{vehicle.features.map((feature) => <span key={feature}><ShieldCheck size={14} />{feature}</span>)}</div> : <p className="detail-muted">Chưa có thông tin tiện nghi.</p>}
        </section>

        <section className="detail-section provider-section">
          <h2>Nhà cung cấp</h2>
          {provider ? (
            <div className="vehicle-provider-card">
              {provider.avatarUrl ? <img src={provider.avatarUrl} alt="" /> : <span className="vehicle-provider-avatar">{provider.name.slice(0, 1).toUpperCase()}</span>}
              <div><strong>{provider.name}</strong>{provider.description && <p>{provider.description}</p>}</div>
              {provider.rating !== undefined && <span className="vehicle-provider-rating"><Star size={13} fill="currentColor" /> {provider.rating.toFixed(1)}{provider.reviewCount !== undefined && ` (${provider.reviewCount})`}</span>}
            </div>
          ) : <p className="detail-muted">Thông tin nhà cung cấp chưa được cập nhật.</p>}
        </section>

        <section className="detail-section detail-price-section">
          <h2>Giá thuê và chi phí</h2>
          <div className="detail-cost-row"><span>Giá thuê</span><strong>{money(vehicle.pricePerDay)} VND <small>/ ngày</small></strong></div>
          <div className="detail-cost-row"><span>Tiền đặt cọc</span><strong>{money(vehicle.depositAmount)} VND</strong></div>
          <div className="detail-cost-row"><span>Giao xe tận nơi <small>(nếu chọn)</small></span><strong>{money(vehicle.deliveryFeeAmount ?? 100000)} VND</strong></div>
          {(vehicle.additionalFees ?? []).map((fee, index) => <div className="detail-cost-row" key={`${fee.name}-${index}`}><span>{fee.name}{fee.description && <small className="detail-fee-description">{fee.description}</small>}</span><strong>{money(fee.amount)} VND</strong></div>)}
        </section>

        <section className="detail-rating-summary">
          <div><span className="detail-rating-large"><Star size={21} fill="currentColor" /> {vehicle.rating > 0 ? vehicle.rating.toFixed(1) : '—'}</span><p>{reviewCount} đánh giá</p></div>
          <button type="button" onClick={() => reviewsRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })}>Xem nhận xét</button>
        </section>

        <div ref={reviewsRef}>
          <VehicleReviews key={vehicle.id} reviews={vehicle.reviews} totalCount={reviewCount} />
        </div>
      </main>

      <div className="vehicle-booking-bar">
        <div><span>Giá thuê</span><strong>{money(vehicle.pricePerDay)} <small>VND / ngày</small></strong></div>
        <button type="button" onClick={() => setBookingVehicle(vehicle)} disabled={!vehicle.isAvailable}>Book Now</button>
      </div>
      <BookingModal
        vehicle={bookingVehicle}
        onClose={() => setBookingVehicle(null)}
        onSuccess={(booking) => navigate(`/booking/${booking.id}`)}
        initialStartDate={locationState.pickupAt}
        initialEndDate={locationState.returnAt}
      />
    </div>
  )
}
