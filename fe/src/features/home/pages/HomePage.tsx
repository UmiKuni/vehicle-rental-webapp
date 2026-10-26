import { Link } from 'react-router-dom'
import { ArrowRight, CalendarCheck, CarFront, CheckCircle2, MapPin, ShieldCheck, WalletCards } from 'lucide-react'
import { APP_NAME, ROUTES } from '@/shared/constants'

const steps = [
  { icon: MapPin, title: 'Tìm xe', description: 'Chọn điểm nhận xe, thời gian và mẫu xe phù hợp.' },
  { icon: CalendarCheck, title: 'Gửi yêu cầu', description: 'Gửi lịch thuê để chủ xe xem và xác nhận.' },
  { icon: CheckCircle2, title: 'Chủ xe xác nhận', description: 'Bạn sẽ nhận thông báo khi yêu cầu được chấp nhận.' },
  { icon: WalletCards, title: 'Thanh toán', description: 'Hoàn tất thanh toán trước ngày nhận xe.' },
  { icon: CarFront, title: 'Nhận và trả xe', description: 'Kiểm tra xe cùng chủ xe rồi bắt đầu hành trình.' },
]

export default function HomePage() {
  return (
    <div className="home-page">
      <header className="home-header">
        <Link to={ROUTES.HOME} className="home-brand" aria-label={`${APP_NAME} - Trang chủ`}>
          <span className="home-brand-mark"><CarFront size={21} strokeWidth={2.4} /></span>
          <span>{APP_NAME}</span>
        </Link>
        <Link to={ROUTES.SEARCH} className="home-header-link">Tìm xe <ArrowRight size={16} /></Link>
      </header>

      <section className="home-hero">
        <div className="home-hero-copy">
          <span className="home-eyebrow"><ShieldCheck size={14} /> Thuê xe dễ dàng, hành trình chủ động</span>
          <h1>Đường đi của bạn.<br /><em>Chiếc xe của bạn.</em></h1>
          <p>Khám phá xe từ chủ xe địa phương, gửi yêu cầu thuê và nhận xe theo lịch trình của bạn.</p>
          <Link to={ROUTES.SEARCH} className="home-cta">Tìm xe phù hợp <ArrowRight size={17} /></Link>
          <div className="home-trust"><span className="home-trust-icon"><CheckCircle2 size={15} /></span> Xem thông tin xe và giá thuê rõ ràng trước khi đặt</div>
        </div>
        <div className="home-hero-art" role="img" aria-label="Chiếc xe sẵn sàng cho chuyến đi của bạn">
          <div className="home-art-sun" />
          <div className="home-art-label"><span>YOUR NEXT DRIVE</span><strong>Bắt đầu từ đây.</strong></div>
          <div className="home-art-road" />
          <img src="https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1100&q=85" alt="Xe sẵn sàng cho hành trình" />
          <div className="home-art-location"><MapPin size={14} /> Tự do chọn điểm nhận xe</div>
        </div>
      </section>

      <section className="home-how">
        <div className="home-section-heading">
          <span className="home-eyebrow">5 bước đơn giản</span>
          <h2>Thuê xe theo cách của bạn</h2>
          <p>Từ lúc tìm xe đến khi trả xe, mọi bước đều rõ ràng.</p>
        </div>
        <div className="home-steps">
          {steps.map(({ icon: Icon, title, description }, index) => (
            <article className="home-step" key={title}>
              <div className="home-step-top"><span className="home-step-icon"><Icon size={19} /></span><span className="home-step-index">0{index + 1}</span></div>
              <h3>{title}</h3><p>{description}</p>
            </article>
          ))}
        </div>
        <Link to={ROUTES.SEARCH} className="home-bottom-link">Khám phá xe ngay <ArrowRight size={16} /></Link>
      </section>
    </div>
  )
}
