import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Fuel, Gauge, MapPin, ShieldCheck, Star, Users } from 'lucide-react'
import { useVehicleDetail } from '../hooks/useVehicles'
import { BookingModal } from '../components/BookingModal'
import { ErrorState } from '@/shared/components/common/StateViews'
import { CardSkeleton } from '@/shared/components/common/Skeleton'
import type { Vehicle } from '@/types/vehicle.types'
import { ROUTES } from '@/shared/constants'

export default function VehicleDetailPage() {
  const { vehicleId } = useParams()
  const navigate = useNavigate()
  const { data: vehicle, isLoading, isError, refetch } = useVehicleDetail(vehicleId)
  const [bookingVehicle, setBookingVehicle] = useState<Vehicle | null>(null)
  if (isLoading) return <div className="vehicle-detail-loading"><CardSkeleton /></div>
  if (isError || !vehicle) return <div className="vehicle-detail-loading"><ErrorState message="Không tìm thấy thông tin xe." onRetry={() => void refetch()} /></div>

  return <div className="vehicle-detail-page">
    <header className="detail-topbar"><Link to={ROUTES.SEARCH}><ArrowLeft size={18} /> Quay lại tìm xe</Link><span>Chi tiết xe</span></header>
    <div className="detail-image-wrap"><img src={vehicle.imageUrl} alt={vehicle.name} /><span className="detail-availability"><span /> Sẵn sàng nhận yêu cầu</span></div>
    <section className="detail-main">
      <div className="detail-title-row"><div><div className="detail-eyebrow">{vehicle.brand} · {vehicle.modelYear}</div><h1>{vehicle.name}</h1></div><div className="detail-rating"><Star size={15} fill="currentColor" /> {vehicle.rating}<small>({vehicle.tripsCount} chuyến)</small></div></div>
      <p className="detail-location"><MapPin size={16} /> {vehicle.location}</p>
      <div className="detail-specs"><div><Users size={18} /><span>{vehicle.seats} chỗ</span></div><div><Gauge size={18} /><span>{vehicle.transmission === 'AUTOMATIC' ? 'Tự động' : 'Số sàn'}</span></div><div><Fuel size={18} /><span>{vehicle.fuelType === 'ELECTRIC' ? 'Điện' : vehicle.fuelType === 'DIESEL' ? 'Diesel' : vehicle.fuelType === 'HYBRID' ? 'Hybrid' : 'Xăng'}</span></div></div>
      <div className="detail-section"><h2>Thông tin xe</h2><p>Xe được chủ xe địa phương cung cấp. Vui lòng gửi yêu cầu để chủ xe xác nhận lịch và hướng dẫn nhận xe.</p><div className="detail-features">{vehicle.features.map((feature) => <span key={feature}><ShieldCheck size={14} />{feature}</span>)}</div></div>
      <div className="detail-price"><div><strong>{new Intl.NumberFormat('vi-VN').format(vehicle.pricePerDay)} ₫</strong><span> / ngày</span></div><small>Đặt cọc {new Intl.NumberFormat('vi-VN').format(vehicle.depositAmount)} ₫</small></div>
      <button type="button" className="detail-book-button" onClick={() => setBookingVehicle(vehicle)}>Book now <span>Gửi yêu cầu thuê xe</span></button>
    </section>
    <BookingModal vehicle={bookingVehicle} onClose={() => setBookingVehicle(null)} onSuccess={() => navigate(ROUTES.BOOKINGS)} />
  </div>
}
