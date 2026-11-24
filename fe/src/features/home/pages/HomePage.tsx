import { Link } from 'react-router-dom'
import { ArrowRight, CarFront } from 'lucide-react'
import { APP_NAME, ROUTES } from '@/shared/constants'

const steps = [
  { title: 'Find a car', description: 'Choose your area, dates, and car.' },
  { title: 'Send a request', description: 'Ask the owner to confirm your trip.' },
  { title: 'Get approved', description: 'The owner reviews your request.' },
  { title: 'Make payment', description: 'Pay before your pickup time.' },
  { title: 'Pick up and return', description: 'Meet the owner, then return the car.' },
]

export default function HomePage() {
  return (
    <div className="home-page">
      <header className="home-header">
        <Link to={ROUTES.HOME} className="home-brand" aria-label={`${APP_NAME} - Home`}>
          <span className="home-brand-mark"><CarFront size={21} strokeWidth={2.4} /></span>
          <span>{APP_NAME}</span>
        </Link>
      </header>

      <section className="home-hero">
        <div className="home-hero-copy">
          <span className="home-eyebrow">A simpler way to rent</span>
          <h1>Find the right car for your trip.</h1>
          <p>Browse local cars, request your dates, and confirm with the owner.</p>
          <Link to={ROUTES.SEARCH} className="home-cta">Find a car <ArrowRight size={17} /></Link>
        </div>
        <div className="home-hero-art" role="img" aria-label="A car ready for your next trip">
          <img src="https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1100&q=85" alt="Car ready for your next trip" />
        </div>
      </section>

      <section className="home-how">
        <div className="home-section-heading">
          <span className="home-eyebrow">Five simple steps</span>
          <h2>Rent a car your way</h2>
          <p>Every step is clear, from finding a car to returning it.</p>
        </div>
        <div className="home-steps">
          {steps.map(({ title, description }, index) => (
            <article className="home-step" key={title}>
              <div className="home-step-top"><span className="home-step-index">0{index + 1}</span></div>
              <h3>{title}</h3><p>{description}</p>
            </article>
          ))}
        </div>
      </section>
    </div>
  )
}
