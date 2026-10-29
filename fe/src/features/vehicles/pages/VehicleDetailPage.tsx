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
  if (isError || !vehicle) return <div className="vehicle-detail-loading"><ErrorState message="Vehicle details could not be found." onRetry={() => void refetch()} /></div>

  return <div className="vehicle-detail-page">
    <header className="detail-topbar"><Link to={ROUTES.SEARCH}><ArrowLeft size={18} /> Back to search</Link><span>Vehicle details</span></header>
    <div className="detail-image-wrap"><img src={vehicle.imageUrl} alt={vehicle.name} /><span className="detail-availability"><span /> Available for requests</span></div>
    <section className="detail-main">
      <div className="detail-title-row"><div><div className="detail-eyebrow">{vehicle.brand} · {vehicle.modelYear}</div><h1>{vehicle.name}</h1></div><div className="detail-rating"><Star size={15} fill="currentColor" /> {vehicle.rating}<small>({vehicle.tripsCount} trips)</small></div></div>
      <p className="detail-location"><MapPin size={16} /> {vehicle.location}</p>
      <div className="detail-specs"><div><Users size={18} /><span>{vehicle.seats} seats</span></div><div><Gauge size={18} /><span>{vehicle.transmission === 'AUTOMATIC' ? 'Automatic' : 'Manual'}</span></div><div><Fuel size={18} /><span>{vehicle.fuelType === 'ELECTRIC' ? 'Electric' : vehicle.fuelType === 'DIESEL' ? 'Diesel' : vehicle.fuelType === 'HYBRID' ? 'Hybrid' : 'Gasoline'}</span></div></div>
      <div className="detail-section"><h2>About this car</h2><p>Listed by a local car owner. Send a rental request to confirm availability and pickup details.</p><div className="detail-features">{vehicle.features.map((feature) => <span key={feature}><ShieldCheck size={14} />{feature}</span>)}</div></div>
      <div className="detail-price"><div><strong>{new Intl.NumberFormat('en-US').format(vehicle.pricePerDay)} VND</strong><span> / day</span></div><small>Deposit {new Intl.NumberFormat('en-US').format(vehicle.depositAmount)} VND</small></div>
      <button type="button" className="detail-book-button" onClick={() => setBookingVehicle(vehicle)}>Book now <span>Send a rental request</span></button>
    </section>
    <BookingModal vehicle={bookingVehicle} onClose={() => setBookingVehicle(null)} onSuccess={() => navigate(ROUTES.BOOKINGS)} />
  </div>
}
