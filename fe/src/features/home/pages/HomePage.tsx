import { Link } from 'react-router-dom'
import { ArrowRight, CalendarCheck, CarFront, CheckCircle2, MapPin, ShieldCheck, WalletCards } from 'lucide-react'
import { APP_NAME, ROUTES } from '@/shared/constants'

const steps = [
  { icon: MapPin, title: 'Find a car', description: 'Choose your pickup area, dates, and the car that fits your trip.' },
  { icon: CalendarCheck, title: 'Send a request', description: 'Share your rental dates with the car owner for approval.' },
  { icon: CheckCircle2, title: 'Get approved', description: 'We’ll notify you as soon as the owner accepts your request.' },
  { icon: WalletCards, title: 'Make payment', description: 'Complete payment before your scheduled pickup.' },
  { icon: CarFront, title: 'Pick up and return', description: 'Inspect the car with its owner, then enjoy your trip.' },
]

export default function HomePage() {
  return (
    <div className="home-page">
      <header className="home-header">
        <Link to={ROUTES.HOME} className="home-brand" aria-label={`${APP_NAME} - Home`}>
          <span className="home-brand-mark"><CarFront size={21} strokeWidth={2.4} /></span>
          <span>{APP_NAME}</span>
        </Link>
        <Link to={ROUTES.SEARCH} className="home-header-link">Find a car <ArrowRight size={16} /></Link>
      </header>

      <section className="home-hero">
        <div className="home-hero-copy">
          <span className="home-eyebrow"><ShieldCheck size={14} /> Easy rentals. Your own way.</span>
          <h1>Your road.<br /><em>Your ride.</em></h1>
          <p>Explore cars from local owners, send a rental request, and pick up on your schedule.</p>
          <Link to={ROUTES.SEARCH} className="home-cta">Find your next car <ArrowRight size={17} /></Link>
          <div className="home-trust"><span className="home-trust-icon"><CheckCircle2 size={15} /></span> Clear vehicle details and pricing before you book</div>
        </div>
        <div className="home-hero-art" role="img" aria-label="A car ready for your next trip">
          <div className="home-art-sun" />
          <div className="home-art-label"><span>YOUR NEXT DRIVE</span><strong>Starts right here.</strong></div>
          <div className="home-art-road" />
          <img src="https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1100&q=85" alt="Car ready for your next trip" />
          <div className="home-art-location"><MapPin size={14} /> Choose your pickup location</div>
        </div>
      </section>

      <section className="home-how">
        <div className="home-section-heading">
          <span className="home-eyebrow">Five simple steps</span>
          <h2>Rent a car your way</h2>
          <p>Every step is clear, from finding a car to returning it.</p>
        </div>
        <div className="home-steps">
          {steps.map(({ icon: Icon, title, description }, index) => (
            <article className="home-step" key={title}>
              <div className="home-step-top"><span className="home-step-icon"><Icon size={19} /></span><span className="home-step-index">0{index + 1}</span></div>
              <h3>{title}</h3><p>{description}</p>
            </article>
          ))}
        </div>
        <Link to={ROUTES.SEARCH} className="home-bottom-link">Explore available cars <ArrowRight size={16} /></Link>
      </section>
    </div>
  )
}
