import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft, CheckCircle2 } from 'lucide-react'
import { ROUTES } from '@/shared/constants'
import { useKycSubmit } from '../hooks/useKycSubmit'
import { KycStep1VehicleType } from '../components/KycStep1VehicleType'
import { KycStep2Cccd } from '../components/KycStep2Cccd'
import { KycStep3PersonalInfo } from '../components/KycStep3PersonalInfo'
import type { KycStep1Values, KycStep2Values, KycStep3Values } from '../schemas/kyc.schema'
import { cn } from '@/shared/utils'

type Step = 1 | 2 | 3

const STEPS = [
  { num: 1 as Step, title: 'Loại xe', shortTitle: 'Phương tiện' },
  { num: 2 as Step, title: 'Hộ chiếu / CCCD', shortTitle: 'Giấy tờ' },
  { num: 3 as Step, title: 'Cá nhân', shortTitle: 'Thông tin' },
]

interface WizardData {
  step1?: KycStep1Values
  step2?: KycStep2Values
  step3?: KycStep3Values
}

export default function KycWizardPage() {
  const navigate = useNavigate()
  const [currentStep, setCurrentStep] = useState<Step>(1)
  const [data, setData] = useState<WizardData>({})
  const { mutate: submitKyc, isPending, isSuccess } = useKycSubmit()

  const handleStep1 = (step1: KycStep1Values) => {
    setData((d) => ({ ...d, step1 }))
    setCurrentStep(2)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const handleStep2 = (step2: KycStep2Values) => {
    setData((d) => ({ ...d, step2 }))
    setCurrentStep(3)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const handleStep3 = (step3: KycStep3Values) => {
    if (!data.step1 || !data.step2) return
    submitKyc({
      step1: data.step1,
      step2: {
        docType: data.step2.docType,
        idNumber: data.step2.idNumber,
        country: data.step2.country,
        expiryDate: data.step2.expiryDate,
        cccdNumber: data.step2.idNumber,
        frontPreview: data.step2.frontPreview ?? data.step2.cccdFrontPreview,
        backPreview: data.step2.backPreview ?? data.step2.cccdBackPreview,
      },
      step3,
    })
  }

  if (isSuccess) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] px-6 text-center">
        <div className="flex h-20 w-20 items-center justify-center rounded-full bg-emerald-100 dark:bg-emerald-950/50 mb-6">
          <CheckCircle2 className="size-10 text-emerald-600 dark:text-emerald-400" />
        </div>
        <h2 className="text-xl font-bold text-foreground">Hồ sơ đã được gửi!</h2>
        <p className="mt-2 text-sm text-muted-foreground max-w-xs leading-relaxed">
          Chúng tôi sẽ xem xét và phản hồi trong vòng <strong>1–2 ngày làm việc</strong>. Bạn sẽ nhận thông báo qua email.
        </p>
        <button
          onClick={() => navigate(ROUTES.PROFILE)}
          className="mt-8 rounded-xl bg-[#1a8a9d] px-6 py-3 text-sm font-semibold text-white hover:bg-[#147485] transition-colors active-press"
        >
          Về trang hồ sơ
        </button>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-background">
      {/* Sticky header */}
      <div className="sticky top-0 z-20 border-b border-border bg-background/95 backdrop-blur">
        <div className="flex items-center gap-3 px-4 py-3.5">
          <button
            onClick={() => (currentStep > 1 ? setCurrentStep((s) => (s - 1) as Step) : navigate(ROUTES.PROFILE))}
            className="flex h-9 w-9 items-center justify-center rounded-full hover:bg-accent transition-colors"
          >
            <ArrowLeft className="size-5 text-foreground" />
          </button>
          <div className="flex-1 min-w-0">
            <h1 className="text-sm font-bold text-foreground truncate">Xác thực tài khoản</h1>
            <p className="text-[11px] text-muted-foreground">Bước {currentStep} / 3</p>
          </div>
        </div>

        {/* Step progress pills */}
        <div className="flex items-center gap-1 px-4 pb-3">
          {STEPS.map(({ num, shortTitle }) => (
            <div key={num} className="flex items-center gap-1 flex-1">
              <div className="flex-1">
                <div
                  className={cn(
                    'h-1.5 rounded-full transition-all duration-300',
                    currentStep > num
                      ? 'bg-emerald-500'
                      : currentStep === num
                        ? 'bg-[#1a8a9d]'
                        : 'bg-border'
                  )}
                />
                <p className={cn(
                  'mt-1 text-[10px] font-medium text-center transition-colors',
                  currentStep === num ? 'text-[#1a8a9d]' : currentStep > num ? 'text-emerald-600 dark:text-emerald-400' : 'text-muted-foreground'
                )}>
                  {currentStep > num ? '✓ ' : ''}{shortTitle}
                </p>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Step content */}
      <div className="px-4 py-6 pb-24 max-w-lg mx-auto">
        {/* Step title */}
        <div className="mb-6">
          <h2 className="text-lg font-bold text-foreground">
            {currentStep === 1 && 'Loại phương tiện & bằng lái'}
            {currentStep === 2 && 'Xác thực Hộ chiếu (Passport) / CCCD'}
            {currentStep === 3 && 'Thông tin cá nhân'}
          </h2>
          <p className="mt-1 text-xs text-muted-foreground">
            {currentStep === 1 && 'Cho chúng tôi biết bạn quan tâm đến loại xe nào'}
            {currentStep === 2 && 'Chụp ảnh hộ chiếu hoặc CCCD để xác minh danh tính của bạn'}
            {currentStep === 3 && 'Điền thông tin cá nhân và liên hệ khẩn cấp'}
          </p>
        </div>

        {/* Step forms */}
        {currentStep === 1 && (
          <KycStep1VehicleType
            defaultValues={data.step1}
            onNext={handleStep1}
          />
        )}
        {currentStep === 2 && (
          <KycStep2Cccd
            defaultValues={data.step2}
            onNext={handleStep2}
            onBack={() => setCurrentStep(1)}
          />
        )}
        {currentStep === 3 && (
          <KycStep3PersonalInfo
            defaultValues={data.step3}
            onSubmit={handleStep3}
            onBack={() => setCurrentStep(2)}
            isSubmitting={isPending}
          />
        )}
      </div>
    </div>
  )
}
